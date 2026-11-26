using UnityEngine;

/// <summary>
/// Third-person camera for the LOCAL player. Lives inside the Player prefab
/// (Player > CameraManager > CameraPivot > Main Camera). All references are resolved
/// from this player's own hierarchy - never FindObjectOfType, which would grab another
/// player's components in multiplayer. Driven by PlayerManager.LateUpdate.
/// Adds weapon recoil, damage shake and a sprint FOV kick for game feel.
/// </summary>
public class CameraManager : MonoBehaviour
{
    private InputManager inputManager;
    private PlayerMovement playerMovement;

    [Header("Camera References")]
    public Transform cameraTransform;   // The camera
    public Transform cameraPivot;       // The pivot point (child of this object)
    public Transform playerTransform;   // The player target

    private Vector3 cameraFollowVelocity = Vector3.zero;

    [Header("Follow Settings")]
    public float cameraFollowSpeed = 0.3f;

    [Header("Rotation Settings")]
    public float camLookSpeed = 2f;
    public float camPivotSpeed = 2f;
    public float lookAngle;   // Yaw
    public float pivotAngle;  // Pitch
    public float minPivotAngle = -30f;
    public float maxPivotAngle = 30f;

    [Header("Camera Collision")]
    public LayerMask collisionLayer;
    private float defaultPosition;
    public float cameraCollisionOffset = 0.2f;
    public float minCollisionOffset = 0.2f;
    public float cameraCollisionRadius = 0.2f;
    [Tooltip("How quickly the camera moves in/out when blocked (frame-rate independent).")]
    public float collisionSharpness = 14f;
    private Vector3 cameraVectorPosition;

    [Header("Scope")]
    public GameObject scopeCanvas;
    public GameObject playerUI;
    public Camera mainCamera;
    public float scopedFOV = 10f;
    private bool isScoped;
    private float originalFOV = 60f;

    [Header("Feel")]
    [Tooltip("Extra field of view while sprinting.")]
    public float sprintFovBoost = 8f;
    public float fovSharpness = 8f;
    [Tooltip("How fast recoil returns to rest.")]
    public float recoilRecovery = 9f;
    public float maxShakeAngle = 2.2f;
    public float shakeDecay = 1.6f;

    float currentFov;
    float recoilPitch, recoilYaw;
    float shakeTrauma;
    bool detached;

    void Awake()
    {
        PlayerManager player = GetComponentInParent<PlayerManager>();
        if (player != null)
        {
            playerTransform = player.transform;
            inputManager = player.GetComponent<InputManager>();
            playerMovement = player.GetComponent<PlayerMovement>();
        }
        else
        {
            Debug.LogError("CameraManager must be a child of the Player (PlayerManager) object.");
        }

        if (mainCamera == null)
            mainCamera = GetComponentInChildren<Camera>(true);

        if (mainCamera != null)
        {
            cameraTransform = mainCamera.transform;
            originalFOV = mainCamera.fieldOfView;
        }
        else
        {
            Debug.LogError("No Camera found under CameraManager.");
        }

        if (cameraTransform != null)
            defaultPosition = cameraTransform.localPosition.z;

        if (playerTransform != null)
            lookAngle = playerTransform.eulerAngles.y;

        currentFov = originalFOV;

        // The prefab had "Nothing" here, so the camera went through walls and buildings.
        if (collisionLayer.value == 0)
            collisionLayer = WorldLayers.Mask;
        if (playerTransform != null)
            collisionLayer &= ~(1 << playerTransform.gameObject.layer);
    }

    void Start()
    {
        // Initial scope/UI state (only applied again when it changes).
        ApplyScope(false);
    }

    public void HandleAllCameraMovement()
    {
        if (detached || playerTransform == null || cameraTransform == null)
            return;

        FollowTarget();
        RotateCamera();
        CameraCollision();
        isPlayerScoped();
        UpdateFov();
    }

    private void FollowTarget()
    {
        transform.position = Vector3.SmoothDamp(
            transform.position,
            playerTransform.position,
            ref cameraFollowVelocity,
            cameraFollowSpeed);
    }

    private void RotateCamera()
    {
        if (inputManager == null) return;

        // Horizontal (Yaw)
        lookAngle += inputManager.cameraInputX * camLookSpeed;

        // Vertical (Pitch) - inverted Y
        pivotAngle -= inputManager.cameraInputY * camPivotSpeed;
        pivotAngle = Mathf.Clamp(pivotAngle, minPivotAngle, maxPivotAngle);

        float dt = Time.deltaTime;
        float recover = 1f - Mathf.Exp(-recoilRecovery * dt);
        recoilPitch = Mathf.Lerp(recoilPitch, 0f, recover);
        recoilYaw = Mathf.Lerp(recoilYaw, 0f, recover);

        // Trauma-based shake (squared so small hits are subtle and big ones punchy).
        shakeTrauma = Mathf.Max(0f, shakeTrauma - shakeDecay * dt);
        float shake = shakeTrauma * shakeTrauma * maxShakeAngle;
        float time = Time.time * 25f;
        float shakePitch = shake * (Mathf.PerlinNoise(time, 0.1f) * 2f - 1f);
        float shakeYaw = shake * (Mathf.PerlinNoise(0.3f, time) * 2f - 1f);
        float shakeRoll = shake * (Mathf.PerlinNoise(time, time) * 2f - 1f);

        transform.rotation = Quaternion.Euler(0f, lookAngle + recoilYaw + shakeYaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pivotAngle - recoilPitch + shakePitch, 0f, shakeRoll);

        if (playerMovement != null && !playerMovement.isMoving && !playerMovement.isSprinting)
            playerTransform.rotation = Quaternion.Euler(0f, lookAngle, 0f);
    }

    private void CameraCollision()
    {
        float targetPosition = defaultPosition;
        Vector3 direction = (cameraTransform.position - cameraPivot.position).normalized;

        if (Physics.SphereCast(
            cameraPivot.position,
            cameraCollisionRadius,
            direction,
            out RaycastHit hit,
            Mathf.Abs(targetPosition),
            collisionLayer,
            QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.CompareTag("Player"))
            {
                float distance = Vector3.Distance(cameraPivot.position, hit.point);
                targetPosition = -(distance - cameraCollisionOffset);
            }
        }

        if (Mathf.Abs(targetPosition) < minCollisionOffset)
            targetPosition = -minCollisionOffset;

        cameraVectorPosition = cameraTransform.localPosition;
        // Frame-rate independent (the old fixed 0.2 lerp felt different at 30 vs 60 fps).
        cameraVectorPosition.z = Mathf.Lerp(cameraVectorPosition.z, targetPosition, 1f - Mathf.Exp(-collisionSharpness * Time.deltaTime));
        cameraTransform.localPosition = cameraVectorPosition;
    }

    public void isPlayerScoped()
    {
        bool wantScope = inputManager != null && inputManager.scopeInput;
        if (wantScope != isScoped)
            ApplyScope(wantScope);
    }

    private void ApplyScope(bool scoped)
    {
        isScoped = scoped;
        if (scopeCanvas != null) scopeCanvas.SetActive(scoped);
        if (playerUI != null) playerUI.SetActive(!scoped);
        if (mainCamera != null)
        {
            // Scope snaps (like raising a sight); sprint FOV eases in UpdateFov.
            currentFov = scoped ? scopedFOV : originalFOV;
            mainCamera.fieldOfView = currentFov;
        }
    }

    void UpdateFov()
    {
        if (mainCamera == null || isScoped)
            return;
        bool sprinting = playerMovement != null && playerMovement.isSprinting && playerMovement.isMoving;
        float target = originalFOV + (sprinting ? sprintFovBoost : 0f);
        currentFov = Mathf.Lerp(currentFov, target, 1f - Mathf.Exp(-fovSharpness * Time.deltaTime));
        mainCamera.fieldOfView = currentFov;
    }

    public bool IsScoped => isScoped;

    /// <summary>Kick the view up when firing; springs back automatically.</summary>
    public void AddRecoil(float pitch, float yawJitter)
    {
        float scale = isScoped ? 0.35f : 1f;
        recoilPitch = Mathf.Min(recoilPitch + pitch * scale, 6f);
        recoilYaw += Random.Range(-yawJitter, yawJitter) * scale;
    }

    /// <summary>Screen shake, 0..1 (taking damage).</summary>
    public void AddShake(float amount)
    {
        shakeTrauma = Mathf.Clamp01(shakeTrauma + amount);
    }

    /// <summary>
    /// Unparents the camera so it survives the body being destroyed (used by the death camera).
    /// The CameraManager stops driving it.
    /// </summary>
    public Camera DetachCamera()
    {
        if (detached || mainCamera == null)
            return null;
        ApplyScope(false);
        if (playerUI != null)
            playerUI.SetActive(false);
        detached = true;
        mainCamera.transform.SetParent(null, true);
        mainCamera.fieldOfView = originalFOV;
        return mainCamera;
    }
}
