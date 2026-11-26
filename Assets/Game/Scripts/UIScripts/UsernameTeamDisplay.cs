using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Name tag above REMOTE players: name and team in team colour, plus a small health bar
/// (health is synced by the owner). Hidden while the player is dead.
/// </summary>
public class UsernameTeamDisplay : MonoBehaviourPunCallbacks
{
    public Text usernameText;
    public Text teamText;
    public PhotonView view;

    PlayerMovement movement;
    RectTransform healthFill;
    Image healthFillImage;
    float shownHealth = -1f;
    bool shownDead;
    CanvasGroup group;

    void Start()
    {
        if (view == null)
            view = GetComponentInParent<PhotonView>();
        if (view == null)
            return;

        if (view.IsMine)
        {
            gameObject.SetActive(false);
            return;
        }

        movement = view.GetComponent<PlayerMovement>();
        group = gameObject.GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        usernameText.text = view.Owner.NickName;
        BuildHealthBar();
        RefreshTeam();
    }

    void BuildHealthBar()
    {
        if (usernameText == null)
            return;
        RectTransform nameRect = usernameText.rectTransform;
        Rect r = nameRect.rect;
        float width = Mathf.Max(r.width * 0.7f, 1f);
        float height = Mathf.Max(r.height * 0.12f, 0.01f);

        Image back = NewImage(nameRect.parent, "HealthBar", new Color(0f, 0f, 0f, 0.6f));
        RectTransform backRect = back.rectTransform;
        backRect.anchorMin = nameRect.anchorMin;
        backRect.anchorMax = nameRect.anchorMax;
        backRect.pivot = new Vector2(0.5f, 1f);
        backRect.sizeDelta = new Vector2(width, height);
        backRect.anchoredPosition = nameRect.anchoredPosition + new Vector2(0f, -r.height * 0.5f);
        backRect.localScale = Vector3.one;

        Image fill = NewImage(backRect, "Fill", Color.white);
        healthFill = fill.rectTransform;
        healthFill.anchorMin = Vector2.zero;
        healthFill.anchorMax = Vector2.one;
        healthFill.offsetMin = healthFill.offsetMax = Vector2.zero;
        healthFillImage = fill;
    }

    // The name tag is a world-space uGUI canvas in the Player prefab, so the bar stays uGUI too.
    static Image NewImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    void Update()
    {
        if (movement == null)
            return;

        bool dead = movement.IsDead;
        if (dead != shownDead)
        {
            shownDead = dead;
            group.alpha = dead ? 0f : 1f;
        }

        float health = movement.HealthFraction;
        if (healthFill != null && !Mathf.Approximately(health, shownHealth))
        {
            shownHealth = health;
            healthFill.anchorMax = new Vector2(health, 1f);
            healthFillImage.color =Color.Lerp(Teams.RedColor, new Color(0.4f, 1f, 0.45f), health);
        }
    }

    void RefreshTeam()
    {
        int team = PlayerTeams.GetTeam(view.Owner);
        Color color = Teams.Color(team);
        teamText.text = team > 0 ? Teams.Name(team) : "";
        teamText.color = color;
        usernameText.color = Color.Lerp(Color.white, color, 0.6f);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (view != null && targetPlayer == view.Owner && changedProps.ContainsKey(PlayerTeams.TeamKey))
            RefreshTeam();
    }
}
