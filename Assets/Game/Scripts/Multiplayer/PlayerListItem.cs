using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PlayerListItem : MonoBehaviourPunCallbacks
{
    public Text playerUsername;
    public Text teamText;
    Player player;

    // Display only: teams are assigned by the master client (see PlayerTeams).
    public void SetUp(Player _player)
    {
        player = _player;
        playerUsername.text = _player.NickName;
        RefreshTeam();
    }

    void RefreshTeam()
    {
        int team = PlayerTeams.GetTeam(player);
        teamText.text = team > 0 ? "Team " + team : "Team -";
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer == player && changedProps.ContainsKey(PlayerTeams.TeamKey))
            RefreshTeam();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (player == otherPlayer)
            Destroy(gameObject);
    }

    public override void OnLeftRoom()
    {
        Destroy(gameObject);
    }
}
