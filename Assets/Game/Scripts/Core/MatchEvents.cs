using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public struct KillInfo
{
    public int killerActor;   // -1 = environment (fell off the map)
    public int victimActor;
    public int killerTeam;
    public int victimTeam;
    public string killerName;
    public string victimName;

    public bool IsEnvironment => killerActor <= 0 || killerActor == victimActor;
    public bool LocalIsKiller => PhotonNetwork.LocalPlayer != null && killerActor == PhotonNetwork.LocalPlayer.ActorNumber && !IsEnvironment;
    public bool LocalIsVictim => PhotonNetwork.LocalPlayer != null && victimActor == PhotonNetwork.LocalPlayer.ActorNumber;
}

/// <summary>
/// Plain C# events that connect gameplay to HUD/audio without hard references,
/// so gameplay scripts keep working even when no HUD exists.
/// </summary>
public static class MatchEvents
{
    /// <summary>Raised on every client for every kill (kill feed).</summary>
    public static event Action<KillInfo> Kill;
    /// <summary>The local player's shot hit an enemy.</summary>
    public static event Action LocalHit;
    /// <summary>The local player took damage: amount, attacker world position.</summary>
    public static event Action<float, Vector3> LocalDamaged;
    /// <summary>The local player died: killer name (empty = environment), respawn delay.</summary>
    public static event Action<string, float> LocalDied;
    /// <summary>A new local player body spawned.</summary>
    public static event Action<PlayerMovement> LocalSpawned;
    /// <summary>The match ended on this client: winning team (0 = draw).</summary>
    public static event Action<int> MatchOver;

    public static void RaiseKill(KillInfo info) => Kill?.Invoke(info);
    public static void RaiseLocalHit() => LocalHit?.Invoke();
    public static void RaiseLocalDamaged(float amount, Vector3 from) => LocalDamaged?.Invoke(amount, from);
    public static void RaiseLocalDied(string killer, float respawn) => LocalDied?.Invoke(killer, respawn);
    public static void RaiseLocalSpawned(PlayerMovement player) => LocalSpawned?.Invoke(player);
    public static void RaiseMatchOver(int winner) => MatchOver?.Invoke(winner);

    public static string NameOf(int actor)
    {
        if (actor <= 0 || PhotonNetwork.CurrentRoom == null)
            return "";
        Player p = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        return p != null && !string.IsNullOrEmpty(p.NickName) ? p.NickName : "Player " + actor;
    }
}

/// <summary>All live player bodies (local and remote), for radar, spawn picking and hit validation.</summary>
public static class PlayerRegistry
{
    public static readonly List<PlayerMovement> All = new List<PlayerMovement>();
    public static PlayerMovement Local { get; private set; }

    public static void Register(PlayerMovement p)
    {
        if (!All.Contains(p))
            All.Add(p);
    }

    public static void Unregister(PlayerMovement p)
    {
        All.Remove(p);
        if (Local == p)
            Local = null;
    }

    public static void SetLocal(PlayerMovement p)
    {
        Local = p;
        MatchEvents.RaiseLocalSpawned(p);
    }

    public static PlayerMovement FindByActor(int actor)
    {
        for (int i = 0; i < All.Count; i++)
        {
            PlayerMovement p = All[i];
            if (p != null && p.OwnerActor == actor)
                return p;
        }
        return null;
    }
}
