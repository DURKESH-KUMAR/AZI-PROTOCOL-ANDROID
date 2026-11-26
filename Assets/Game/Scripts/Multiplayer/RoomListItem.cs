using UnityEngine;
using UnityEngine.UI;
using Photon.Realtime;

public class RoomListItem : MonoBehaviour
{
    [SerializeField] Text roomNameText;
    public RoomInfo info;
    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    public void SetUp(RoomInfo _info)
    {
        info = _info;
        bool full = _info.MaxPlayers > 0 && _info.PlayerCount >= _info.MaxPlayers;
        string count = _info.MaxPlayers > 0 ? _info.PlayerCount + "/" + _info.MaxPlayers : _info.PlayerCount.ToString();
        roomNameText.text = _info.Name + "   <color=#" + (full ? "FF5A4D>FULL " : "9AA4B2>") + count + "</color>";
        roomNameText.supportRichText = true;
        if (button != null)
            button.interactable = !full;
    }

    // Name kept: bound to the Button in RoomListItem.prefab.
    public void onClick()
    {
        if (info != null && Launcher.instance != null)
            Launcher.instance.JoinRoom(info);
    }
}
