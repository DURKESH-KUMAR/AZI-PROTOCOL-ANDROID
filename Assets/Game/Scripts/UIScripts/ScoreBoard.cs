using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Scores live in ROOM custom properties so every client (and anyone reconnecting)
/// sees the same values. Only the master client changes them, so two deaths in the
/// same moment can't overwrite each other.
/// Team 1 deaths score for Red, team 2 deaths score for Blue.
/// </summary>
public class ScoreBoard : MonoBehaviourPunCallbacks
{
    public static ScoreBoard instance;

    const string BlueKey = "BlueScore";
    const string RedKey = "RedScore";

    public Text blueTeamText;
    public Text redTeamText;
    public int blueTeamScore = 0;
    public int redTeamScore = 0;
    private PhotonView view;

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
        // New match: master resets the score. Everyone else just reads what's there.
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { BlueKey, 0 }, { RedKey, 0 } });
            blueTeamScore = redTeamScore = 0;
            masterBlue = masterRed = 0;
            masterCountsReady = true;
            RefreshUI();
        }
        else
        {
            ReadScoresFromRoom();
        }
    }

    /// <summary>Called by the dying (local) player.</summary>
    public void PlayerDied(int playerTeam)
    {
        if (playerTeam != 1 && playerTeam != 2)
            return;
        view.RPC(nameof(RPC_ReportDeath), RpcTarget.MasterClient, playerTeam);
    }

    // Master-only authoritative counters. Room properties echo back asynchronously, so the
    // master must not re-read them between increments (a stale echo would lose a kill).
    bool masterCountsReady;
    int masterBlue, masterRed;

    [PunRPC]
    void RPC_ReportDeath(int deadPlayerTeam)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        if (!masterCountsReady)
        {
            ReadScoresFromRoom();
            masterBlue = blueTeamScore;
            masterRed = redTeamScore;
            masterCountsReady = true;
        }

        if (deadPlayerTeam == 2) masterBlue++;
        else if (deadPlayerTeam == 1) masterRed++;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { BlueKey, masterBlue }, { RedKey, masterRed } });
        blueTeamScore = masterBlue;
        redTeamScore = masterRed;
        RefreshUI();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // New master re-seeds from the room on its next report.
        masterCountsReady = false;
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
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
