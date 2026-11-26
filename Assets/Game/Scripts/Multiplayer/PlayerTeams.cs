using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Single source of truth for team data. Teams live in each player's Photon
/// custom properties under <see cref="TeamKey"/> and are assigned ONLY by the
/// master client, so every client sees the same team for the same player.
/// </summary>
public static class PlayerTeams
{
    public const string TeamKey = "Team";

    /// <summary>Returns 1 or 2, or 0 if the player has no team yet.</summary>
    public static int GetTeam(Player player)
    {
        if (player != null && player.CustomProperties.TryGetValue(TeamKey, out object value) && value is int team)
            return team;
        return 0;
    }

    /// <summary>
    /// Master client only: give every player in the room without a team the
    /// team that currently has fewer members.
    /// </summary>
    public static void AssignMissingTeams()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        // Property changes only reach the local cache when the server echoes them back,
        // so remember what we already assigned in this room to avoid double-assigning.
        string roomName = PhotonNetwork.CurrentRoom.Name;
        if (pendingRoom != roomName)
        {
            pendingRoom = roomName;
            pendingTeams.Clear();
        }

        int team1 = 0, team2 = 0;
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            int t = EffectiveTeam(players[i]);
            if (t == 1) team1++;
            else if (t == 2) team2++;
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (EffectiveTeam(players[i]) != 0)
                continue;

            int team = team1 <= team2 ? 1 : 2;
            if (team == 1) team1++; else team2++;
            pendingTeams[players[i].ActorNumber] = team;
            players[i].SetCustomProperties(new Hashtable { { TeamKey, team } });
        }
    }

    static string pendingRoom;
    static readonly Dictionary<int, int> pendingTeams = new Dictionary<int, int>();

    static int EffectiveTeam(Player player)
    {
        int team = GetTeam(player);
        if (team == 0 && pendingTeams.TryGetValue(player.ActorNumber, out int pending))
            team = pending;
        return team;
    }

    /// <summary>Drops the local player's team so it does not leak into the next room.</summary>
    public static void ClearLocalTeam()
    {
        pendingRoom = null;
        pendingTeams.Clear();

        if (PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(TeamKey))
            PhotonNetwork.RemovePlayerCustomProperties(new[] { TeamKey });
    }
}
