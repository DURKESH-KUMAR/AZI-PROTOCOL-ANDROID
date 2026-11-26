using UnityEngine;
using Photon.Pun;

/// <summary>
/// Single driver for the local player's per-frame logic: input (Update),
/// physics movement (FixedUpdate) and camera (LateUpdate). Remote copies do nothing.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    InputManager inputManager;
    PlayerMovement playerMovement;
    CameraManager cameraManager;
    public bool isInteracting;
    Animator animator;
    PhotonView view;

    static readonly int IsInteractingHash = Animator.StringToHash("isInteracting");
    static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");

    void Awake()
    {
        view = GetComponent<PhotonView>();
        animator = GetComponent<Animator>();
        inputManager = GetComponent<InputManager>();
        playerMovement = GetComponent<PlayerMovement>();
        // This player's own camera rig - NOT FindObjectOfType (would pick another player's).
        cameraManager = GetComponentInChildren<CameraManager>(true);
    }

    void Start()
    {
        if (!view.IsMine && cameraManager != null)
        {
            // Remote players must not own a camera / audio listener on this device.
            Destroy(cameraManager.gameObject);
            cameraManager = null;
        }

        // Remote copies are driven by the network: skip Update/FixedUpdate/LateUpdate entirely.
        if (!view.IsMine)
            enabled = false;
        else
            PlayerControlsUI.Attach(gameObject); // UI Toolkit touch controls replace the prefab's uGUI canvas
    }

    /// <summary>Set by the pause menu: the match keeps running (multiplayer), but this player stops acting.</summary>
    public static bool InputBlocked;

    void Update()
    {
        if (!view.IsMine || playerMovement.IsDead)
            return;
        if (InputBlocked)
        {
            inputManager.ClearInputs();
            return;
        }
        inputManager.HandleAllInputs();
    }

    void FixedUpdate()
    {
        if (!view.IsMine)
            return;
        // With inputs cleared (pause/death) movement resolves to standing still.
        playerMovement.HandleAllMovement();
    }

    void LateUpdate()
    {
        if (!view.IsMine || playerMovement.IsDead)
            return;
        if (cameraManager != null)
            cameraManager.HandleAllCameraMovement();
        isInteracting = animator.GetBool(IsInteractingHash);
        playerMovement.isJumping = animator.GetBool(AnimatorManager.IsJumpingHash);
        animator.SetBool(IsGroundedHash, playerMovement.isGrounded);
    }
}
