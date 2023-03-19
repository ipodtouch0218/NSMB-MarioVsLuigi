using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.Pause.Options {
    public class PauseOptionMenuManager : Selectable {

        //---Static Variables
        public static PauseOptionMenuManager Instance;

        //---Serialzied Variables
        [SerializeField] private List<PauseOptionTab> tabs;
        [SerializeField] private ScrollRect scroll;

        [SerializeField] private Image backButton;
        [SerializeField] private Sprite backSelectedSprite, backDeselectedSprite;

        //---Private Variables
        [SerializeField] private int currentTabIndex;
        [SerializeField] private int currentOptionIndex; //-1 = tabs are selected
        private bool inputted;
        private GameObject previouslySelected;

        //---Private Propreties
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

        protected override void Awake() {
            base.Awake();
            Instance = this;
        }

        protected override void OnEnable() {
            base.OnEnable();
            if (!Application.isPlaying)
                return;

            ControlSystem.controls.UI.Navigate.performed += OnNavigate;
            ControlSystem.controls.UI.Navigate.canceled += OnNavigate;
            ControlSystem.controls.UI.Cancel.performed += OnCancel;
            ControlSystem.controls.UI.Submit.performed += OnSubmit;

            if (EventSystem.current) {
                previouslySelected = EventSystem.current.currentSelectedGameObject;
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        protected override void OnDisable() {
            base.OnDisable();
            if (!Application.isPlaying)
                return;

            ControlSystem.controls.UI.Navigate.performed -= OnNavigate;
            ControlSystem.controls.UI.Navigate.canceled -= OnNavigate;
            ControlSystem.controls.UI.Cancel.performed -= OnCancel;
            ControlSystem.controls.UI.Submit.performed -= OnSubmit;

            if (EventSystem.current) {
                EventSystem.current.SetSelectedGameObject(previouslySelected);
            }
        }

        private void OnCancel(InputAction.CallbackContext context) {
            if (Back) {
                CloseMenu();
                return;
            }

            Back = true;
            SetCurrentOption(-1);
            SelectedTab.Unhighlighted();
            GlobalController.Instance.PlaySound(Enums.Sounds.UI_Cursor);
        }

        private void OnSubmit(InputAction.CallbackContext context) {
            if (Back) {
                CloseMenu();
                return;
            }

            if (!SelectedOption)
                return;

            SelectedOption.OnClick();
        }

        private void OnNavigate(InputAction.CallbackContext context) {

            if (context.canceled) {
                inputted = false;
                return;
            }

            if (inputted)
                return;

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

            if (Back && (down || right)) {
                Back = false;
                if (right) {
                    if (currentTabIndex != 0)
                        SetTab(0, false);
                    else
                        SelectedTab.Highlighted();
                }
                if (down) {
                    SetCurrentOption(0);
                    if (currentOptionIndex == -1)
                        SelectedTab.Highlighted();
                }
                GlobalController.Instance.PlaySound(Enums.Sounds.UI_Cursor);
                return;
            }

            PauseOption currentOption = SelectedOption;
            if (up || down) {
                // Move between options
                int newOptionIndex = Mathf.Clamp(currentOptionIndex + (up ? -1 : 1), -1, SelectedTab.options.Count - 1);
                SetCurrentOption(newOptionIndex);

            } else if (currentOption) {
                // Give this input to the option
                if (left)
                    currentOption.OnLeftPress();
                else
                    currentOption.OnRightPress();

            } else {
                // Move between tabs
                if (left && currentTabIndex == 0) {
                    // Enable the back button
                    Back = true;
                    SetCurrentOption(-1);
                    SelectedTab.Unhighlighted();
                    GlobalController.Instance.PlaySound(Enums.Sounds.UI_Cursor);
                }
                int newTabIndex = Mathf.Clamp(currentTabIndex + (left ? -1 : 1), 0, tabs.Count - 1);
                SetTab(newTabIndex);
            }
            inputted = true;
        }

        public void OpenMenu() {
            gameObject.SetActive(true);

            foreach (PauseOptionTab tab in tabs)
                tab.Deselected();

            currentTabIndex = -1;
            currentOptionIndex = -1;
            Back = false;
            SetTab(0, false);
            //SetCurrentOption(0);
        }

        public void CloseMenu() {
            if (SelectedTab)
                SelectedTab.Deselected();

            if (SelectedOption)
                SelectedOption.Deselected();

            gameObject.SetActive(false);
            GlobalController.Instance.PlaySound(Enums.Sounds.UI_Back);
        }

        public void SetCurrentOption(int index) {
            if (currentOptionIndex == index || index >= SelectedTab.options.Count || index < -1)
                return;

            if (SelectedOption)
                SelectedOption.Deselected();

            int direction = index - currentOptionIndex;
            int original = currentOptionIndex;
            currentOptionIndex = index;

            while ((!SelectedOption || SelectedOption is NonselectableOption) && index >= 0 && index < SelectedTab.options.Count) {
                currentOptionIndex += direction;
            }

            if (currentOptionIndex >= SelectedTab.options.Count)
                currentOptionIndex = original;

            if (currentOptionIndex < -1)
                currentOptionIndex = -1;

            if (SelectedTab) {
                if (currentOptionIndex == -1) {
                    SelectedTab.Highlighted();
                } else {
                    SelectedTab.Unhighlighted();
                }
            }

            if (SelectedOption)
                SelectedOption.Selected();
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

            if (currentTabIndex == index)
                return;

            if (SelectedTab)
                SelectedTab.Deselected();

            currentTabIndex = index;

            if (SelectedTab)
                SelectedTab.Selected();

            if (sound)
                GlobalController.Instance.PlaySound(Enums.Sounds.UI_Cursor);

            scroll.verticalNormalizedPosition = 0;
        }

        public void SetTab(PauseOptionTab tab) {
            SetTab(tabs.IndexOf(tab));
        }
    }
}
