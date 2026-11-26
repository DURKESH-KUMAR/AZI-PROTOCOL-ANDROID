using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Match authority (runs on the master client).
/// - Team scores live in ROOM properties, per-player kills/deaths in PLAYER properties,
///   so every client (and anyone reconnecting) sees the same values.
/// - Only the master changes them, so two deaths at the same moment can't overwrite each other.
/// - Broadcasts the kill feed, checks the score limit and ends the match (-> Results scene).
/// Team 1 (BLUE) deaths score for Red, team 2 (RED) deaths score for Blue.
/// </summary>
public class ScoreBoard : MonoBehaviourPunCallbacks
{
    public static ScoreBoard instance;

    const string BlueKey = GameProps.BlueScore;
    const string RedKey = GameProps.RedScore;

    public Text blueTeamText;
    public Text redTeamText;
    public int blueTeamScore = 0;
    public int redTeamScore = 0;

    [Tooltip("First team to reach this many kills wins. 0 = no limit (time only).")]
    [SerializeField] int scoreLimit = 25;
    [Tooltip("Seconds the VICTORY/DEFEAT banner shows before the Results scene loads.")]
    [SerializeField] float endBannerTime = 4f;

    private PhotonView view;

    // Master-only authoritative counters. Properties echo back asynchronously, so the
    // master must not re-read them between increments (a stale echo would lose a kill).
    bool masterCountsReady;
    int masterBlue, masterRed;
    readonly Dictionary<int, int> masterKills = new Dictionary<int, int>();
    readonly Dictionary<int, int> masterDeaths = new Dictionary<int, int>();
    bool loadingResults;

    public int ScoreLimit => scoreLimit;

    void Awake()
    {
        view = GetComponent<PhotonView>();
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Start()
    {
        MatchState.Reset();

        // New match: master resets everything. Everyone else just reads what's there.
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { BlueKey, 0 }, { RedKey, 0 },
                { GameProps.MatchOver, false }, { GameProps.Winner, 0 }, { GameProps.EndReason, GameProps.EndReasonTime }
            });
            foreach (Player p in PhotonNetwork.PlayerList)
                p.SetCustomProperties(new Hashtable { { GameProps.Kills, 0 }, { GameProps.Deaths, 0 } });

            blueTeamScore = redTeamScore = 0;
            masterBlue = masterRed = 0;
            masterKills.Clear();
            masterDeaths.Clear();
            masterCountsReady = true;
            RefreshUI();
        }
        else
        {
            ReadScoresFromRoom();
        }
    }

    // ---------------------------------------------------------------- deaths

    /// <summary>Called by the dying (local) player. killerActor = -1 for environment deaths.</summary>
    public void ReportDeath(int victimTeam, int victimActor, int killerActor)
    {
        if (victimTeam != 1 && victimTeam != 2)
            return;
        view.RPC(nameof(RPC_ReportDeath), RpcTarget.MasterClient, victimTeam, victimActor, killerActor);
    }

    // Kept for callers using the old API.
    public void PlayerDied(int playerTeam) => ReportDeath(playerTeam, PhotonNetwork.LocalPlayer.ActorNumber, -1);

    [PunRPC]
    void RPC_ReportDeath(int deadPlayerTeam, int victimActor, int killerActor, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || MatchState.IsOver)
            return;
        // Only the victim may report its own death.
        if (info.Sender != null && info.Sender.ActorNumber != victimActor)
            return;

        if (!masterCountsReady)
        {
            ReadScoresFromRoom();
            masterBlue = blueTeamScore;
            masterRed = redTeamScore;
            masterCountsReady = true;
        }

        // Team score: any death of a team member scores for the other team (falls included).
        if (deadPlayerTeam == 2) masterBlue++;
        else if (deadPlayerTeam == 1) masterRed++;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { BlueKey, masterBlue }, { RedKey, masterRed } });
        blueTeamScore = masterBlue;
        redTeamScore = masterRed;
        RefreshUI();

        // Per-player stats.
        Player victim = PhotonNetwork.CurrentRoom.GetPlayer(victimActor);
        if (victim != null)
            victim.SetCustomProperties(new Hashtable { { GameProps.Deaths, Increment(masterDeaths, victim, GameProps.Deaths) } });

        Player killer = killerActor > 0 && killerActor != victimActor ? PhotonNetwork.CurrentRoom.GetPlayer(killerActor) : null;
        if (killer != null)
            killer.SetCustomProperties(new Hashtable { { GameProps.Kills, Increment(masterKills, killer, GameProps.Kills) } });

        int killerTeam = killer != null ? PlayerTeams.GetTeam(killer) : 0;
        view.RPC(nameof(RPC_KillFeed), RpcTarget.All, killer != null ? killerActor : -1, victimActor, killerTeam, deadPlayerTeam);

        if (scoreLimit > 0 && (masterBlue >= scoreLimit || masterRed >= scoreLimit))
            EndMatch(GameProps.EndReasonScore);
    }

    static int Increment(Dictionary<int, int> counts, Player player, string key)
    {
        if (!counts.TryGetValue(player.ActorNumber, out int value))
            value = GameProps.GetInt(player.CustomProperties, key);
        value++;
        counts[player.ActorNumber] = value;
        return value;
    }

    [PunRPC]
    void RPC_KillFeed(int killerActor, int victimActor, int killerTeam, int victimTeam)
    {
        MatchEvents.RaiseKill(new KillInfo
        {
            killerActor = killerActor,
            victimActor = victimActor,
            killerTeam = killerTeam,
            victimTeam = victimTeam,
            killerName = MatchEvents.NameOf(killerActor),
            victimName = MatchEvents.NameOf(victimActor),
        });
    }

    // ---------------------------------------------------------------- match end

    /// <summary>Master only: freeze the match, announce the winner, then load Results for everyone.</summary>
    public void EndMatch(int reason)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || MatchState.IsOver)
            return;

        int blue = masterCountsReady ? masterBlue : blueTeamScore;
        int red = masterCountsReady ? masterRed : redTeamScore;
        int winner = blue > red ? Teams.Blue : red > blue ? Teams.Red : 0;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { GameProps.MatchOver, true }, { GameProps.Winner, winner }, { GameProps.EndReason, reason }
        });
        // Final roster in the room, so Results is complete even for a phone that arrives
        // after other players have already left.
        MatchResults.Publish(masterKills, masterDeaths);
        SetMatchOver(winner);
        StartCoroutine(LoadResultsAfterBanner());
    }

    IEnumerator LoadResultsAfterBanner()
    {
        if (loadingResults)
            yield break;
        loadingResults = true;
        yield return new WaitForSeconds(endBannerTime);
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            PhotonNetwork.LoadLevel(GameScenes.AfterMatch);
        else
            loadingResults = false; // master switched mid-banner; the new master takes over
    }

    void SetMatchOver(int winner)
    {
        if (MatchState.IsOver)
            return;
        MatchState.IsOver = true;
        MatchState.Winner = winner;
        MatchEvents.RaiseMatchOver(winner);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // New master re-seeds from the room on its next report.
        masterCountsReady = false;
        masterKills.Clear();
        masterDeaths.Clear();
        // If the old master left during the end banner, finish the job.
        if (PhotonNetwork.IsMasterClient && MatchState.IsOver)
            StartCoroutine(LoadResultsAfterBanner());
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(GameProps.MatchOver) && GameProps.GetBool(propertiesThatChanged, GameProps.MatchOver))
            SetMatchOver(GameProps.GetInt(PhotonNetwork.CurrentRoom.CustomProperties, GameProps.Winner));

        if (masterCountsReady && PhotonNetwork.IsMasterClient)
            return; // master already shows its authoritative values
        if (propertiesThatChanged.ContainsKey(BlueKey) || propertiesThatChanged.ContainsKey(RedKey))
            ReadScoresFromRoom();
    }

    void ReadScoresFromRoom()
    {
        Room room = PhotonNetwork.CurrentRoom;
        if (room == null)
            return;
        if (room.CustomProperties.TryGetValue(BlueKey, out object b) && b is int blue) blueTeamScore = blue;
        if (room.CustomProperties.TryGetValue(RedKey, out object r) && r is int red) redTeamScore = red;
        RefreshUI();
    }

    void RefreshUI()
    {
        if (blueTeamText != null) blueTeamText.text = blueTeamScore.ToString();
        if (redTeamText != null) redTeamText.text = redTeamScore.ToString();
    }
}
