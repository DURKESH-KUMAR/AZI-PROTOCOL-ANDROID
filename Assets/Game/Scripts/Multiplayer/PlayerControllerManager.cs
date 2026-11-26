using System.Collections;
using UnityEngine;
using Photon.Pun;

public class PlayerControllerManager : MonoBehaviour
{
    // Resources path. Forward slash always works; Path.Combine gives '\' on Windows.
    const string PlayerPrefab = "PhotonPrefabs/Player";

    [SerializeField] float respawnDelay = 3f;

    PhotonView view;
    GameObject controller;
    GameObject deathCamera;
    public int playerTeam;

    Transform spawnArea1;
    Transform spawnArea2;
    bool respawning;

    public float RespawnDelay => respawnDelay;

    void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    void Start()
    {
        if (view.IsMine)
            CreateController();
    }

    void OnDestroy()
    {
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
            playerTeam = PhotonNetwork.LocalPlayer.ActorNumber % 2 == 1 ? 1 : 2;
            Debug.LogWarning("No team property found, falling back to team " + playerTeam);
        }
        SpawnInTeamArea(playerTeam);
    }

    void SpawnInTeamArea(int team)
    {
        if (spawnArea1 == null || spawnArea2 == null)
        {
            GameObject a1 = GameObject.Find("SpawnArea1");
            GameObject a2 = GameObject.Find("SpawnArea2");
            if (a1 == null || a2 == null)
            {
                Debug.LogError("Spawn area not found");
                return;
            }
            spawnArea1 = a1.transform;
            spawnArea2 = a2.transform;
        }

        Transform area = team == 2 ? spawnArea2 : spawnArea1;
        if (area.childCount == 0)
        {
            Debug.LogError("No available spawn points for team: " + team);
            return;
        }

        Transform spawnPoint = PickSafestSpawn(area, team);
        controller = PhotonNetwork.Instantiate(PlayerPrefab,
            spawnPoint.position, spawnPoint.rotation, 0, new object[] { view.ViewID });
    }

    /// <summary>
    /// Spawn point farthest from living enemies (with a little randomness), so players
    /// are not spawn-killed by someone standing on top of a spawn point.
    /// </summary>
    static Transform PickSafestSpawn(Transform area, int team)
    {
        Transform best = area.GetChild(Random.Range(0, area.childCount));
        float bestScore = float.MinValue;
        for (int i = 0; i < area.childCount; i++)
        {
            Transform point = area.GetChild(i);
            float nearestEnemy = 10000f;
            for (int p = 0; p < PlayerRegistry.All.Count; p++)
            {
                PlayerMovement other = PlayerRegistry.All[p];
                if (other == null || other.IsDead || other.playerTeam == team)
                    continue;
                nearestEnemy = Mathf.Min(nearestEnemy, (other.transform.position - point.position).sqrMagnitude);
            }
            float score = nearestEnemy + Random.Range(0f, 25f);
            if (score > bestScore)
            {
                bestScore = score;
                best = point;
            }
        }
        return best;
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
