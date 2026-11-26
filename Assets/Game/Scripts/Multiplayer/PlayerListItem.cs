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
        Refresh();
    }

    void Refresh()
    {
        if (player == null)
            return;

        int team = PlayerTeams.GetTeam(player);
        Color color = Teams.Color(team);

        string name = player.NickName;
        if (player.IsLocal)
            name += "  (you)";
        if (player.IsMasterClient)
            name += "  [HOST]";
        playerUsername.text = name;
        playerUsername.color = player.IsLocal ? GameUI.Accent : Color.white;

        bool ready = player.IsMasterClient || GameProps.GetBool(player.CustomProperties, GameProps.Ready);
        teamText.text = (team > 0 ? Teams.Name(team) : "-") + (ready ? "  READY" : "  ...");
        teamText.color = color;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer == player)
            Refresh();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Refresh();
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
