using System.Collections;
using UnityEngine;
using Photon.Pun;

public class ShootingController : MonoBehaviour
{
    Animator animator;
    InputManager inputManager;
    PlayerMovement playerMovement;
    CameraManager cameraManager;
    Camera aimCamera;

    [Header("Shooting Var")]
    public Transform firePoint;
    [Tooltip("Shots per second (5 = one shot every 0.2s).")]
    public float fireRate = 5f;
    public float fireRange = 100f;
    public float fireDamage = 15;
    private float nextFireTime = 0f;

    [Header("Reloading")]
    public int maxAmmo = 30;
    private int currentAmmo;
    public float reloadTime = 1.5f;
    float reloadStartTime;

    [Header("Shooting Flags")]
    public bool isShooting;
    public bool isWalking;
    public bool isShootingInput;
    public bool isReloading = false;
    public bool isScopeInput;

    [Header("Sound Effects")]
    public AudioSource soundAudioSource;
    public AudioClip shootingSoundClip;
    public AudioClip reloadingSoundClip;

    [Header("Particle Effects")]
    public ParticleSystem muzzleFlash;
    public ParticleSystem bloodEffect;

    [Header("Feel")]
    public float recoilPitch = 0.9f;
    public float recoilYaw = 0.35f;
    public Color tracerColor = new Color(1f, 0.85f, 0.45f, 0.9f);
    public float tracerWidth = 0.025f;
    public float tracerTime = 0.06f;

    PhotonView view;
    Coroutine reloadRoutine;
    bool reloadUsedShootTrigger;
    LineRenderer tracer;
    float tracerHideAt;

    // Last bools written to the Animator (-1 = unknown), so unchanged values aren't re-sent every frame.
    int lastShootAnimState = -1;

    static readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    static Material tracerMaterial;

    static readonly int ShootHash = Animator.StringToHash("Shoot");
    static readonly int ShootingMovementHash = Animator.StringToHash("ShootingMovement");
    static readonly int ShootWalkHash = Animator.StringToHash("ShootWalk");
    static readonly int ReloadHash = Animator.StringToHash("Reload");
    static readonly int ShootReloadHash = Animator.StringToHash("ShootReload");

    /// <summary>Team of this player (live from Photon properties).</summary>
    public int playerTeam => playerMovement != null ? playerMovement.playerTeam : 0;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public bool IsReloading => isReloading;
    public float ReloadProgress => isReloading && reloadTime > 0f ? Mathf.Clamp01((Time.time - reloadStartTime) / reloadTime) : 0f;
    public float LastShotTime { get; private set; } = -100f;

    void Awake()
    {
        view = GetComponent<PhotonView>();
        animator = GetComponent<Animator>();
        inputManager = GetComponent<InputManager>();
        playerMovement = GetComponent<PlayerMovement>();
        cameraManager = GetComponentInChildren<CameraManager>(true);
        aimCamera = GetComponentInChildren<Camera>(true);
        currentAmmo = maxAmmo;
        CreateTracer();
    }

    void OnDisable()
    {
        lastShootAnimState = -1;
        // Don't leave a pending reload running on a disabled/destroyed player.
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
            isReloading = false;
        }
    }

    void Update()
    {
        if (tracer != null && tracer.enabled && Time.time >= tracerHideAt)
            tracer.enabled = false;

        if (!view.IsMine)
            return;

        // One-shot input: always consume it so a single press can't trigger reloads forever.
        bool reloadPressed = inputManager.ConsumeReloadInput();

        if (playerMovement.IsDead || isReloading || playerMovement.isSprinting || !MatchState.CombatAllowed || PlayerManager.InputBlocked)
        {
            SetShootAnim(false, false, false);
            isShooting = false;
            return;
        }

        isWalking = playerMovement.isMoving;
        isShootingInput = inputManager.fireInput;
        isScopeInput = inputManager.scopeInput;

        if (isShootingInput)
        {
            TryShoot();
            if (isWalking)
                SetShootAnim(false, true, true);
            else
                SetShootAnim(true, false, false);
            isShooting = true;
        }
        else if (isScopeInput)
        {
            SetShootAnim(false, true, false);
            isShooting = false;
        }
        else
        {
            SetShootAnim(false, false, false);
            isShooting = false;
        }

        if (reloadPressed && currentAmmo < maxAmmo)
            Reload();
    }

    void SetShootAnim(bool shoot, bool shootingMovement, bool shootWalk)
    {
        int state = (shoot ? 1 : 0) | (shootingMovement ? 2 : 0) | (shootWalk ? 4 : 0);
        if (state == lastShootAnimState)
            return;
        lastShootAnimState = state;

        animator.SetBool(ShootHash, shoot);
        animator.SetBool(ShootingMovementHash, shootingMovement);
        animator.SetBool(ShootWalkHash, shootWalk);
    }

    void TryShoot()
    {
        if (Time.time < nextFireTime)
            return;
        nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);
        Shoot();
    }

    private void Shoot()
    {
        if (currentAmmo <= 0)
        {
            Reload();
            return;
        }

        currentAmmo--;
        LastShotTime = Time.time;

        bool hitEnemy = false;
        Vector3 hitNormal = Vector3.up;
        Ray ray = AimRay();
        Vector3 endPoint = ray.origin + ray.direction * fireRange;

        if (FindHit(ray, out RaycastHit hit))
        {
            endPoint = hit.point;
            hitNormal = hit.normal;

            // Collider may be on a child bone/mesh, so search upwards.
            PlayerMovement target = hit.collider.GetComponentInParent<PlayerMovement>();
            if (target != null && target != playerMovement && !target.IsDead && (playerTeam == 0 || target.playerTeam != playerTeam))
            {
                if (!target.IsSpawnProtected)
                {
                    target.ApplyDamage(fireDamage, PhotonNetwork.LocalPlayer.ActorNumber, transform.position);
                    MatchEvents.RaiseLocalHit();
                }
                hitEnemy = true;
            }
        }

        if (cameraManager != null)
            cameraManager.AddRecoil(recoilPitch, recoilYaw);

        // Local feedback immediately, everyone else via RPC.
        PlayShotEffects(hitEnemy, endPoint, hitNormal);
        view.RPC(nameof(RPC_Shoot), RpcTarget.Others, hitEnemy, endPoint, hitNormal);
    }

    /// <summary>
    /// Aim through the centre of the screen (where the crosshair is), not along the gun barrel:
    /// in third person the barrel and the view point in different directions.
    /// The ray starts level with the player so walls BEHIND the player (between it and the camera) are ignored.
    /// </summary>
    Ray AimRay()
    {
        if (aimCamera == null || !aimCamera.isActiveAndEnabled)
            return new Ray(firePoint.position, firePoint.forward);

        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float skip = Vector3.Dot(firePoint.position - ray.origin, ray.direction);
        if (skip > 0f)
            ray.origin += ray.direction * skip;
        return ray;
    }

    /// <summary>Nearest hit that is not part of the shooter's own body (fixes shots blocked by yourself).</summary>
    bool FindHit(Ray ray, out RaycastHit best)
    {
        int count = Physics.RaycastNonAlloc(ray, hitBuffer, fireRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        best = default;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.distance >= bestDistance || h.collider.transform.IsChildOf(transform))
                continue;
            best = h;
            bestDistance = h.distance;
        }
        return bestDistance < float.MaxValue;
    }

    [PunRPC]
    void RPC_Shoot(bool hitEnemy, Vector3 hitPoint, Vector3 hitNormal)
    {
        LastShotTime = Time.time;
        PlayShotEffects(hitEnemy, hitPoint, hitNormal);
    }

    void PlayShotEffects(bool hitEnemy, Vector3 endPoint, Vector3 hitNormal)
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();
        if (soundAudioSource != null && shootingSoundClip != null)
            soundAudioSource.PlayOneShot(shootingSoundClip);

        ShowTracer(endPoint);

        if (hitEnemy && bloodEffect != null)
            EffectPool.Play(bloodEffect, endPoint, Quaternion.LookRotation(hitNormal));
    }

    void CreateTracer()
    {
        if (tracerMaterial == null)
        {
            // UI/Default is in "Always Included Shaders", so this never renders pink on device.
            Shader shader = Shader.Find("UI/Default");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader != null)
                tracerMaterial = new Material(shader);
        }
        if (tracerMaterial == null)
            return;

        GameObject go = new GameObject("Tracer");
        go.transform.SetParent(transform, false);
        tracer = go.AddComponent<LineRenderer>();
        tracer.sharedMaterial = tracerMaterial;
        tracer.useWorldSpace = true;
        tracer.positionCount = 2;
        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracer.receiveShadows = false;
        tracer.numCapVertices = 2;
        tracer.startWidth = tracerWidth;
        tracer.endWidth = tracerWidth * 0.4f;
        Color end = tracerColor;
        end.a = 0f;
        tracer.startColor = tracerColor;
        tracer.endColor = end;
        tracer.enabled = false;
    }

    void ShowTracer(Vector3 endPoint)
    {
        if (tracer == null || firePoint == null)
            return;
        tracer.SetPosition(0, firePoint.position);
        tracer.SetPosition(1, endPoint);
        tracer.enabled = true;
        tracerHideAt = Time.time + tracerTime;
    }

    private void Reload()
    {
        if (isReloading || currentAmmo >= maxAmmo)
            return;

        reloadUsedShootTrigger = isShootingInput && isWalking;
        animator.SetTrigger(reloadUsedShootTrigger ? ShootReloadHash : ReloadHash);
        isReloading = true;
        reloadStartTime = Time.time;
        if (soundAudioSource != null && reloadingSoundClip != null)
            soundAudioSource.PlayOneShot(reloadingSoundClip);
        reloadRoutine = StartCoroutine(FinishReloadingAfterDelay());
    }

    /// <summary>For a HUD reload button.</summary>
    public void RequestReload()
    {
        if (view.IsMine && !playerMovement.IsDead)
            Reload();
    }

    IEnumerator FinishReloadingAfterDelay()
    {
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
        isReloading = false;
        reloadRoutine = null;
        animator.ResetTrigger(reloadUsedShootTrigger ? ShootReloadHash : ReloadHash);
    }
}
