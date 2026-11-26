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
    }

    void Update()
    {
        if (!view.IsMine)
            return;
        inputManager.HandleAllInputs();
    }

    void FixedUpdate()
    {
        if (!view.IsMine)
            return;
        playerMovement.HandleAllMovement();
    }

    void LateUpdate()
    {
        if (!view.IsMine)
            return;
        if (cameraManager != null)
            cameraManager.HandleAllCameraMovement();
        isInteracting = animator.GetBool(IsInteractingHash);
        playerMovement.isJumping = animator.GetBool(AnimatorManager.IsJumpingHash);
        animator.SetBool(IsGroundedHash, playerMovement.isGrounded);
    }
}
