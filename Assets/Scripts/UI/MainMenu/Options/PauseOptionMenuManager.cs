using NSMB.Extensions;
using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.Pause.Options {

    public class PauseOptionMenuManager : Selectable {

        //---Static Variables
        public static event Action<bool> OnOptionsOpenedToggled;

        //---Serialzied Variables
        [SerializeField] private List<PauseOptionTab> tabs;
        [SerializeField] private ScrollRect scroll;

        [SerializeField] private Image backButton;
        [SerializeField] private Sprite backSelectedSprite, backDeselectedSprite;
        [SerializeField] private Canvas canvas;

        //---Private Variables
        private int currentTabIndex;
        private int currentOptionIndex; // -1 = tabs are selected
        private bool inputted;
        private GameObject previouslySelected;
        private float upHoldStart, downHoldStart;

        //---Propreties
        public bool EnableInput { get; set; }
        private PauseOption SelectedOption => (currentOptionIndex >= 0 && currentOptionIndex < SelectedTab.options.Count) ? SelectedTab.options[currentOptionIndex] : null;
        private PauseOptionTab SelectedTab => (currentTabIndex >= 0 && currentTabIndex < tabs.Count) ? tabs[currentTabIndex] : null;
        private bool _back;
        private bool Back {
            get => _back;
            set {
                _back = value;
                backButton.sprite = _back ? backSelectedSprite : backDeselectedSprite;
            }
        }
        public bool RequireReconnect { get; set; }

        protected override void OnEnable() {
            base.OnEnable();

            ControlSystem.controls.UI.Navigate.performed += OnNavigate;
            ControlSystem.controls.UI.Navigate.canceled += OnNavigate;
            ControlSystem.controls.UI.Next.performed += OnNext;
            ControlSystem.controls.UI.Previous.performed += OnPrevious;
            ControlSystem.controls.UI.Cancel.performed += OnCancel;
            ControlSystem.controls.UI.Submit.performed += OnSubmit;

            if (EventSystem.current) {
                previouslySelected = EventSystem.current.currentSelectedGameObject;
                EventSystem.current.SetSelectedGameObject(gameObject);
            }

            EnableInput = true;
            OnOptionsOpenedToggled?.Invoke(true);
        }

        protected override void OnDisable() {
            base.OnDisable();

            ControlSystem.controls.UI.Navigate.performed -= OnNavigate;
            ControlSystem.controls.UI.Navigate.canceled -= OnNavigate;
            ControlSystem.controls.UI.Next.performed -= OnNext;
            ControlSystem.controls.UI.Previous.performed -= OnPrevious;
            ControlSystem.controls.UI.Cancel.performed -= OnCancel;
            ControlSystem.controls.UI.Submit.performed -= OnSubmit;

            if (EventSystem.current) {
                EventSystem.current.SetSelectedGameObject(previouslySelected);
            }
            OnOptionsOpenedToggled?.Invoke(false);

            /* TODO
            if (RequireReconnect && NetworkHandler.Runner.LobbyInfo.IsValid) {
                _ = NetworkHandler.ConnectToSameRegion();
            }
            */
            RequireReconnect = false;
        }

        public void Update() {
            if (!Application.isPlaying) {
                return;
            }

            if (!EnableInput) {
                return;
            }

            Vector2 direction = ControlSystem.controls.UI.Navigate.ReadValue<Vector2>();
            direction = direction.normalized;
            float u = Vector2.Dot(direction, Vector2.up);
            float d = Vector2.Dot(direction, Vector2.down);
            float l = Vector2.Dot(direction, Vector2.left);
            float r = Vector2.Dot(direction, Vector2.right);
            bool up = u > d && u > l && u > r;
            bool down = !up && d > u && d > l && d > r;
            bool left = !up && !down && l > u && l > d && l > r;
            bool right = !up && !down && !left && r > u && r > d && r > l;


            if (SelectedTab) {
                Func<bool, bool> func;
                if (up) {
                    func = SelectedTab.OnUpPress;
                } else if (down) {
                    func = SelectedTab.OnDownPress;
                } else if (left) {
                    func = SelectedTab.OnLeftPress;
                } else {
                    func = SelectedTab.OnRightPress;
                }

                if (func(true)) {
                    return;
                }
            }

            if (!SelectedOption) {
                return;
            }

            if (left) {
                SelectedOption.OnLeftHeld();
            } else if (right) {
                SelectedOption.OnRightHeld();
            } else if (up) {
                if (Time.unscaledTime > upHoldStart) {
                    int newOptionIndex = Mathf.Clamp(currentOptionIndex - 1, -1, SelectedTab.options.Count - 1);
                    SetCurrentOption(newOptionIndex, true);
                    upHoldStart = Time.unscaledTime + 0.125f;
                }
            } else if (down) {
                if (Time.unscaledTime > downHoldStart) {
                    // Move between options
                    int newOptionIndex = Mathf.Clamp(currentOptionIndex + 1, -1, SelectedTab.options.Count - 1);
                    SetCurrentOption(newOptionIndex, true);
                    downHoldStart = Time.unscaledTime + 0.125f;
                }
            }
        }

        public void ChangeTab(int diff) {
            if (diff == 0) {
                return;
            }

            if (Back && diff > 0) {
                Back = false;
                if (currentTabIndex != 0) {
                    SetTab(0, false);
                } else {
                    SelectedTab.Highlighted();
                }
                GlobalController.Instance.PlaySound(SoundEffect.UI_Cursor);
                return;

            } else if (!Back && diff < 0 && currentTabIndex == 0) {
                // Enable the back button
                Back = true;
                SetCurrentOption(-1);
                SelectedTab.Unhighlighted();
                GlobalController.Instance.PlaySound(SoundEffect.UI_Cursor);
            }
            int newTabIndex = Mathf.Clamp(currentTabIndex + diff, 0, tabs.Count - 1);
            SetTab(newTabIndex);
        }

        private void OnCancel(InputAction.CallbackContext context) {
            if (!EnableInput) {
                return;
            }

            if (SelectedTab && SelectedTab.OnCancel()) {
                return;
            }

            if (Back) {
                CloseMenu();
                return;
            }

            Back = true;
            SetCurrentOption(-1);
            SelectedTab.Unhighlighted();
            GlobalController.Instance.PlaySound(SoundEffect.UI_Cursor);
        }

        private void OnSubmit(InputAction.CallbackContext context) {
            if (!EnableInput) {
                return;
            }

            if (SelectedTab && SelectedTab.OnSubmit()) {
                return;
            }

            if (Back) {
                CloseMenu();
                return;
            }

            if (!SelectedOption) {
                return;
            }

            SelectedOption.OnClick();
        }

        private void OnNext(InputAction.CallbackContext context) {
            ChangeTab(1);
        }

        private void OnPrevious(InputAction.CallbackContext context) {
            ChangeTab(-1);
        }

        private void OnNavigate(InputAction.CallbackContext context) {

            if (context.canceled || context.ReadValue<Vector2>() == Vector2.zero) {
                inputted = false;
                return;
            }

            if (!EnableInput) {
                return;
            }

            if (inputted) {
                return;
            }

            Vector2 direction = context.ReadValue<Vector2>();
            direction = direction.normalized;
            float u = Vector2.Dot(direction, Vector2.up);
            float d = Vector2.Dot(direction, Vector2.down);
            float l = Vector2.Dot(direction, Vector2.left);
            float r = Vector2.Dot(direction, Vector2.right);
            bool up = u > d && u > l && u > r;
            bool down = !up && d > u && d > l && d > r;
            bool left = !up && !down && l > u && l > d && l > r;
            bool right = !up && !down && !left && r > u && r > d && r > l;

            if (SelectedTab) {
                Func<bool, bool> func;
                if (up) {
                    func = SelectedTab.OnUpPress;
                } else if (down) {
                    func = SelectedTab.OnDownPress;
                } else if (left) {
                    func = SelectedTab.OnLeftPress;
                } else if (right) {
                    func = SelectedTab.OnRightPress;
                } else {
                    return;
                }

                if (func(false)) {
                    return;
                }
            }

            if (Back && down) {
                Back = false;
                SetCurrentOption(0);
                downHoldStart = Time.unscaledTime + 0.5f;
                if (currentOptionIndex == -1) {
                    SelectedTab.Highlighted();
                }
                return;
            }

            PauseOption currentOption = SelectedOption;
            if (up || down) {
                // Move between options
                int newOptionIndex = Mathf.Clamp(currentOptionIndex + (up ? -1 : 1), -1, SelectedTab.options.Count - 1);
                SetCurrentOption(newOptionIndex, true);
                if (up) {
                    upHoldStart = Time.unscaledTime + 0.5f;
                } else {
                    downHoldStart = Time.unscaledTime + 0.5f;
                }

            } else if (currentOption) {
                // Give this input to the option
                if (left) {
                    currentOption.OnLeftPress();
                } else {
                    currentOption.OnRightPress();
                }
            } else {
                // Move between tabs
                ChangeTab(left ? -1 : 1);
            }
            inputted = true;
        }

        public void OpenMenu() {
            gameObject.SetActive(true);

            foreach (PauseOptionTab tab in tabs) {
                tab.Deselected();
            }

            currentTabIndex = -1;
            currentOptionIndex = -1;
            Back = false;
            EnableInput = true;
            SetTab(0, false);
            //SetCurrentOption(0);

            QuantumGame game = QuantumRunner.DefaultGame;
            if (game != null) {
                foreach (var slot in game.GetLocalPlayerSlots()) {
                    game.SendCommand(slot, new CommandSetInSettings { InSettings = true });
                }
            }
        }

        public void CloseMenu() {
            if (SelectedTab) {
                SelectedTab.Deselected();
            }

            if (SelectedOption) {
                SelectedOption.Deselected();
            }

            EnableInput = false;
            gameObject.SetActive(false);
            GlobalController.Instance.PlaySound(SoundEffect.UI_Back);

            QuantumGame game = QuantumRunner.DefaultGame;
            if (game != null) {
                foreach (var slot in game.GetLocalPlayerSlots()) {
                    game.SendCommand(slot, new CommandSetInSettings { InSettings = false });
                }
            }
        }

        public void SetCurrentOption(int index, bool center = false) {
            if (currentOptionIndex == index || index >= SelectedTab.options.Count || index < -1) {
                return;
            }

            if (SelectedOption) {
                SelectedOption.Deselected();
            }

            int direction = index - currentOptionIndex;
            int original = currentOptionIndex;
            currentOptionIndex = index;

            while ((!SelectedOption || !SelectedOption.IsSelectable) && currentOptionIndex >= 0 && currentOptionIndex < SelectedTab.options.Count) {
                currentOptionIndex += direction;
            }

            if (currentOptionIndex >= SelectedTab.options.Count) {
                currentOptionIndex = original;
            }

            if (currentOptionIndex < -1) {
                currentOptionIndex = -1;
            }

            if (SelectedTab) {
                if (currentOptionIndex == -1) {
                    SelectedTab.Highlighted();
                } else {
                    SelectedTab.Unhighlighted();
                }
            }

            if (SelectedOption) {
                SelectedOption.Selected();
                if (center) {
                    // TODO: doesnt work smh. figure it out, future me.
                    scroll.verticalNormalizedPosition = scroll.ScrollToCenter(SelectedOption.rectTransform, false);
                }
            } else {
                // No selected option = tab selected
                if (center) {
                    Canvas.ForceUpdateCanvases();
                    scroll.verticalNormalizedPosition = 1;
                    Canvas.ForceUpdateCanvases();
                }
            }
        }

        public void SetCurrentOption(PauseOption option) {
            foreach (PauseOptionTab tab in tabs) {
                if (tab.options.Contains(option)) {
                    SetTab(tab);
                    SetCurrentOption(tabs[currentTabIndex].options.IndexOf(option));
                    break;
                }
            }
        }

        public void SetTab(int index, bool sound = true) {
            SetCurrentOption(-1);

            if (currentTabIndex == index) {
                return;
            }

            if (SelectedTab) {
                SelectedTab.Deselected();
            }

            currentTabIndex = index;

            if (SelectedTab) {
                SelectedTab.Selected();
            }

            if (sound) {
                GlobalController.Instance.PlaySound(SoundEffect.UI_Cursor);
            }

            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
        }

        public void SetTab(PauseOptionTab tab) {
            SetTab(tabs.IndexOf(tab));
        }
    }
}
