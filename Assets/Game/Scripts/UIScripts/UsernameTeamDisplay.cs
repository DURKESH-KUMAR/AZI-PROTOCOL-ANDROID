using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Name + team label above REMOTE players. Class name now matches the file name
/// (UsernameTeamDisplay.cs) so Unity can load it as a component.
/// </summary>
public class UsernameTeamDisplay : MonoBehaviourPunCallbacks
{
    public Text usernameText;
    public Text teamText;
    public PhotonView view;

    void Start()
    {
        if (view == null)
            view = GetComponentInParent<PhotonView>();
        if (view == null)
            return;

        if (view.IsMine)
        {
            gameObject.SetActive(false);
            return;
        }

        usernameText.text = view.Owner.NickName;
        RefreshTeam();
    }

    void RefreshTeam()
    {
        int team = PlayerTeams.GetTeam(view.Owner);
        teamText.text = team > 0 ? "Team " + team : "";
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (view != null && targetPlayer == view.Owner && changedProps.ContainsKey(PlayerTeams.TeamKey))
            RefreshTeam();
    }
}
