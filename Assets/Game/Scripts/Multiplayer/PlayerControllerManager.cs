using System.Collections;
using System.IO;
using UnityEngine;
using Photon.Pun;

public class PlayerControllerManager : MonoBehaviour
{
    [SerializeField] float respawnDelay = 3f;

    PhotonView view;
    GameObject controller;
    public int playerTeam;

    Transform spawnArea1;
    Transform spawnArea2;
    bool respawning;

    void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    void Start()
    {
        if (view.IsMine)
            CreateController();
    }

    void CreateController()
    {
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

        Transform spawnPoint = area.GetChild(Random.Range(0, area.childCount));
        controller = PhotonNetwork.Instantiate(Path.Combine("PhotonPrefabs", "Player"),
            spawnPoint.position, spawnPoint.rotation, 0, new object[] { view.ViewID });
    }

    public void Die()
    {
        if (respawning)
            return;

        if (controller != null)
        {
            PhotonNetwork.Destroy(controller);
            controller = null;
        }
        StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay()
    {
        respawning = true;
        yield return new WaitForSeconds(respawnDelay);
        respawning = false;
        if (view.IsMine && PhotonNetwork.InRoom)
            CreateController();
    }
}
