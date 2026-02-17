using UnityEngine;
using Photon.Pun;

public class InputManager : MonoBehaviour
{
    [Header("References")]
    public PlayerMovement playerMovement;      // Player movement logic
    public AnimatorManager animatorManager;    // Handles animation changes
    public FixedJoystick movementJoystick;     // Joystick for movement
    public FixedJoystick cameraJoystick;       // Joystick for camera control

    private PhotonView photonView;

    [Header("Movement Values")]
    public float verticalInput;
    public float horizontalInput;
    public float movementAmount;

    [Header("Camera Values")]
    public float cameraInputX;
    public float cameraInputY;
    public float lookSensitivity = 0.15f;

    [Header("Input Flags")]
    public bool sprintInput;
    public bool jumpInput;
    public bool fireInput;
    public bool reloadInput;
    public bool scopeInput;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
    }

    void Update()
    {
        // Only process input for the local player
        if (photonView != null && !photonView.IsMine)
            return;

        HandleAllInputs();
        HandleCameraInput();
    }

    // ===================== INPUT HANDLERS =====================

    public void HandleAllInputs()
    {
        HandleMovementInput();
        HandleSprintInput();
        HandleJumpInput();
    }

    private void HandleMovementInput()
    {
        horizontalInput = movementJoystick.Horizontal;
        verticalInput = movementJoystick.Vertical;

        movementAmount = Mathf.Clamp01(
            Mathf.Abs(horizontalInput) + Mathf.Abs(verticalInput)
        );

        animatorManager.ChangeAnimatorValues(horizontalInput, movementAmount, sprintInput);
    }

    private void HandleSprintInput()
    {
        if (sprintInput && movementAmount > 0.5f)
            playerMovement.isSprinting = true;
        else
            playerMovement.isSprinting = false;
    }

    private void HandleJumpInput()
    {
        if (jumpInput)
        {
            jumpInput = false;
            playerMovement.HandleJumping();
        }
    }

    private void HandleCameraInput()
    {
        cameraInputX = cameraJoystick.Horizontal * lookSensitivity;
        cameraInputY = cameraJoystick.Vertical * lookSensitivity;
    }

    // ===================== UI BUTTON EVENTS =====================

    public void OnFirePressed()   => fireInput = true;
    public void OnFireReleased()  => fireInput = false;

    public void OnJumpPressed()   => jumpInput = true;

    public void OnReloadPressed() => reloadInput = true;

    public void OnScopePressed()  => scopeInput = true;
    public void OnScopeReleased() => scopeInput = false;

    public void OnSprintPressed()  => sprintInput = true;
    public void OnSprintReleased() => sprintInput = false;
}
