using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Immutable snapshot of a finished (or abandoned) match, shown by the Results screen.
/// </summary>
public sealed class MatchResult
{
    public struct Entry
    {
        public int actor;
        public string name;
        public int team, kills, deaths;
        public float Ratio => deaths == 0 ? kills : kills / (float)deaths;
    }

    public int blueScore, redScore, winner, endReason;
    public int localActor, localTeam;
    public bool leftEarly;
    public readonly List<Entry> players = new List<Entry>();

    public bool LocalWon => winner != 0 && winner == localTeam;
}

/// <summary>
/// Builds <see cref="MatchResult"/>s. At match end the master publishes a compact roster
/// (parallel arrays) into the room, so a slow phone that reaches the Results scene after
/// others already left still sees every player.
/// </summary>
public static class MatchResults
{
    const string ActorsKey = "ResA", NamesKey = "ResN", TeamsKey = "ResT", KillsKey = "ResK", DeathsKey = "ResD";

    /// <summary>Set before loading Results without a room (player left mid-match); consumed once.</summary>
    public static MatchResult Pending;

    /// <summary>Master only: write the final roster into the room properties.</summary>
    public static void Publish(Dictionary<int, int> kills, Dictionary<int, int> deaths)
    {
        Room room = PhotonNetwork.CurrentRoom;
        if (room == null || !PhotonNetwork.IsMasterClient)
            return;

        Player[] list = PhotonNetwork.PlayerList;
        int[] actors = new int[list.Length], teams = new int[list.Length], k = new int[list.Length], d = new int[list.Length];
        string[] names = new string[list.Length];
        for (int i = 0; i < list.Length; i++)
        {
            Player p = list[i];
            actors[i] = p.ActorNumber;
            names[i] = DisplayName(p);
            teams[i] = PlayerTeams.GetTeam(p);
            k[i] = kills != null && kills.TryGetValue(p.ActorNumber, out int kv) ? kv : GameProps.GetInt(p.CustomProperties, GameProps.Kills);
            d[i] = deaths != null && deaths.TryGetValue(p.ActorNumber, out int dv) ? dv : GameProps.GetInt(p.CustomProperties, GameProps.Deaths);
        }
        room.SetCustomProperties(new Hashtable { { ActorsKey, actors }, { NamesKey, names }, { TeamsKey, teams }, { KillsKey, k }, { DeathsKey, d } });
    }

    /// <summary>Snapshot from the current room (published roster if present, else live players).</summary>
    public static MatchResult Capture(bool leftEarly)
    {
        Room room = PhotonNetwork.CurrentRoom;
        if (room == null)
            return null;

        Hashtable props = room.CustomProperties;
        MatchResult r = new MatchResult
        {
            blueScore = GameProps.GetInt(props, GameProps.BlueScore),
            redScore = GameProps.GetInt(props, GameProps.RedScore),
            endReason = GameProps.GetInt(props, GameProps.EndReason),
            leftEarly = leftEarly,
            localActor = PhotonNetwork.LocalPlayer.ActorNumber,
            localTeam = PlayerTeams.GetTeam(PhotonNetwork.LocalPlayer),
        };
        r.winner = props.ContainsKey(GameProps.Winner) && GameProps.GetBool(props, GameProps.MatchOver)
            ? GameProps.GetInt(props, GameProps.Winner)
            : r.blueScore > r.redScore ? Teams.Blue : r.redScore > r.blueScore ? Teams.Red : 0;

        if (props.TryGetValue(ActorsKey, out object a) && a is int[] actors &&
            props.TryGetValue(NamesKey, out object n) && n is string[] names &&
            props.TryGetValue(TeamsKey, out object t) && t is int[] teams &&
            props.TryGetValue(KillsKey, out object k) && k is int[] kills &&
            props.TryGetValue(DeathsKey, out object d) && d is int[] deaths &&
            names.Length == actors.Length && teams.Length == actors.Length && kills.Length == actors.Length && deaths.Length == actors.Length)
        {
            for (int i = 0; i < actors.Length; i++)
                r.players.Add(new MatchResult.Entry { actor = actors[i], name = names[i], team = teams[i], kills = kills[i], deaths = deaths[i] });
        }
        else
        {
            foreach (Player p in PhotonNetwork.PlayerList)
            {
                r.players.Add(new MatchResult.Entry
                {
                    actor = p.ActorNumber,
                    name = DisplayName(p),
                    team = PlayerTeams.GetTeam(p),
                    kills = GameProps.GetInt(p.CustomProperties, GameProps.Kills),
                    deaths = GameProps.GetInt(p.CustomProperties, GameProps.Deaths),
                });
            }
        }

        r.players.Sort((x, y) => y.kills != x.kills ? y.kills.CompareTo(x.kills) : x.deaths.CompareTo(y.deaths));
        return r;
    }

    static string DisplayName(Player p) => string.IsNullOrEmpty(p.NickName) ? "Player " + p.ActorNumber : p.NickName;
}
