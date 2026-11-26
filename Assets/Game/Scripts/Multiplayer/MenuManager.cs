using System;
using UnityEngine;

/// <summary>
/// Switches between the main-menu screens by name ("LoadingMenu", "TitleMenu", ...).
/// Still drives the legacy uGUI menus, and raises <see cref="MenuOpened"/> so the UI Toolkit
/// main menu (MainMenuUI) shows the matching screen. Launcher only talks to this class,
/// so it works with either UI.
/// </summary>
public class MenuManager : MonoBehaviour
{
    public static MenuManager instance;
    public Menu[] menus;

    /// <summary>Raised with the menu name whenever a menu opens.</summary>
    public static event Action<string> MenuOpened;

    /// <summary>The last opened menu name (so a UI created later can sync).</summary>
    public static string Current { get; private set; }

    void Awake()
    {
        instance = this;
        Current = null;
        for (int i = 0; i < menus.Length; i++)
            if (menus[i] != null && menus[i].isOpen)
                Current = menus[i].menuName;
    }

    void OnDestroy()
    {
        // Don't keep a static reference to a destroyed object after the scene unloads.
        if (instance == this)
            instance = null;
    }

    public void OpenMenu(string menuName)
    {
        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i].menuName == menuName)
            {
                OpenMenu(menus[i]);
                return;
            }
        }
        // Not an error with the UI Toolkit menu: it handles names on its own.
        Current = menuName;
        MenuOpened?.Invoke(menuName);
    }

    public void OpenMenu(Menu menu)
    {
        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i] != menu && menus[i].isOpen)
                CloseMenu(menus[i]);
        }
        menu.Open();
        Current = menu.menuName;
        MenuOpened?.Invoke(menu.menuName);
    }

    public void CloseMenu(Menu menu)
    {
        menu.Close();
    }
}
