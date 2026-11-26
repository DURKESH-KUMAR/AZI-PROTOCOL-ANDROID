using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Photon.Pun;

/// <summary>
/// In-match HUD (Resources/UI/HUD.uxml + HUD.uss), created automatically in the match scene.
/// Ammo + reload bar, hit markers, damage vignette + direction arrow, low-health pulse,
/// kill feed, kill-streak call-outs, radar, warm-up countdown, spawn protection,
/// death/respawn screen, match-over banner and pause menu with settings.
/// Animations are USS transitions (toggle a class), and styles are only written when a
/// value changes, so the HUD costs next to nothing per frame. Only buttons and the open
/// pause menu take touches; the joysticks underneath keep working.
/// </summary>
public class MatchHUD : MonoBehaviour
{
    public static MatchHUD Instance { get; private set; }

    const float RadarRange = 40f;
    const float RadarHalf = 107f;   // (230px radar / 2) - dot margin
    const float EnemyRevealTime = 2.5f;
    const int MaxFeed = 5;
    const float FeedLifetime = 6f;
    const float DamageArrowRadius = 230f;

    // Elements
    VisualElement lowHealth, damageFlash, damageArrow, hitmarker, callout, ammoBox, reload, reloadFill;
    VisualElement killfeed, radar, radarDots, death, banner, pause, settingsHost;
    Label spawn, calloutTitle, calloutSub, warmup, ammoCount, reloadLabel, deathTitle, deathTimer, bannerTitle, bannerSub;
    Button leaveButton;
    VisualElement settings;

    // State
    PlayerMovement local;
    ShootingController localGun;
    Camera localCamera;
    int shownAmmo = -1, shownMaxAmmo = -1;
    string shownSpawn, shownDeathTimer;
    float hitUntil, calloutUntil, respawnAt;
    float damageFlashValue, damageArrowValue, shownFlash = -1f, shownLow = -1f, shownArrow = -1f;
    Vector3 lastDamageFrom;
    int lastWarmupSecond = -1;
    bool fightShown, leaveArmed, reloadShown, radarShown = true, ammoShown = true;
    float shownReload = -1f;
    int killStreak;

    readonly List<FeedEntry> feed = new List<FeedEntry>();
    readonly List<VisualElement> dots = new List<VisualElement>();

    struct FeedEntry
    {
        public Label label;
        public float born;
        public bool fading;
    }

    void Awake()
    {
        Instance = this;
        VisualElement root = GameUI.CreateDocument(transform, "HUD", 50).rootVisualElement;
        Query(root);

        Texture2D vignette = GameUI.Vignette;
        lowHealth.style.backgroundImage = vignette;
        lowHealth.style.unityBackgroundImageTintColor = new Color(0.55f, 0f, 0f);
        damageFlash.style.backgroundImage = vignette;
        damageFlash.style.unityBackgroundImageTintColor = new Color(0.85f, 0.05f, 0.05f);

        for (int i = 0; i < 4; i++)
        {
            VisualElement line = hitmarker.Q("hit-" + i);
            float angle = 45f + 90f * i;
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.right * 26f;
            line.style.translate = new Translate(dir.x, -dir.y);
            line.style.rotate = new Rotate(-angle);
        }

        root.Bind("pause-button", TogglePause);
        root.Bind("resume", TogglePause);
        root.Bind("settings", OpenSettings);
        leaveButton = root.Bind("leave", OnLeavePressed);
    }

    void Query(VisualElement root)
    {
        lowHealth = root.Q("low-health");
        damageFlash = root.Q("damage-flash");
        damageArrow = root.Q("damage-arrow");
        hitmarker = root.Q("hitmarker");
        spawn = root.Q<Label>("spawn");
        callout = root.Q("callout");
        calloutTitle = root.Q<Label>("callout-title");
        calloutSub = root.Q<Label>("callout-sub");
        warmup = root.Q<Label>("warmup");
        ammoBox = root.Q("ammo");
        ammoCount = root.Q<Label>("ammo-count");
        reload = root.Q("reload");
        reloadLabel = root.Q<Label>("reload-label");
        reloadFill = root.Q("reload-fill");
        killfeed = root.Q("killfeed");
        radar = root.Q("radar");
        radarDots = root.Q("radar-dots");
        death = root.Q("death");
        deathTitle = root.Q<Label>("death-title");
        deathTimer = root.Q<Label>("death-timer");
        banner = root.Q("banner");
        bannerTitle = root.Q<Label>("banner-title");
        bannerSub = root.Q<Label>("banner-sub");
        pause = root.Q("pause");
        settingsHost = root.Q("settings-host");
    }

    void OnEnable()
    {
        MatchEvents.Kill += OnKill;
        MatchEvents.LocalHit += OnLocalHit;
        MatchEvents.LocalDamaged += OnLocalDamaged;
        MatchEvents.LocalDied += OnLocalDied;
        MatchEvents.LocalSpawned += OnLocalSpawned;
        MatchEvents.MatchOver += OnMatchOver;
    }

    void OnDisable()
    {
        MatchEvents.Kill -= OnKill;
        MatchEvents.LocalHit -= OnLocalHit;
        MatchEvents.LocalDamaged -= OnLocalDamaged;
        MatchEvents.LocalDied -= OnLocalDied;
        MatchEvents.LocalSpawned -= OnLocalSpawned;
        MatchEvents.MatchOver -= OnMatchOver;
    }

    void OnDestroy()
    {
        PlayerManager.InputBlocked = false;
        if (Instance == this)
            Instance = null;
    }

    // ===================================================================== PAUSE

    public bool IsPaused => pause != null && !pause.ClassListContains("hidden");

    public void TogglePause()
    {
        bool open = !IsPaused;
        pause.SetVisible(open);
        CloseSettings();
        leaveArmed = false;
        if (leaveButton != null)
            leaveButton.text = "LEAVE MATCH";
        PlayerManager.InputBlocked = open;
        UnityEngine.Cursor.visible = open;
        UnityEngine.Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
    }

    void OpenSettings()
    {
        if (settings == null)
            settings = SettingsPanel.Open(settingsHost, false, () => settings = null);
    }

    void CloseSettings()
    {
        if (settings == null)
            return;
        GameSettings.Save();
        settings.RemoveFromHierarchy();
        settings = null;
    }

    void OnLeavePressed()
    {
        // Two taps: leaving by accident mid-match is painful.
        if (!leaveArmed)
        {
            leaveArmed = true;
            leaveButton.text = "TAP AGAIN TO LEAVE";
            return;
        }
        GameManager manager = FindAnyObjectByType<GameManager>();
        if (manager != null)
            manager.OpenScene4();
        else
            GameManager.LeaveToResults();
    }

    /// <summary>Android back: close settings, else toggle pause.</summary>
    public void OnBackPressed()
    {
        if (settings != null)
            CloseSettings();
        else
            TogglePause();
    }

    // ===================================================================== EVENTS

    void OnLocalSpawned(PlayerMovement player)
    {
        local = player;
        localGun = player != null ? player.Shooting : null;
        localCamera = player != null ? player.GetComponentInChildren<Camera>(true) : null;
        death.SetVisible(false);
        shownAmmo = shownMaxAmmo = -1;
    }

    void OnLocalHit()
    {
        hitUntil = Time.time + 0.1f;
        hitmarker.RemoveFromClassList("hitmarker--kill");
        hitmarker.AddToClassList("hitmarker--show");
        GameAudio.PlayUI(GameAudio.Hit, 0.55f);
    }

    void OnLocalDamaged(float amount, Vector3 from)
    {
        damageFlashValue = Mathf.Clamp01(damageFlashValue + 0.35f + amount / 60f);
        damageArrowValue = 1f;
        lastDamageFrom = from;
        GameAudio.PlayUI(GameAudio.Hurt, 0.7f);
    }

    void OnLocalDied(string killer, float respawnDelay)
    {
        killStreak = 0;
        respawnAt = Time.time + respawnDelay;
        deathTitle.text = string.IsNullOrEmpty(killer) ? "YOU FELL" : "ELIMINATED BY  " + killer.ToUpperInvariant();
        shownDeathTimer = null;
        death.SetVisible(true);
        if (IsPaused)
            TogglePause();
    }

    void OnKill(KillInfo info)
    {
        AddFeedEntry(info);
        if (!info.LocalIsKiller)
            return;

        killStreak++;
        hitUntil = Time.time + 0.3f;
        hitmarker.AddToClassList("hitmarker--kill");
        hitmarker.AddToClassList("hitmarker--show");
        GameAudio.PlayUI(GameAudio.Kill, 0.8f);
        ShowCallout(StreakName(killStreak) ?? "ELIMINATED", info.victimName.ToUpperInvariant(), 2.2f);
        if (killStreak >= 2)
            GameSettings.Vibrate();
    }

    static string StreakName(int streak)
    {
        switch (streak)
        {
            case 2: return "DOUBLE KILL";
            case 3: return "TRIPLE KILL";
            case 4: return "QUAD KILL";
            case 5: return "RAMPAGE";
            default: return streak > 5 ? "UNSTOPPABLE" : null;
        }
    }

    void OnMatchOver(int winner)
    {
        int myTeam = PlayerTeams.GetTeam(PhotonNetwork.LocalPlayer);
        string title;
        Color color;
        if (winner == 0) { title = "DRAW"; color = GameUI.TextColor; }
        else if (myTeam == 0) { title = Teams.Name(winner) + " WINS"; color = Teams.Color(winner); }
        else if (winner == myTeam) { title = "VICTORY"; color = GameUI.Accent; }
        else { title = "DEFEAT"; color = Teams.RedColor; }

        ScoreBoard sb = ScoreBoard.instance;
        bannerTitle.text = title;
        bannerTitle.style.color = color;
        bannerSub.text = (sb != null ? "BLUE  " + sb.blueTeamScore + "   -   " + sb.redTeamScore + "  RED\n" : "") + "<size=26>Loading results...</size>";
        banner.AddToClassList("banner--show");
        warmup.text = "";
        GameAudio.PlayUI(winner != 0 && winner == myTeam ? GameAudio.Victory : GameAudio.Defeat, 0.9f);
        if (IsPaused)
            TogglePause();
    }

    void ShowCallout(string title, string sub, float seconds)
    {
        calloutTitle.text = title;
        calloutSub.text = sub;
        calloutUntil = Time.time + seconds;
        // Restart the pop-in transition.
        callout.RemoveFromClassList("callout--show");
        callout.schedule.Execute(() => callout.AddToClassList("callout--show"));
    }

    void AddFeedEntry(KillInfo info)
    {
        if (feed.Count >= MaxFeed)
        {
            feed[0].label.RemoveFromHierarchy();
            feed.RemoveAt(0);
        }

        string victim = "<color=" + GameUI.Hex(Teams.Color(info.victimTeam)) + ">" + info.victimName + "</color>";
        string text = info.IsEnvironment
            ? victim + "   <color=#B0B8C4>fell</color>"
            : "<color=" + GameUI.Hex(Teams.Color(info.killerTeam)) + ">" + info.killerName + "</color>   <color=#FFB830>»</color>   " + victim;

        Label row = new Label(text) { pickingMode = PickingMode.Ignore };
        row.AddToClassList("feed-row");
        row.AddToClassList("feed-row--new");
        if (info.LocalIsKiller || info.LocalIsVictim)
            row.AddToClassList("feed-row--mine");
        killfeed.Insert(0, row); // newest on top
        row.schedule.Execute(() => row.RemoveFromClassList("feed-row--new")); // slide in
        feed.Add(new FeedEntry { label = row, born = Time.time });
    }

    // ===================================================================== UPDATE

    void Update()
    {
        if (local == null && PlayerRegistry.Local != null)
            OnLocalSpawned(PlayerRegistry.Local);

        float dt = Time.deltaTime;
        UpdateWeapon();
        UpdateTimedClasses();
        UpdateDamage(dt);
        UpdateWarmup();
        UpdateFeed();
        UpdateRadar();
        UpdateDeath();
    }

    void UpdateWeapon()
    {
        bool alive = local != null && !local.IsDead && localGun != null;
        if (ammoShown != alive)
        {
            ammoShown = alive;
            ammoBox.SetVisible(alive);
        }
        if (!alive)
        {
            SetReloadVisible(false);
            SetSpawn("");
            return;
        }

        int ammo = localGun.CurrentAmmo, max = localGun.MaxAmmo;
        if (ammo != shownAmmo || max != shownMaxAmmo)
        {
            shownAmmo = ammo;
            shownMaxAmmo = max;
            ammoCount.text = ammo + " <size=28><color=#9AA4B2>/ " + max + "</color></size>";
            ammoCount.EnableInClassList("ammo__count--empty", ammo == 0);
            ammoCount.EnableInClassList("ammo__count--low", ammo > 0 && ammo <= max / 4);
        }

        bool reloading = localGun.IsReloading;
        bool empty = ammo == 0 && !reloading;
        SetReloadVisible(reloading || empty);
        if (reloading)
        {
            reloadLabel.text = "RELOADING";
            reloadLabel.RemoveFromClassList("reload__label--empty");
            float p = Mathf.Round(localGun.ReloadProgress * 50f) / 50f; // 2% steps: fewer layout updates
            if (p != shownReload)
            {
                shownReload = p;
                reloadFill.style.width = Length.Percent(p * 100f);
            }
        }
        else if (empty)
        {
            reloadLabel.text = "TAP RELOAD";
            reloadLabel.AddToClassList("reload__label--empty");
            if (shownReload != 0f)
            {
                shownReload = 0f;
                reloadFill.style.width = Length.Percent(0f);
            }
        }

        SetSpawn(local.IsSpawnProtected ? "SPAWN PROTECTED  " + local.SpawnProtectionRemaining.ToString("0.0") + "s" : "");
    }

    void SetReloadVisible(bool visible)
    {
        if (reloadShown == visible)
            return;
        reloadShown = visible;
        reload.SetVisible(visible);
    }

    void SetSpawn(string text)
    {
        if (text == shownSpawn)
            return;
        shownSpawn = text;
        spawn.text = text;
    }

    void UpdateTimedClasses()
    {
        if (hitmarker.ClassListContains("hitmarker--show") && Time.time >= hitUntil)
            hitmarker.RemoveFromClassList("hitmarker--show"); // USS fades it out
        if (calloutUntil > 0f && Time.time >= calloutUntil)
        {
            calloutUntil = 0f;
            callout.RemoveFromClassList("callout--show");
        }
    }

    void UpdateDamage(float dt)
    {
        damageFlashValue = Mathf.Max(0f, damageFlashValue - dt * 1.6f);
        SetOpacity(damageFlash, damageFlashValue * 0.75f, ref shownFlash);

        float health = local != null && !local.IsDead ? local.HealthFraction : 1f;
        float low = health < 0.35f ? (0.35f - health) / 0.35f : 0f;
        SetOpacity(lowHealth, low > 0f ? low * (0.45f + 0.2f * Mathf.Sin(Time.time * 6f)) : 0f, ref shownLow);

        damageArrowValue = Mathf.Max(0f, damageArrowValue - dt * 0.9f);
        SetOpacity(damageArrow, damageArrowValue, ref shownArrow);
        if (damageArrowValue > 0f && local != null)
        {
            Vector2 rel = ToViewSpace(lastDamageFrom - local.transform.position);
            float angle = Mathf.Atan2(rel.x, rel.y); // 0 = straight ahead
            damageArrow.style.translate = new Translate(Mathf.Sin(angle) * DamageArrowRadius, -Mathf.Cos(angle) * DamageArrowRadius);
            damageArrow.style.rotate = new Rotate(angle * Mathf.Rad2Deg); // UI Toolkit rotates clockwise: bar stays tangent to the ring
        }
    }

    static void SetOpacity(VisualElement e, float value, ref float shown)
    {
        value = Mathf.Round(value * 100f) / 100f;
        if (value == shown)
            return;
        shown = value;
        e.style.opacity = value;
    }

    void UpdateWarmup()
    {
        TimerScript timer = TimerScript.Instance;
        if (timer == null || MatchState.IsOver)
            return;

        if (timer.IsWarmup)
        {
            int seconds = Mathf.Max(1, Mathf.CeilToInt(timer.WarmupRemaining));
            if (seconds != lastWarmupSecond)
            {
                lastWarmupSecond = seconds;
                warmup.text = "MATCH STARTS IN\n<size=120>" + seconds + "</size>";
                warmup.AddToClassList("warmup--pulse");
                warmup.schedule.Execute(() => warmup.RemoveFromClassList("warmup--pulse"));
                if (seconds <= 3)
                    GameAudio.PlayUI(GameAudio.Beep, 0.6f);
            }
            fightShown = false;
        }
        else if (!fightShown)
        {
            fightShown = true;
            lastWarmupSecond = -1;
            warmup.text = "";
            if (Time.timeSinceLevelLoad > 1f) // not when joining an already-running match
            {
                int limit = ScoreBoard.instance != null ? ScoreBoard.instance.ScoreLimit : 0;
                ShowCallout("FIGHT!", limit > 0 ? "First team to " + limit + " kills wins" : "Most kills wins", 1.8f);
                GameAudio.PlayUI(GameAudio.Go, 0.7f);
            }
        }
    }

    void UpdateFeed()
    {
        for (int i = feed.Count - 1; i >= 0; i--)
        {
            FeedEntry entry = feed[i];
            float age = Time.time - entry.born;
            if (age > FeedLifetime)
            {
                entry.label.RemoveFromHierarchy();
                feed.RemoveAt(i);
            }
            else if (!entry.fading && age > FeedLifetime - 0.7f)
            {
                entry.fading = true;
                entry.label.AddToClassList("feed-row--fade"); // USS opacity transition
                feed[i] = entry;
            }
        }
    }

    void UpdateRadar()
    {
        bool show = local != null && !local.IsDead;
        if (radarShown != show)
        {
            radarShown = show;
            radar.SetVisible(show);
        }
        if (!show)
            return;

        int myTeam = local.playerTeam;
        int used = 0;
        for (int i = 0; i < PlayerRegistry.All.Count; i++)
        {
            PlayerMovement p = PlayerRegistry.All[i];
            if (p == null || p == local || p.IsDead)
                continue;
            bool ally = myTeam != 0 && p.playerTeam == myTeam;
            float sinceShot = Time.time - p.LastShotTime;
            if (!ally && sinceShot > EnemyRevealTime)
                continue; // enemies only appear on the radar when they fire

            Vector2 rel = ToViewSpace(p.transform.position - local.transform.position) / RadarRange * RadarHalf;
            if (rel.sqrMagnitude > RadarHalf * RadarHalf)
            {
                if (!ally)
                    continue;
                rel = rel.normalized * RadarHalf; // allies pinned to the edge
            }

            VisualElement dot = Dot(used++);
            Color c = Teams.Color(p.playerTeam);
            if (!ally)
                c.a = Mathf.Clamp01(1.2f - sinceShot / EnemyRevealTime);
            dot.style.backgroundColor = c;
            dot.style.left = 115f + rel.x;
            dot.style.top = 115f - rel.y;
        }
        for (int i = used; i < dots.Count; i++)
            dots[i].SetVisible(false);
    }

    VisualElement Dot(int index)
    {
        while (dots.Count <= index)
        {
            VisualElement dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("radar__dot");
            radarDots.Add(dot);
            dots.Add(dot);
        }
        VisualElement d = dots[index];
        d.SetVisible(true);
        return d;
    }

    void UpdateDeath()
    {
        if (death.ClassListContains("hidden"))
            return;
        float left = Mathf.Max(0f, respawnAt - Time.time);
        string text = MatchState.IsOver ? "" : left > 0.05f ? "Respawning in " + Mathf.CeilToInt(left) : "Respawning...";
        if (text != shownDeathTimer)
        {
            shownDeathTimer = text;
            deathTimer.text = text;
        }
    }

    /// <summary>World offset -> 2D offset relative to where the local camera looks (x right, y forward).</summary>
    Vector2 ToViewSpace(Vector3 worldOffset)
    {
        float yaw = localCamera != null ? localCamera.transform.eulerAngles.y : local != null ? local.transform.eulerAngles.y : 0f;
        float r = yaw * Mathf.Deg2Rad;
        float cos = Mathf.Cos(r), sin = Mathf.Sin(r);
        return new Vector2(worldOffset.x * cos - worldOffset.z * sin, worldOffset.x * sin + worldOffset.z * cos);
    }
}
