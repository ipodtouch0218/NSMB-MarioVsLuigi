using NSMB.Extensions;
using NSMB.Translation;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class MainMenuCanvas : MonoBehaviour {

    //---Properties
    public List<MainMenuSubmenu> SubmenuStack => submenuStack;

    //---Serialized Variables
    [SerializeField] private MainMenuSubmenu startingSubmenu;
    [SerializeField] private List<MainMenuSubmenu> submenus;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private AudioSource sfx;

    [Header("Header")]
    [SerializeField] private GameObject header;
    [SerializeField] private TMP_Text headerPath;
    [SerializeField] private string headerSeparation;


    //---Private Variables
    private readonly List<MainMenuSubmenu> submenuStack = new();

    public void OnValidate() {
        this.SetIfNull(ref sfx);
    }

    public void Start() {
        foreach (var menu in submenus) {
            menu.Initialize(this);
        }
        OpenMenu(startingSubmenu);
    }

    public void UpdateHeader() {
        StringBuilder builder = new();

        bool showHeader = false;
        foreach (var menu in submenuStack) {
            showHeader |= menu.ShowHeader;
            if (!string.IsNullOrEmpty(menu.Header)) {
                builder.Append(menu.Header).Append(headerSeparation);
            }
        }

        if (builder.Length > 0) {
            builder.Remove(builder.Length - headerSeparation.Length, headerSeparation.Length);
            headerPath.text = builder.ToString();
        }

        header.SetActive(showHeader);
    }

    public void OpenMenu(MainMenuSubmenu menu) {
        if (submenuStack.Count > 0) {
            submenuStack[^1].Hide(true);
        }

        submenuStack.Add(menu);
        menu.Show(true);
        UpdateHeader();
        ShowHideMainPanel();

        sfx.PlayOneShot(SoundEffect.UI_Decide);
    }

    public void GoBack() {
        if (submenuStack.Count <= 1) {
            return;
        }

        bool playSound = false;
        MainMenuSubmenu currentSubmenu = submenuStack[^1];
        if (currentSubmenu.TryGoBack(out playSound)) {
            currentSubmenu.Hide(false);
            submenuStack.RemoveAt(submenuStack.Count - 1);
            submenuStack[^1].Show(false);
        }

        if (playSound) {
            sfx.PlayOneShot(SoundEffect.UI_Back);
        }

        UpdateHeader();
        ShowHideMainPanel();
    }

    private void ShowHideMainPanel() {
        bool showMainPanel = false;
        foreach (var submenu in submenuStack) {
            if (submenu.RequiresMainPanel) {
                showMainPanel = true;
                break;
            }
        }

        mainPanel.SetActive(showMainPanel);
    }
}
