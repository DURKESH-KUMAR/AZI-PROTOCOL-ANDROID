using UnityEngine;

/// <summary>
/// Third-person camera for the LOCAL player. Lives inside the Player prefab
/// (Player > CameraManager > CameraPivot > Main Camera). All references are resolved
/// from this player's own hierarchy - never FindObjectOfType, which would grab another
/// player's components in multiplayer. Driven by PlayerManager.LateUpdate.
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
    private Vector3 cameraVectorPosition;

    [Header("Scope")]
    public GameObject scopeCanvas;
    public GameObject playerUI;
    public Camera mainCamera;
    public float scopedFOV = 10f;
    private bool isScoped;
    private float originalFOV = 60f;

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
    }

    void Start()
    {
        // Initial scope/UI state (only applied again when it changes).
        ApplyScope(false);
    }

    public void HandleAllCameraMovement()
    {
        if (playerTransform == null || cameraTransform == null)
            return;

        FollowTarget();
        RotateCamera();
        CameraCollision();
        isPlayerScoped();
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

        transform.rotation = Quaternion.Euler(0f, lookAngle, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pivotAngle, 0f, 0f);

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
        cameraVectorPosition.z = Mathf.Lerp(cameraTransform.localPosition.z, targetPosition, 0.2f);
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
        if (mainCamera != null) mainCamera.fieldOfView = scoped ? scopedFOV : originalFOV;
    }
}
