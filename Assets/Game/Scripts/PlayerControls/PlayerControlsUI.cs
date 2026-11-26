using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit version of the Player prefab's screen UI (Resources/UI/Controls.uxml + Controls.uss),
/// replacing the "Health&amp;Crosshair" and "ScopeCanvas" uGUI canvases for the LOCAL player:
/// multi-touch sticks (floating move stick), fire x2, jump, sprint (hold), reload and aim (new),
/// health bar with damage trail, dynamic crosshair (spreads when moving/firing, turns red on
/// enemies) and a scope overlay. Added by PlayerManager; remote players never get it.
/// If the UXML is missing, the original uGUI canvas simply stays in use.
/// </summary>
public class PlayerControlsUI : MonoBehaviour
{
    const float CrosshairGap = 10f;
    const float CrosshairMaxSpread = 22f;

    InputManager input;
    PlayerMovement movement;
    ShootingController gun;
    CameraManager cameraManager;
    Camera aimCamera;

    VisualElement root, controls, scope, crosshair, chLeft, chRight, chUp, chDown, health, healthFill, healthTrail, aim, reload;
    Label healthValue;
    TouchStick moveStick, lookStick;
    TouchButton[] buttons;

    float spread, shownSpread = -1f;
    float shownHealth = -1f, trail = 1f, trailHoldUntil;
    bool shownScoped, shownDead, shownEnemy, shownAimOn, shownReloadOff;
    int shownHp = -1;

    static readonly RaycastHit[] aimHits = new RaycastHit[8];

    public static bool Available => GameUI.Load("Controls") != null;

    /// <summary>Called once for the local player.</summary>
    public static PlayerControlsUI Attach(GameObject player)
    {
        if (!Available)
            return null;
        PlayerControlsUI ui = player.GetComponent<PlayerControlsUI>();
        return ui != null ? ui : player.AddComponent<PlayerControlsUI>();
    }

    void Awake()
    {
        input = GetComponent<InputManager>();
        movement = GetComponent<PlayerMovement>();
        gun = GetComponent<ShootingController>();
        cameraManager = GetComponentInChildren<CameraManager>(true);
        aimCamera = GetComponentInChildren<Camera>(true);

        DisableLegacyCanvas();

        root = GameUI.CreateDocument(transform, "Controls", 40).rootVisualElement;
        controls = root.Q("controls");
        scope = root.Q("scope");
        root.Q("scope-vignette").style.backgroundImage = GameUI.Vignette;
        crosshair = root.Q("crosshair");
        chLeft = root.Q("ch-left");
        chRight = root.Q("ch-right");
        chUp = root.Q("ch-up");
        chDown = root.Q("ch-down");
        health = root.Q("health");
        healthFill = root.Q("health-fill");
        healthTrail = root.Q("health-trail");
        healthValue = root.Q<Label>("health-value");
        aim = root.Q("aim");
        reload = root.Q("reload");

        moveStick = new TouchStick(root.Q("move-zone"), root.Q("move-stick"), root.Q("move-knob"), floating: true);
        lookStick = new TouchStick(root.Q("look-zone"), root.Q("look-stick"), root.Q("look-knob"), floating: false);
        input.moveStick = moveStick;
        input.lookStick = lookStick;

        buttons = new[]
        {
            new TouchButton(root.Q("fire-left"), input.OnFirePressed, input.OnFireReleased),
            new TouchButton(root.Q("fire-right"), input.OnFirePressed, input.OnFireReleased),
            new TouchButton(root.Q("jump"), input.OnJumpPressed),
            new TouchButton(root.Q("sprint"), input.OnSprintPressed, input.OnSprintReleased),
            new TouchButton(reload, input.OnReloadPressed),
            new TouchButton(aim, input.ToggleScope),
        };
    }

    /// <summary>
    /// Turns off the prefab's uGUI controls for this player and stops CameraManager toggling
    /// them on scope (it used to re-enable the canvas when un-scoping).
    /// </summary>
    void DisableLegacyCanvas()
    {
        if (input.movementJoystick != null)
        {
            Canvas canvas = input.movementJoystick.GetComponentInParent<Canvas>(true);
            if (canvas != null)
                canvas.rootCanvas.gameObject.SetActive(false);
        }
        if (cameraManager != null)
        {
            if (cameraManager.scopeCanvas != null)
                cameraManager.scopeCanvas.SetActive(false);
            cameraManager.scopeCanvas = null;
            cameraManager.playerUI = null;
        }
        movement.healthbarSlider = null;
    }

    void OnDestroy()
    {
        if (input != null)
        {
            input.moveStick = null;
            input.lookStick = null;
        }
    }

    void ReleaseAll()
    {
        moveStick.Release();
        lookStick.Release();
        for (int i = 0; i < buttons.Length; i++)
            buttons[i].Release();
    }

    void LateUpdate()
    {
        bool dead = movement.IsDead;
        if (dead != shownDead)
        {
            shownDead = dead;
            controls.SetVisible(!dead);
            if (dead)
                ReleaseAll();
        }
        if (dead)
            return;

        if (PlayerManager.InputBlocked && (moveStick.IsHeld || lookStick.IsHeld))
            ReleaseAll(); // pause menu opened mid-touch

        UpdateScope();
        UpdateHealth();
        UpdateCrosshair();
        UpdateButtons();
    }

    void UpdateScope()
    {
        bool scoped = cameraManager != null && cameraManager.IsScoped;
        if (scoped == shownScoped)
            return;
        shownScoped = scoped;
        scope.EnableInClassList("scope--on", scoped);
        controls.EnableInClassList("controls--scoped", scoped);
    }

    void UpdateButtons()
    {
        bool aimOn = input.scopeInput;
        if (aimOn != shownAimOn)
        {
            shownAimOn = aimOn;
            aim.EnableInClassList("action--on", aimOn);
        }
        bool reloadOff = gun == null || gun.IsReloading || gun.CurrentAmmo >= gun.MaxAmmo;
        if (reloadOff != shownReloadOff)
        {
            shownReloadOff = reloadOff;
            reload.EnableInClassList("action--disabled", reloadOff);
        }
    }

    void UpdateHealth()
    {
        float h = movement.HealthFraction;
        if (h != shownHealth)
        {
            if (h < shownHealth)
                trailHoldUntil = Time.time + 0.45f; // let the white "damage trail" linger, then drain
            else
                trail = h;
            shownHealth = h;
            healthFill.style.width = Length.Percent(h * 100f);
            healthFill.style.backgroundColor = h > 0.6f ? new Color(0.35f, 0.85f, 0.48f)
                : h > 0.3f ? new Color(1f, 0.75f, 0.3f) : Teams.RedColor;
            health.EnableInClassList("health--low", h <= 0.3f);
        }

        int hp = Mathf.CeilToInt(movement.currentHealth);
        if (hp != shownHp)
        {
            shownHp = hp;
            healthValue.text = hp.ToString();
        }

        if (trail > h && Time.time >= trailHoldUntil)
        {
            trail = Mathf.MoveTowards(trail, h, Time.deltaTime * 0.6f);
            healthTrail.style.width = Length.Percent(trail * 100f);
        }
        else if (trail < h)
        {
            trail = h;
            healthTrail.style.width = Length.Percent(trail * 100f);
        }
    }

    void UpdateCrosshair()
    {
        if (shownScoped)
            return;

        // Spread: moving, sprinting, airborne and firing open the crosshair up; it settles when still.
        float target = 0f;
        if (movement.isMoving) target += 0.35f;
        if (movement.isSprinting) target += 0.35f;
        if (!movement.isGrounded) target += 0.4f;
        if (gun != null && gun.isShooting) target += 0.5f;
        spread = Mathf.Lerp(spread, Mathf.Clamp01(target), 1f - Mathf.Exp(-12f * Time.deltaTime));

        float gap = CrosshairGap + spread * CrosshairMaxSpread;
        gap = Mathf.Round(gap * 2f) * 0.5f;
        if (gap != shownSpread)
        {
            shownSpread = gap;
            chLeft.style.left = -gap - 16f;
            chRight.style.left = gap;
            chUp.style.top = -gap - 16f;
            chDown.style.top = gap;
        }

        bool enemy = IsAimingAtEnemy();
        if (enemy != shownEnemy)
        {
            shownEnemy = enemy;
            crosshair.EnableInClassList("crosshair--enemy", enemy);
        }
    }

    /// <summary>Crosshair turns red over an enemy (cheap: 8-hit buffer, only every few frames).</summary>
    bool IsAimingAtEnemy()
    {
        if (aimCamera == null || gun == null || (Time.frameCount & 3) != 0)
            return shownEnemy;
        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int count = Physics.RaycastNonAlloc(ray, aimHits, gun.fireRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        PlayerMovement target = null;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = aimHits[i];
            if (h.distance >= best || h.collider.transform.IsChildOf(transform))
                continue;
            best = h.distance;
            target = h.collider.GetComponentInParent<PlayerMovement>();
        }
        return target != null && !target.IsDead && target.playerTeam != movement.playerTeam;
    }
}
