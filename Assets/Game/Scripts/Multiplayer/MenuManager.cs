using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public static MenuManager instance;
    public Menu[] menus;

    void Awake()
    {
        instance = this;
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
        Debug.LogWarning("Menu not found: " + menuName);
    }

    public void OpenMenu(Menu menu)
    {
        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i] != menu && menus[i].isOpen)
                CloseMenu(menus[i]);
        }
        menu.Open();
    }

    public void CloseMenu(Menu menu)
    {
        menu.Close();
    }
}
