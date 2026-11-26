using System.Collections;
using UnityEngine;
using Photon.Pun;

public class ShootingController : MonoBehaviour
{
    Animator animator;
    InputManager inputManager;
    PlayerMovement playerMovement;

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

    PhotonView view;
    Coroutine reloadRoutine;
    bool reloadUsedShootTrigger;

    static readonly int ShootHash = Animator.StringToHash("Shoot");
    static readonly int ShootingMovementHash = Animator.StringToHash("ShootingMovement");
    static readonly int ShootWalkHash = Animator.StringToHash("ShootWalk");
    static readonly int ReloadHash = Animator.StringToHash("Reload");
    static readonly int ShootReloadHash = Animator.StringToHash("ShootReload");

    /// <summary>Team of this player (live from Photon properties).</summary>
    public int playerTeam => playerMovement != null ? playerMovement.playerTeam : 0;

    void Awake()
    {
        view = GetComponent<PhotonView>();
        animator = GetComponent<Animator>();
        inputManager = GetComponent<InputManager>();
        playerMovement = GetComponent<PlayerMovement>();
        currentAmmo = maxAmmo;
    }

    void OnDisable()
    {
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
        if (!view.IsMine)
            return;

        // One-shot input: always consume it so a single press can't trigger reloads forever.
        bool reloadPressed = inputManager.ConsumeReloadInput();

        if (isReloading || playerMovement.isSprinting)
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
        bool hitEnemy = false;
        Vector3 hitPoint = Vector3.zero;
        Vector3 hitNormal = Vector3.up;

        if (Physics.Raycast(firePoint.position, firePoint.forward, out RaycastHit hit, fireRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // Collider may be on a child bone/mesh, so search upwards.
            PlayerMovement target = hit.collider.GetComponentInParent<PlayerMovement>();
            if (target != null && target != playerMovement && target.playerTeam != playerTeam)
            {
                target.ApplyDamage(fireDamage);
                hitEnemy = true;
                hitPoint = hit.point;
                hitNormal = hit.normal;
            }
        }

        // Local feedback immediately, everyone else via RPC (they previously never saw/heard shots).
        PlayShotEffects(hitEnemy, hitPoint, hitNormal);
        view.RPC(nameof(RPC_Shoot), RpcTarget.Others, hitEnemy, hitPoint, hitNormal);
    }

    [PunRPC]
    void RPC_Shoot(bool hitEnemy, Vector3 hitPoint, Vector3 hitNormal)
    {
        PlayShotEffects(hitEnemy, hitPoint, hitNormal);
    }

    void PlayShotEffects(bool hitEnemy, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();
        if (soundAudioSource != null && shootingSoundClip != null)
            soundAudioSource.PlayOneShot(shootingSoundClip);

        if (hitEnemy && bloodEffect != null)
        {
            ParticleSystem blood = Instantiate(bloodEffect, hitPoint, Quaternion.LookRotation(hitNormal));
            ParticleSystem.MainModule main = blood.main;
            // Destroy after the last particle can have died, so the effect is always cleaned up.
            Destroy(blood.gameObject, main.duration + main.startLifetime.constantMax);
        }
    }

    private void Reload()
    {
        if (isReloading || currentAmmo >= maxAmmo)
            return;

        reloadUsedShootTrigger = isShootingInput && isWalking;
        animator.SetTrigger(reloadUsedShootTrigger ? ShootReloadHash : ReloadHash);
        isReloading = true;
        if (soundAudioSource != null && reloadingSoundClip != null)
            soundAudioSource.PlayOneShot(reloadingSoundClip);
        reloadRoutine = StartCoroutine(FinishReloadingAfterDelay());
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
