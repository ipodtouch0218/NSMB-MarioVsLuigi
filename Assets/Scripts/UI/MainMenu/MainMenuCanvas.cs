using NSMB.Networking;
using NSMB.UI.MainMenu.Submenus.Prompts;
using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu {
    public class MainMenuCanvas : MonoBehaviour {

        //---Events
        public event Action<Color> HeaderColorChanged;

        //---Properties
        public static MainMenuCanvas Instance { get; private set; }
        public List<MainMenuSubmenu> SubmenuStack => submenuStack;
        public Color HeaderColor => headerImage.color;
        public EventSystem EventSystem => eventSystem;

        //---Serialized Variables
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private MainMenuSubmenu startingSubmenu;
        [SerializeField] private MainMenuSubmenu[] inRoomSubmenuStack;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private ErrorPromptSubmenu errorSubmenu;

        [Header("Header")]
        [SerializeField] private GameObject header;
        [SerializeField] private Image headerImage;
        [SerializeField] private TMP_Text headerPath;

        //---Private Variables
        private readonly List<MainMenuSubmenu> allSubmenus = new();
        private readonly List<MainMenuSubmenu> submenuStack = new();
        private Color defaultHeaderColor;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
        }

        public void OnEnable() {
            Settings.Controls.UI.Enable();
        }

        public void OnDisable() {
            while (submenuStack.Count > 0) {
                if (submenuStack[^1] is PromptSubmenu ps) {
                    CloseSubmenu(ps);
                } else {
                    break;
                }
            }
        }

        public void Awake() {
            Instance = this;
            defaultHeaderColor = headerImage.color;
        }

        public void Start() {
            GetComponentsInChildren(true, allSubmenus);
            foreach (var menu in allSubmenus) {
                menu.Initialize();
                menu.Hide(SubmenuHideReason.Closed);
            }

            NetworkHandler.OnError += OnError;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OpenMenu(startingSubmenu);
            // Bodge for opening the room list if we somehow unloaded the main menu...
            // Usually happens when we start the game in the editor and end the game.
            var runner = QuantumRunner.Default;
            if (runner != null && !runner.Session.IsReplay) {
                foreach (var submenu in inRoomSubmenuStack) {
                    OpenMenu(submenu, null, false);
                }
            }
        }

        public void OnDestroy() {
            foreach (var menu in allSubmenus) {
                menu.OnDestroy();
            }
            NetworkHandler.OnError -= OnError;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Update() {
            // Fallback: select the default object if we somehow aren't selecting anything.
            if (!eventSystem.currentSelectedGameObject && SubmenuStack.Count > 0) {
                eventSystem.SetSelectedGameObject(SubmenuStack[^1].DefaultSelection);
            }
        }

        public void UpdateHeader() {
            StringBuilder builder = new();

            bool showHeader = false;
            Color? newHeaderColor = null;

            bool rtl = GlobalController.Instance.translationManager.RightToLeft;
            IEnumerable<MainMenuSubmenu> submenus = rtl ? submenuStack.Reverse<MainMenuSubmenu>() : submenuStack;
            string headerSeparation = rtl ? " < " : " > ";
            foreach (var menu in submenus) {
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
                headerPath.horizontalAlignment = rtl ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
            }

            Color newColor = newHeaderColor ?? defaultHeaderColor;
            if (HeaderColor != newColor) {
                headerImage.color = newColor;
                HeaderColorChanged?.Invoke(newColor);
            }
            header.SetActive(showHeader);
        }

        public bool IsSubmenuOpen(MainMenuSubmenu menu) {
            return submenuStack.Contains(menu);
        }

        public void OpenMenu(MainMenuSubmenu menu) {
            OpenMenu(menu, menu.IsOverlay ? SoundEffect.UI_WindowOpen : SoundEffect.UI_Decide);
        }

        public void OpenMenu(MainMenuSubmenu menu, SoundEffect? sound, bool first = true) {
            bool wasPresent = submenuStack.Contains(menu);
            if (wasPresent) {
                // Close menus on top of this menu
                while (submenuStack[^1] != menu) {
                    GoBack(true);
                }
                first = false;
            }

            if (submenuStack.Count > 0) {
                submenuStack[^1].Hide(menu.IsOverlay ? SubmenuHideReason.Overlayed : SubmenuHideReason.Background);
                if (sound.HasValue) {
                    PlaySound(sound.Value);
                }
            }

            if (!wasPresent) {
                submenuStack.Add(menu);
            }

            menu.InternalShow(first);
            UpdateHeader();
            ShowHideMainPanel();
        }

        public void CloseSubmenuAndChildren(MainMenuSubmenu menu) {
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
                    newHead.InternalShow(false);
                    UpdateHeader();
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
                submenuStack[^1].InternalShow(false);
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

        private void OnError(string message, bool disconnect) {
            errorSubmenu.OpenWithString(message, disconnect);
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateHeader();
        }
    }

    public enum SubmenuHideReason {
        Closed,
        Background,
        Overlayed,
    }
}
