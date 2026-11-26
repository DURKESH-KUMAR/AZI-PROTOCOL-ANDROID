using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class PlayerMovement : MonoBehaviour
{
    [Header("Player Health")]
    const float maxHealth = 150f;
    public float currentHealth;
    public Slider healthbarSlider;
    public GameObject playerUI;
    [Tooltip("Seconds after spawning during which the player cannot take damage.")]
    public float spawnProtectionTime = 2f;
    float spawnTime;
    bool isDead;

    [Header("Ref & Physics")]
    PlayerControllerManager playerControllerManager;
    InputManager inputManager;
    PlayerManager playerManager;
    AnimatorManager animatorManager;
    Vector3 moveDirection;
    Transform cameragameObject;
    Rigidbody playerRigidbody;

    [Header("Falling and Landing")]
    public float inAirTimer;
    public float leapingVelocity;
    public float fallingVelocity;
    public float rayCastHeightOffset = 0.5f;
    [Tooltip("How far below the ray origin the ground check reaches.")]
    public float groundCheckDistance = 1f;
    [Tooltip("Layers counted as ground. Include every walkable layer (e.g. Default + Props).")]
    public LayerMask groundLayer;

    [Header("Movement Flags")]
    public bool isMoving;
    public bool isSprinting;
    public bool isGrounded;
    public bool isJumping;

    [Header("Movement Values")]
    public float movementSpeed = 2f;
    public float rotationSpeed = 13f;
    public float sprintingSpeed = 7f;

    [Header("Jump Var")]
    public float jumpHeight = 4f;
    public float gravityIntensity = -15f;

    PhotonView view;

    /// <summary>Team of this player's owner, read live from Photon properties (0 = none).</summary>
    public int playerTeam => PlayerTeams.GetTeam(view != null ? view.Owner : null);

    void Awake()
    {
        currentHealth = maxHealth;
        inputManager = GetComponent<InputManager>();
        animatorManager = GetComponent<AnimatorManager>();
        playerManager = GetComponent<PlayerManager>();
        playerRigidbody = GetComponent<Rigidbody>();
        view = GetComponent<PhotonView>();

        // This player's own camera (every Player prefab has one) - not Camera.main.
        Camera ownCamera = GetComponentInChildren<Camera>(true);
        if (ownCamera != null)
            cameragameObject = ownCamera.transform;

        object[] data = view.InstantiationData;
        if (data != null && data.Length > 0)
        {
            PhotonView managerView = PhotonView.Find((int)data[0]);
            if (managerView != null)
                playerControllerManager = managerView.GetComponent<PlayerControllerManager>();
        }

        if (view.IsMine && healthbarSlider != null)
        {
            healthbarSlider.minValue = 0f;
            healthbarSlider.maxValue = maxHealth;
            healthbarSlider.value = currentHealth;
        }
    }

    void Start()
    {
        spawnTime = Time.time;
        if (!view.IsMine)
        {
            Destroy(playerRigidbody);
            Destroy(playerUI);
        }
    }

    public void HandleAllMovement()
    {
        HandleFallingAndLanding();
        if (playerManager.isInteracting)
            return;

        HandleMovement();
        HandleRotation();
    }

    void HandleMovement()
    {
        if (isJumping || cameragameObject == null)
            return;

        Vector3 forward = cameragameObject.forward;
        forward.y = 0f;
        moveDirection = forward.normalized * inputManager.verticalInput;
        moveDirection += cameragameObject.right * inputManager.horizontalInput;
        moveDirection.y = 0f;
        moveDirection.Normalize();

        isMoving = inputManager.movementAmount > 0f;

        if (isSprinting)
            moveDirection *= sprintingSpeed;
        else if (isMoving)
            moveDirection *= movementSpeed;

        playerRigidbody.linearVelocity = moveDirection;
    }

    void HandleRotation()
    {
        if (isJumping || cameragameObject == null)
            return;

        Vector3 targetDirection = cameragameObject.forward * inputManager.verticalInput;
        targetDirection += cameragameObject.right * inputManager.horizontalInput;
        targetDirection.y = 0f;
        targetDirection.Normalize();

        if (targetDirection == Vector3.zero)
            targetDirection = transform.forward;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    void HandleFallingAndLanding()
    {
        Vector3 rayCastOrigin = transform.position;
        rayCastOrigin.y += rayCastHeightOffset;
        Vector3 targetPosition = transform.position;

        if (!isGrounded && !isJumping)
        {
            if (!playerManager.isInteracting)
                animatorManager.PlayTargetAnim("Falling", true);

            inAirTimer += Time.deltaTime;
            playerRigidbody.AddForce(transform.forward * leapingVelocity);
            playerRigidbody.AddForce(-Vector3.up * fallingVelocity * inAirTimer);
        }

        // FIX: the layer mask was previously passed as maxDistance, so ALL layers were hit.
        if (Physics.SphereCast(rayCastOrigin, 0.2f, Vector3.down, out RaycastHit hit,
                groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            if (!isGrounded && !playerManager.isInteracting)
                animatorManager.PlayTargetAnim("Landing", true);

            targetPosition.y = hit.point.y;
            inAirTimer = 0f;
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }

        if (isGrounded && !isJumping)
        {
            if (playerManager.isInteracting || inputManager.movementAmount > 0f)
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime / 0.1f);
            else
                transform.position = targetPosition;
        }
    }

    public void HandleJumping()
    {
        if (!isGrounded || isJumping)
            return;

        animatorManager.animator.SetBool(AnimatorManager.IsJumpingHash, true);
        animatorManager.PlayTargetAnim("Jump", false);
        float jumpingVelocity = Mathf.Sqrt(-2f * gravityIntensity * jumpHeight);
        Vector3 playerVelocity = moveDirection;
        playerVelocity.y = jumpingVelocity;
        playerRigidbody.linearVelocity = playerVelocity;
        isJumping = true;
    }

    public void SetIsJumping(bool isJumping)
    {
        this.isJumping = isJumping;
    }

    public void ApplyDamage(float damageValue)
    {
        view.RPC(nameof(RPC_TakeDamage), RpcTarget.All, damageValue);
    }

    [PunRPC]
    void RPC_TakeDamage(float damage)
    {
        if (!view.IsMine || isDead)
            return;
        if (Time.time < spawnTime + spawnProtectionTime)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (healthbarSlider != null)
            healthbarSlider.value = currentHealth;

        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        // Guard: several damage RPCs can arrive in the same frame before the destroy takes effect.
        isDead = true;
        if (ScoreBoard.instance != null)
            ScoreBoard.instance.PlayerDied(playerTeam);
        if (playerControllerManager != null)
            playerControllerManager.Die();
    }
}
