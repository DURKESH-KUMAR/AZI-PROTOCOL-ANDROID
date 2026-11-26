using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;


public class RoomManager : MonoBehaviourPunCallbacks
{
    public static RoomManager instance;

    // Resources path. Forward slash always works; Path.Combine gives '\' on Windows.
    const string PlayerControllerManagerPrefab = "PhotonPrefabs/PlayerControllerManager";



    void Awake()
    {
        if (instance)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (GameScenes.IsMatch(scene) && PhotonNetwork.InRoom)
        {
            PhotonNetwork.Instantiate(PlayerControllerManagerPrefab, Vector3.zero, Quaternion.identity);
        }
    }
}
