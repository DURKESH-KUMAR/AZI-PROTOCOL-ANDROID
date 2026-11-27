using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class PlayerMovement : MonoBehaviour
{
    [Header("Player Health")]
    [SerializeField] float maxHealth = 150f;
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
    ShootingController shootingController;
    CameraManager cameraManager;
    Vector3 moveDirection;
    Transform cameragameObject;
    Rigidbody playerRigidbody;
    Collider[] colliders;

    [Header("Falling and Landing")]
    public float inAirTimer;
    public float leapingVelocity;
    public float fallingVelocity;
    public float rayCastHeightOffset = 0.5f;
    [Tooltip("How far below the ray origin the ground check reaches.")]
    public float groundCheckDistance = 1f;
    [Tooltip("Layers counted as ground. Include every walkable layer (e.g. Default + Props).")]
    public LayerMask groundLayer;
    [Tooltip("Falling below this height kills the player (out of the map).")]
    public float killPlaneY = -40f;

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
    [Tooltip("Safety net: a jump can never last longer than this, even if the animation event is missed.")]
    public float maxJumpTime = 1.5f;
    float jumpStartTime;

    [Header("Death")]
    [Tooltip("Seconds the body stays on the ground before it is removed.")]
    public float corpseTime = 2.2f;

    PhotonView view;

    static readonly int FallingHash = Animator.StringToHash("Falling");
    static readonly int LandingHash = Animator.StringToHash("Landing");
    static readonly int JumpHash = Animator.StringToHash("Jump");

    // Victim-side hit validation: last accepted hit time per attacker.
    readonly Dictionary<int, float> lastHitFrom = new Dictionary<int, float>();

    /// <summary>Team of this player's owner, read live from Photon properties (0 = none).</summary>
    public int playerTeam => PlayerTeams.GetTeam(view != null ? view.Owner : null);
    public int OwnerActor => view != null && view.Owner != null ? view.Owner.ActorNumber : -1;
    public string OwnerName => view != null && view.Owner != null ? view.Owner.NickName : "";
    public bool IsMine => view != null && view.IsMine;
    public bool IsDead => isDead;
    public float MaxHealth => maxHealth;
    public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public bool IsSpawnProtected => Time.time < spawnTime + spawnProtectionTime;
    public float SpawnProtectionRemaining => Mathf.Max(0f, spawnTime + spawnProtectionTime - Time.time);
    public ShootingController Shooting => shootingController;
    /// <summary>Time this player last fired (for the radar: enemies show up when they shoot).</summary>
    public float LastShotTime => shootingController != null ? shootingController.LastShotTime : -100f;

    void Awake()
    {
        currentHealth = maxHealth;
        inputManager = GetComponent<InputManager>();
        animatorManager = GetComponent<AnimatorManager>();
        playerManager = GetComponent<PlayerManager>();
        shootingController = GetComponent<ShootingController>();
        playerRigidbody = GetComponent<Rigidbody>();
        view = GetComponent<PhotonView>();
        cameraManager = GetComponentInChildren<CameraManager>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        // Always include the level's ground layers (never the player's own layer), whatever
        // the prefab says, so the ground check can't silently miss the terrain again.
        groundLayer = (groundLayer | WorldLayers.Mask) & ~(1 << gameObject.layer);

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

        // Footsteps for every body (remote ones too), so you can hear enemies around you.
        if (GetComponent<FootstepAudio>() == null)
            gameObject.AddComponent<FootstepAudio>();
    }

    void OnEnable() => PlayerRegistry.Register(this);

    void OnDisable() => PlayerRegistry.Unregister(this);

    void Start()
    {
        spawnTime = Time.time;
        if (!view.IsMine)
        {
            Destroy(playerRigidbody);
            Destroy(playerUI);
        }
        else
        {
            PlayerRegistry.SetLocal(this);
        }
    }

    public void HandleAllMovement()
    {
        if (isDead)
            return;

        if (transform.position.y < killPlaneY)
        {
            Die(-1); // fell out of the map
            return;
        }

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
                animatorManager.PlayTargetAnim(FallingHash, true);

            inAirTimer += Time.deltaTime;
            // One combined force call per physics step instead of two.
            playerRigidbody.AddForce(transform.forward * leapingVelocity + Vector3.down * (fallingVelocity * inAirTimer));
        }

        if (Physics.SphereCast(rayCastOrigin, 0.2f, Vector3.down, out RaycastHit hit,
                groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            if (!isGrounded && !playerManager.isInteracting)
                animatorManager.PlayTargetAnim(LandingHash, true);

            targetPosition.y = hit.point.y;
            inAirTimer = 0f;
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }

        // FIX (stuck jump): isJumping was only cleared by the jump animation's exit event.
        // If that state was interrupted the player could never move or jump again.
        if (isJumping)
        {
            float jumpAge = Time.time - jumpStartTime;
            bool landed = isGrounded && jumpAge > 0.25f && playerRigidbody.linearVelocity.y <= 0.05f;
            if (landed || jumpAge > maxJumpTime)
                SetIsJumping(false);
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
        if (!isGrounded || isJumping || isDead)
            return;

        animatorManager.animator.SetBool(AnimatorManager.IsJumpingHash, true);
        animatorManager.PlayTargetAnim(JumpHash, false);
        float jumpingVelocity = Mathf.Sqrt(-2f * gravityIntensity * jumpHeight);
        Vector3 playerVelocity = moveDirection;
        playerVelocity.y = jumpingVelocity;
        playerRigidbody.linearVelocity = playerVelocity;
        isJumping = true;
        jumpStartTime = Time.time;
    }

    public void SetIsJumping(bool isJumping)
    {
        this.isJumping = isJumping;
        if (!isJumping && animatorManager != null && animatorManager.animator != null)
            animatorManager.animator.SetBool(AnimatorManager.IsJumpingHash, false); // PlayerManager reads it back each frame
    }

    // ===================================================================== DAMAGE

    /// <summary>Called by the SHOOTER's client. Only the victim's owner applies (and validates) it.</summary>
    public void ApplyDamage(float damageValue, int attackerActor, Vector3 attackerPosition)
    {
        if (view.Owner == null)
            return;
        view.RPC(nameof(RPC_TakeDamage), view.Owner, damageValue, attackerActor, attackerPosition);
    }

    [PunRPC]
    void RPC_TakeDamage(float damage, int attackerActor, Vector3 attackerPosition, PhotonMessageInfo info)
    {
        if (!view.IsMine || isDead || MatchState.IsOver)
            return;
        if (IsSpawnProtected)
            return;
        if (!IsHitPlausible(ref damage, attackerActor, attackerPosition, info))
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (healthbarSlider != null)
            healthbarSlider.value = currentHealth;
        view.RPC(nameof(RPC_SyncHealth), RpcTarget.Others, currentHealth);

        MatchEvents.RaiseLocalDamaged(damage, attackerPosition);
        if (cameraManager != null)
            cameraManager.AddShake(Mathf.Clamp01(damage / 40f));

        if (currentHealth <= 0f)
            Die(attackerActor);
    }

    /// <summary>
    /// Anti-cheat (victim side): the shooter's client decides the hit, so check that it is
    /// physically possible using the attacker's body and weapon stats as THIS client sees them.
    /// Rejects: spoofed attacker, friendly fire, dead attacker, impossible range,
    /// fire-rate hacks; clamps inflated damage.
    /// </summary>
    bool IsHitPlausible(ref float damage, int attackerActor, Vector3 attackerPosition, PhotonMessageInfo info)
    {
        if (info.Sender == null || info.Sender.ActorNumber != attackerActor)
            return false;
        if (damage <= 0f)
            return false;

        int attackerTeam = PlayerTeams.GetTeam(info.Sender);
        if (attackerTeam != 0 && attackerTeam == playerTeam)
            return false;

        PlayerMovement attacker = PlayerRegistry.FindByActor(attackerActor);
        if (attacker == null)
            return true; // body not spawned here yet (join race) - accept, clamp below is skipped
        if (attacker.IsDead)
            return false;

        ShootingController weapon = attacker.Shooting;
        if (weapon != null)
        {
            damage = Mathf.Min(damage, weapon.fireDamage);
            float maxDistance = weapon.fireRange + 8f;
            if ((attacker.transform.position - transform.position).sqrMagnitude > maxDistance * maxDistance)
                return false;

            float minInterval = 0.6f / Mathf.Max(0.01f, weapon.fireRate); // 40% tolerance for network jitter
            if (lastHitFrom.TryGetValue(attackerActor, out float last) && Time.time - last < minInterval)
                return false;
        }
        lastHitFrom[attackerActor] = Time.time;
        return true;
    }

    [PunRPC]
    void RPC_SyncHealth(float health)
    {
        currentHealth = health;
    }

    void Die(int killerActor)
    {
        if (isDead)
            return;
        // Guard: several damage RPCs can arrive in the same frame before the destroy takes effect.
        isDead = true;
        currentHealth = 0f;

        int killer = killerActor == OwnerActor ? -1 : killerActor;
        if (ScoreBoard.instance != null)
            ScoreBoard.instance.ReportDeath(playerTeam, OwnerActor, killer);

        view.RPC(nameof(RPC_OnDied), RpcTarget.All);

        // Fallback: if the spawn data didn't resolve our manager, use the local one, so a death
        // can never leave the player without a respawn.
        if (playerControllerManager == null)
            playerControllerManager = PlayerControllerManager.Local;

        string killerName = killer > 0 ? MatchEvents.NameOf(killer) : "";
        float respawn = playerControllerManager != null ? playerControllerManager.RespawnDelay : 3f;
        MatchEvents.RaiseLocalDied(killerName, respawn);
        GameSettings.Vibrate();

        if (playerControllerManager != null)
            playerControllerManager.Die(gameObject, corpseTime);
        else
            Debug.LogError("[PlayerMovement] No PlayerControllerManager for the local player - cannot respawn.");
    }

    /// <summary>Death on every client: no more hits/collisions, body topples over (owner drives it, transform is synced).</summary>
    [PunRPC]
    void RPC_OnDied()
    {
        isDead = true;
        currentHealth = 0f;
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null)
                colliders[i].enabled = false;

        if (!view.IsMine)
            return;

        if (playerUI != null)
            playerUI.SetActive(false);
        if (inputManager != null)
            inputManager.ClearInputs();
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.isKinematic = true;
        }
        StartCoroutine(Topple());
    }

    IEnumerator Topple()
    {
        if (animatorManager != null)
            animatorManager.ChangeAnimatorValues(0f, 0f, false);

        Quaternion start = transform.rotation;
        Vector3 axis = transform.right;
        Quaternion end = Quaternion.AngleAxis(-82f, axis) * start;
        Vector3 pivot = transform.position;
        Vector3 startPos = transform.position;
        const float duration = 0.55f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            k = k * k; // accelerate like a real fall
            transform.rotation = Quaternion.Slerp(start, end, k);
            transform.position = Vector3.Lerp(startPos, pivot + Vector3.down * 0.05f, k);
            yield return null;
        }
        transform.rotation = end;
    }
}
