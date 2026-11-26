using UnityEngine;

/// <summary>
/// Reads joystick + UI button input. Driven once per frame by PlayerManager.Update
/// (local player only) - it has no Update of its own so input is never processed twice.
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("References")]
    public PlayerMovement playerMovement;      // Player movement logic
    public AnimatorManager animatorManager;    // Handles animation changes
    public FixedJoystick movementJoystick;     // Joystick for movement
    public FixedJoystick cameraJoystick;       // Joystick for camera control

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
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (animatorManager == null) animatorManager = GetComponent<AnimatorManager>();
    }

    // ===================== INPUT HANDLERS =====================

    public void HandleAllInputs()
    {
        HandleMovementInput();
        HandleSprintInput();
        HandleJumpInput();
        HandleCameraInput();
    }

    private void HandleMovementInput()
    {
        if (movementJoystick != null)
        {
            horizontalInput = movementJoystick.Horizontal;
            verticalInput = movementJoystick.Vertical;
        }
        else
        {
            horizontalInput = 0f;
            verticalInput = 0f;
        }

        movementAmount = Mathf.Clamp01(Mathf.Abs(horizontalInput) + Mathf.Abs(verticalInput));
    }

    private void HandleSprintInput()
    {
        playerMovement.isSprinting = sprintInput && movementAmount > 0.5f;
        // Animate after sprint state is known for THIS frame (only sprint-animate when actually sprinting).
        animatorManager.ChangeAnimatorValues(horizontalInput, movementAmount, playerMovement.isSprinting);
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
        if (cameraJoystick == null)
        {
            cameraInputX = cameraInputY = 0f;
            return;
        }
        cameraInputX = cameraJoystick.Horizontal * lookSensitivity;
        cameraInputY = cameraJoystick.Vertical * lookSensitivity;
    }

    /// <summary>Reload is a one-shot press: returns true once, then clears the flag.</summary>
    public bool ConsumeReloadInput()
    {
        bool pressed = reloadInput;
        reloadInput = false;
        return pressed;
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
