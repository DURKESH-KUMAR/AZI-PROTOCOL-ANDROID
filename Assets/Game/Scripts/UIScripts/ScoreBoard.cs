using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Match authority (runs on the master client).
/// - Team scores live in ROOM properties, per-player kills/deaths in PLAYER properties,
///   so every client (and anyone reconnecting) sees the same values.
/// - Only the master changes them, so two deaths at the same moment can't overwrite each other.
/// - Broadcasts the kill feed, checks the score limit and ends the match (-> Results scene).
/// Team 1 (BLUE) deaths score for Red, team 2 (RED) deaths score for Blue.
/// Messages use PhotonNetwork.RaiseEvent, not RPCs: the ScoreBoard object in the match scene has
/// no PhotonView (the old RPC version threw a NullReferenceException on every death, which
/// stopped the death sequence and the respawn).
/// </summary>
public class ScoreBoard : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // Photon event codes (must be below 200; 41/42 are not used anywhere else in the project).
    const byte EvReportDeath = 41;
    const byte EvKillFeed = 42;

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
            // Safety net: anyone who reached the match without a team gets one now. Without a
            // team nobody could damage them and their kills/deaths would not count.
            PlayerTeams.AssignMissingTeams();

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
        // Always report: kills/deaths count even if the team is unknown (only the team score needs it).
        if (!PhotonNetwork.InRoom)
            return;
        PhotonNetwork.RaiseEvent(EvReportDeath, new object[] { victimTeam, victimActor, killerActor },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    // Kept for callers using the old API.
    public void PlayerDied(int playerTeam) => ReportDeath(playerTeam, PhotonNetwork.LocalPlayer.ActorNumber, -1);

    /// <summary>Photon events for this class (registered automatically by MonoBehaviourPunCallbacks).</summary>
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != EvReportDeath && photonEvent.Code != EvKillFeed)
            return;
        if (!(photonEvent.CustomData is object[] data))
            return;

        if (photonEvent.Code == EvReportDeath && data.Length >= 3)
            HandleReportDeath((int)data[0], (int)data[1], (int)data[2], photonEvent.Sender);
        else if (photonEvent.Code == EvKillFeed && data.Length >= 4)
            HandleKillFeed((int)data[0], (int)data[1], (int)data[2], (int)data[3]);
    }

    void HandleReportDeath(int deadPlayerTeam, int victimActor, int killerActor, int senderActor)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || MatchState.IsOver)
            return;
        // Only the victim may report its own death.
        if (senderActor > 0 && senderActor != victimActor)
            return;

        if (!masterCountsReady)
        {
            ReadScoresFromRoom();
            masterBlue = blueTeamScore;
            masterRed = redTeamScore;
            masterCountsReady = true;
        }

        // The master knows the teams best: prefer its view over what the victim sent.
        Player victimPlayer = PhotonNetwork.CurrentRoom.GetPlayer(victimActor);
        int knownTeam = victimPlayer != null ? PlayerTeams.GetTeam(victimPlayer) : 0;
        if (knownTeam != 0)
            deadPlayerTeam = knownTeam;

        // Team score: any death of a team member scores for the other team (falls included).
        if (deadPlayerTeam == Teams.Red) masterBlue++;
        else if (deadPlayerTeam == Teams.Blue) masterRed++;

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
        PhotonNetwork.RaiseEvent(EvKillFeed, new object[] { killer != null ? killerActor : -1, victimActor, killerTeam, deadPlayerTeam },
            new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);

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

    void HandleKillFeed(int killerActor, int victimActor, int killerTeam, int victimTeam)
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
