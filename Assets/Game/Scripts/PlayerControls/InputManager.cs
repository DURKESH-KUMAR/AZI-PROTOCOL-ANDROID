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

    /// <summary>
    /// UI Toolkit hybrid controls (PlayerControlsUI): left virtual joystick + right drag-to-look.
    /// When set they replace the uGUI FixedJoysticks of the old (disabled) canvas.
    /// </summary>
    public IStickInput moveStick;
    public ILookInput lookDrag;

    [Header("Touch Look (drag)")]
    [Tooltip("Camera turn per panel unit dragged (x CameraManager look speed). 0.09 = ~0.18 deg per unit.")]
    public float dragLookSensitivity = 0.09f;
    [Tooltip("Pushing the stick past this (straight ahead) sprints automatically.")]
    [Range(0.5f, 1f)] public float autoSprintThreshold = 0.92f;

    Camera viewCamera;
    float baseFov = 60f;

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
        viewCamera = GetComponentInChildren<Camera>(true);
        if (viewCamera != null)
            baseFov = viewCamera.fieldOfView;
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
        if (moveStick != null)
        {
            Vector2 v = moveStick.Value;
            horizontalInput = v.x;
            verticalInput = v.y;
        }
        else if (movementJoystick != null)
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
        // Sprint: the sprint toggle, or the stick pushed to the top edge (modern auto-sprint).
        bool autoSprint = moveStick != null && verticalInput >= autoSprintThreshold && Mathf.Abs(horizontalInput) < 0.45f;
        playerMovement.isSprinting = (sprintInput || autoSprint) && movementAmount > 0.5f;
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
        if (lookDrag != null)
        {
            // Drag = exact angle per distance (frame-rate independent). Scaled by the current
            // field of view, so the view moves at the same on-screen speed when zoomed in.
            Vector2 d = lookDrag.ConsumeDelta();
            float scale = dragLookSensitivity * GameSettings.LookSensitivity;
            if (viewCamera != null && viewCamera.fieldOfView < baseFov - 0.5f)
                scale *= Mathf.Clamp(viewCamera.fieldOfView / baseFov, 0.1f, 1f) * (GameSettings.ScopeSensitivity / 0.45f);
            cameraInputX = d.x * scale;
            cameraInputY = d.y * scale;
            return;
        }

        if (cameraJoystick == null)
        {
            cameraInputX = cameraInputY = 0f;
            return;
        }
        // Player-chosen sensitivity, reduced while scoped so zoomed aim isn't twitchy.
        float sensitivity = lookSensitivity * GameSettings.LookSensitivity;
        if (scopeInput)
            sensitivity *= GameSettings.ScopeSensitivity;
        cameraInputX = cameraJoystick.Horizontal * sensitivity;
        cameraInputY = cameraJoystick.Vertical * sensitivity;
    }

    /// <summary>Drop all held inputs (death, pause menu) so nothing stays "pressed".</summary>
    public void ClearInputs()
    {
        verticalInput = horizontalInput = movementAmount = 0f;
        cameraInputX = cameraInputY = 0f;
        if (lookDrag != null)
            lookDrag.ConsumeDelta(); // drop drag gathered while paused/dead
        sprintInput = jumpInput = fireInput = reloadInput = scopeInput = false;
        if (playerMovement != null)
        {
            playerMovement.isSprinting = false;
            playerMovement.isMoving = false;
        }
        if (animatorManager != null)
            animatorManager.ChangeAnimatorValues(0f, 0f, false);
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
    public void ToggleScope() => scopeInput = !scopeInput;

    public void OnSprintPressed()  => sprintInput = true;
    public void OnSprintReleased() => sprintInput = false;
}
