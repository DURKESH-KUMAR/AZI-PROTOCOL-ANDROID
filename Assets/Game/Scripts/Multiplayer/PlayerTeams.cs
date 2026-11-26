using System.Collections.Generic;
using UnityEngine;
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

    static void Count(out int team1, out int team2)
    {
        team1 = team2 = 0;
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            int t = EffectiveTeam(players[i]);
            if (t == 1) team1++;
            else if (t == 2) team2++;
        }
    }

    /// <summary>True if the local player may switch without making teams uneven by more than one.</summary>
    public static bool CanSwitchLocalTeam()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;
        int mine = GetTeam(PhotonNetwork.LocalPlayer);
        if (mine == 0)
            return false;
        Count(out int t1, out int t2);
        int from = mine == 1 ? t1 : t2;
        int to = mine == 1 ? t2 : t1;
        // After the move the gap is (from-1) vs (to+1); it stays <= 1 only if the other team is smaller now.
        return to < from;
    }

    /// <summary>
    /// Lobby only: move the local player to the other team. Uses a compare-and-swap on the
    /// current value, so it can't clash with the master assigning teams at the same moment.
    /// </summary>
    public static bool RequestSwitchLocalTeam()
    {
        if (!CanSwitchLocalTeam())
            return false;
        int mine = GetTeam(PhotonNetwork.LocalPlayer);
        int target = mine == 1 ? 2 : 1;
        return PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable { { TeamKey, target } },
            new Hashtable { { TeamKey, mine } });
    }

    /// <summary>
    /// Master, lobby only: after someone leaves, move the newest player of the bigger team
    /// so teams never differ by more than one.
    /// </summary>
    public static void Rebalance()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;
        Count(out int t1, out int t2);
        if (Mathf.Abs(t1 - t2) <= 1)
            return;

        int bigger = t1 > t2 ? 1 : 2;
        Player newest = null;
        foreach (Player p in PhotonNetwork.PlayerList)
            if (EffectiveTeam(p) == bigger && (newest == null || p.ActorNumber > newest.ActorNumber))
                newest = p;
        if (newest == null)
            return;

        int target = bigger == 1 ? 2 : 1;
        pendingTeams[newest.ActorNumber] = target;
        newest.SetCustomProperties(new Hashtable { { TeamKey, target } });
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
