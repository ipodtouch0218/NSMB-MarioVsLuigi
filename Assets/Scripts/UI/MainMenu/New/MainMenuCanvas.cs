using NSMB.Extensions;
using NSMB.UI.MainMenu.Submenus.Prompts;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu {
    public class MainMenuCanvas : MonoBehaviour {

        //---Events
        public event Action<Color> HeaderColorChanged;

        //---Properties
        public static MainMenuCanvas Instance { get; private set; }
        public List<MainMenuSubmenu> SubmenuStack => submenuStack;
        public Color HeaderColor => headerImage.color;

        //---Serialized Variables
        [SerializeField] private MainMenuSubmenu startingSubmenu;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private MainMenuSubmenu goToSubmenuOnError;
        [SerializeField] private ErrorPromptSubmenu errorSubmenu;

        [Header("Header")]
        [SerializeField] private GameObject header;
        [SerializeField] private Image headerImage;
        [SerializeField] private TMP_Text headerPath;
        [SerializeField] private string headerSeparation;

        //---Private Variables
        private readonly List<MainMenuSubmenu> allSubmenus = new();
        private readonly List<MainMenuSubmenu> submenuStack = new();
        private Color defaultHeaderColor;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
        }

        public void Awake() {
            Instance = this;
            defaultHeaderColor = headerImage.color;
        }

        public void Start() {
            GetComponentsInChildren(true, allSubmenus);
            foreach (var menu in allSubmenus) {
                menu.Initialize(this);
                menu.Hide(SubmenuHideReason.Closed);
            }

            NetworkHandler.OnError += OnError;
            OpenMenu(startingSubmenu);
        }

        public void UpdateHeader() {
            StringBuilder builder = new();

            bool showHeader = false;
            Color? newHeaderColor = null;
            foreach (var menu in submenuStack) {
                showHeader |= menu.ShowHeader;
                if (!string.IsNullOrEmpty(menu.Header)) {
                    builder.Append(menu.Header).Append(headerSeparation);
                }
                if (menu.HeaderColor.HasValue) {
                    newHeaderColor = menu.HeaderColor;
                }
            }

            if (builder.Length > 0) {
                builder.Remove(builder.Length - headerSeparation.Length, headerSeparation.Length);
                headerPath.text = builder.ToString();
            }

            Color newColor = newHeaderColor ?? defaultHeaderColor;
            if (HeaderColor == newColor) {
                HeaderColorChanged?.Invoke(newColor);
            }
            headerImage.color = newColor;
            header.SetActive(showHeader);
        }

        public void OpenMenu(MainMenuSubmenu menu) {
            if (submenuStack.Contains(menu)) {
                // Close menus on top of this menu
                while (submenuStack[^1] != menu) {
                    GoBack(true);
                }
            }

            if (submenuStack.Count > 0) {
                submenuStack[^1].Hide(menu.IsOverlay ? SubmenuHideReason.Overlayed : SubmenuHideReason.Background);
                PlaySound(menu.IsOverlay ? SoundEffect.UI_WindowOpen : SoundEffect.UI_Decide);
            }

            submenuStack.Add(menu);
            menu.Show(true);
            UpdateHeader();
            ShowHideMainPanel();
        }

        public void CloseSubmenuWithChildren(MainMenuSubmenu menu) {
            // Close menus on top of this menu, incuding `menu`
            if (!submenuStack.Contains(menu)) {
                return;
            }

            while (true) {
                bool stop = submenuStack[^1] == menu;
                GoBack(true);
                if (stop) {
                    break;
                }
            }
        }
        
        public void CloseSubmenu(MainMenuSubmenu menu) {
            // Close only the single submenu. Call Show() on the new parent if we need to.
            var head = submenuStack[^1];
            if (submenuStack.Remove(menu)) {
                menu.Hide(SubmenuHideReason.Closed);
                var newHead = submenuStack[^1];
                if (newHead != head) {
                    newHead.Show(false);
                }
            }
        }

        public void GoBack() {
            // We have to do this instead of a default parmeter value
            // since I added the parameter later, and adding it
            // breaks existing UnityAction references.
            GoBack(false);
        }

        public void GoBack(bool force) {
            if (submenuStack.Count <= 1) {
                return;
            }

            MainMenuSubmenu currentSubmenu = submenuStack[^1];
            bool playSound = false;
            if (force || currentSubmenu.TryGoBack(out playSound)) {
                currentSubmenu.Hide(SubmenuHideReason.Closed);
                submenuStack.RemoveAt(submenuStack.Count - 1);
                submenuStack[^1].Show(false);
            }

            if (playSound) {
                PlaySound(currentSubmenu.IsOverlay ? SoundEffect.UI_WindowClose : SoundEffect.UI_Back);
            }

            UpdateHeader();
            ShowHideMainPanel();
        }

        public void PlaySound(SoundEffect sound, CharacterAsset character = null) {
            sfx.PlayOneShot(sound, character);
        }

        public void PlayConfirmSound() {
            PlaySound(SoundEffect.UI_Decide);
        }

        public void PlayCursorSound() {
            PlaySound(SoundEffect.UI_Cursor);
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

        private void OnError(string message) {
            errorSubmenu.OpenWithString(message);
        }
    }

    public enum SubmenuHideReason {
        Closed,
        Background,
        Overlayed,
    }
}
