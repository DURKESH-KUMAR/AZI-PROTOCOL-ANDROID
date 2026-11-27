using System.Collections;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// One per player (spawned by RoomManager in the match). Owns the local player's body:
/// spawns it at a point in the TEAM's spawn area (SpawnArea1 = Blue, SpawnArea2 = Red),
/// and after a death keeps the body on screen for a moment (death camera), removes it and
/// respawns a fresh body - full health, full ammo, spawn protection - at the safest point
/// of the same team area.
/// </summary>
public class PlayerControllerManager : MonoBehaviour
{
    // Resources path. Forward slash always works; Path.Combine gives '\' on Windows.
    const string PlayerPrefab = "PhotonPrefabs/Player";
    const float BodyClearance = 1.2f; // metres that must be free of other players around a spawn point

    [SerializeField] float respawnDelay = 3f;

    PhotonView view;
    GameObject controller;
    GameObject deathCamera;
    public int playerTeam;

    Transform spawnArea1;
    Transform spawnArea2;
    bool respawning;

    static readonly Collider[] overlap = new Collider[8];

    public float RespawnDelay => respawnDelay;
    public bool IsMine => view != null && view.IsMine;

    /// <summary>The local player's manager (fallback for bodies that lost their reference).</summary>
    public static PlayerControllerManager Local { get; private set; }

    void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    void Start()
    {
        if (!view.IsMine)
            return;
        Local = this;
        CreateController();
    }

    void OnDestroy()
    {
        if (Local == this)
            Local = null;
        if (deathCamera != null)
            Destroy(deathCamera);
    }

    void CreateController()
    {
        if (deathCamera != null)
        {
            Destroy(deathCamera); // only one camera / AudioListener at a time
            deathCamera = null;
        }

        playerTeam = PlayerTeams.GetTeam(PhotonNetwork.LocalPlayer);
        if (playerTeam == 0)
        {
            // Should not happen (master assigns in the lobby), but never leave a player unspawned.
            playerTeam = PhotonNetwork.LocalPlayer.ActorNumber % 2 == 1 ? Teams.Blue : Teams.Red;
            Debug.LogWarning("No team property found, falling back to team " + playerTeam);
            // Publish it: everyone (damage checks, scoreboard, results) must see the same team.
            // CAS on "no team yet" so a team the master assigns at the same moment wins.
            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable { { PlayerTeams.TeamKey, playerTeam } },
                new ExitGames.Client.Photon.Hashtable { { PlayerTeams.TeamKey, null } });
        }
        SpawnInTeamArea(playerTeam);
    }

    void SpawnInTeamArea(int team)
    {
        Transform area = SpawnArea(team);
        if (area == null || area.childCount == 0)
        {
            Debug.LogError("No spawn points for team " + team + " (expected SpawnArea1 / SpawnArea2 with child points).");
            return;
        }

        Transform spawnPoint = PickSafestSpawn(area, team);
        controller = PhotonNetwork.Instantiate(PlayerPrefab,
            spawnPoint.position, spawnPoint.rotation, 0, new object[] { view.ViewID });
    }

    Transform SpawnArea(int team)
    {
        if (spawnArea1 == null || spawnArea2 == null)
        {
            GameObject a1 = GameObject.Find("SpawnArea1");
            GameObject a2 = GameObject.Find("SpawnArea2");
            spawnArea1 = a1 != null ? a1.transform : null;
            spawnArea2 = a2 != null ? a2.transform : null;
        }
        return team == Teams.Red ? spawnArea2 : spawnArea1;
    }

    /// <summary>
    /// Best point of the team area: as far as possible from living enemies, never on top of
    /// another player (teammate or enemy), with a little randomness so respawns vary.
    /// </summary>
    static Transform PickSafestSpawn(Transform area, int team)
    {
        Transform best = area.GetChild(Random.Range(0, area.childCount));
        float bestScore = float.MinValue;
        for (int i = 0; i < area.childCount; i++)
        {
            Transform point = area.GetChild(i);
            float nearestEnemy = 10000f;
            bool occupied = false;
            for (int p = 0; p < PlayerRegistry.All.Count; p++)
            {
                PlayerMovement other = PlayerRegistry.All[p];
                if (other == null || other.IsDead)
                    continue;
                float sqr = (other.transform.position - point.position).sqrMagnitude;
                if (sqr < BodyClearance * BodyClearance)
                    occupied = true;
                if (other.playerTeam != team)
                    nearestEnemy = Mathf.Min(nearestEnemy, sqr);
            }
            // Bodies not registered yet (just spawned elsewhere) still have colliders.
            if (!occupied)
                occupied = Physics.OverlapSphereNonAlloc(point.position + Vector3.up, BodyClearance * 0.5f, overlap, ~0, QueryTriggerInteraction.Ignore) > 0
                           && HasPlayerCollider(overlap);

            float score = nearestEnemy + Random.Range(0f, 25f) - (occupied ? 1e6f : 0f);
            if (score > bestScore)
            {
                bestScore = score;
                best = point;
            }
        }
        return best;
    }

    static bool HasPlayerCollider(Collider[] hits)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i].GetComponentInParent<PlayerMovement>() != null)
            {
                System.Array.Clear(hits, 0, hits.Length);
                return true;
            }
        }
        System.Array.Clear(hits, 0, hits.Length);
        return false;
    }

    /// <summary>Name of the base this player respawns at ("BLUE BASE").</summary>
    public string BaseName
    {
        get
        {
            int team = playerTeam != 0 ? playerTeam : PlayerTeams.GetTeam(PhotonNetwork.LocalPlayer);
            return Teams.Name(team) + " BASE";
        }
    }

    /// <summary>
    /// Called by the local dying body. Keeps the body on screen for <paramref name="corpseTime"/>
    /// seconds watched by a death camera, removes it, then respawns after the respawn delay.
    /// </summary>
    public void Die(GameObject body, float corpseTime)
    {
        if (respawning)
            return;

        if (body != null)
        {
            if (controller == null)
                controller = body; // body found us through the fallback: we still own it
            CameraManager cameraManager = body.GetComponentInChildren<CameraManager>(true);
            Camera cam = cameraManager != null ? cameraManager.DetachCamera() : null;
            if (cam != null)
            {
                deathCamera = cam.gameObject;
                deathCamera.AddComponent<DeathCamera>().Begin(body.transform);
            }
        }

        StartCoroutine(RespawnAfterDelay(Mathf.Min(corpseTime, respawnDelay)));
    }

    // Kept for callers that still use the old signature.
    public void Die() => Die(controller, 0f);

    IEnumerator RespawnAfterDelay(float corpseTime)
    {
        respawning = true;

        if (corpseTime > 0f)
            yield return new WaitForSeconds(corpseTime);
        if (controller != null)
        {
            PhotonNetwork.Destroy(controller);
            controller = null;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay - corpseTime));
        respawning = false;
        if (view.IsMine && PhotonNetwork.InRoom && !MatchState.IsOver)
            CreateController();
    }
}
