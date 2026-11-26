using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class PlayerUsernameManager : MonoBehaviour
{
    public const string UsernameKey = "username";
    public const int MaxUsernameLength = 20;

    [SerializeField] private InputField usernameInput;
    [SerializeField] private Text errorMessageText;

    public static string SavedName => PlayerPrefs.GetString(UsernameKey, "");

    void Start()
    {
        if (PlayerPrefs.HasKey(UsernameKey))
        {
            string saved = SavedName;
            if (usernameInput != null)
                usernameInput.text = saved;
            PhotonNetwork.NickName = saved;
        }
    }

    /// <summary>Validates, applies and saves a username. Used by both the uGUI and UI Toolkit menus.</summary>
    public static bool TryApply(string raw, out string error)
    {
        string username = (raw ?? "").Trim();
        if (username.Length == 0 || username.Length > MaxUsernameLength)
        {
            error = "Name must be 1-" + MaxUsernameLength + " characters.";
            return false;
        }
        PhotonNetwork.NickName = username;
        PlayerPrefs.SetString(UsernameKey, username);
        // Android may kill the app without a clean quit; write now so the name isn't lost.
        PlayerPrefs.Save();
        error = "";
        return true;
    }

    // Name kept: bound to the UI in MainMenu.unity.
    public void playerUsernameInputValueChanged()
    {
        if (TryApply(usernameInput.text, out string error))
        {
            errorMessageText.text = "";
            MenuManager.instance.OpenMenu("TitleMenu");
        }
        else
        {
            errorMessageText.text = error;
        }
    }
}
