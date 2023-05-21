using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

using NSMB.Extensions;
using NSMB.Game;
using NSMB.Translation;

namespace NSMB.UI.Pause {
    public class PauseMenuManager : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private PauseMenuOptionWrapper[] options;
        [SerializeField] private Material enabledMaterial, disabledMaterial;

        //---Private Variables
        private bool inputted;
        private int selected;
        private bool skipSound;
        private bool isHost;

        public void OnEnable() {
            ControlSystem.controls.UI.Navigate.performed += OnNavigate;
            ControlSystem.controls.UI.Navigate.canceled += OnNavigate;
            ControlSystem.controls.UI.Submit.performed += OnSubmit;
            ControlSystem.controls.UI.Cancel.performed += OnCancel;
            GlobalController.Instance.translationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);

            skipSound = true;
            isHost = NetworkHandler.Runner && NetworkHandler.Runner.GetLocalPlayerData().IsRoomOwner;
            options[1].text.fontMaterial = isHost ? enabledMaterial : disabledMaterial;
            SelectOption(0);
        }

        public void OnDisable() {
            ControlSystem.controls.UI.Navigate.performed -= OnNavigate;
            ControlSystem.controls.UI.Navigate.canceled -= OnNavigate;
            ControlSystem.controls.UI.Submit.performed -= OnSubmit;
            ControlSystem.controls.UI.Cancel.performed -= OnCancel;
            GlobalController.Instance.translationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void OnNavigate(InputAction.CallbackContext context) {
            if (context.canceled) {
                inputted = false;
                return;
            }

            if (GlobalController.Instance.optionsManager.gameObject.activeSelf)
                return;

            Vector2 input = context.ReadValue<Vector2>();
            if (input.y > 0 && !inputted) {
                IncrementOption(-1);
            } else if (input.y < 0 && !inputted) {
                IncrementOption(1);
            }
            inputted = true;
        }

        public void OnSubmit(InputAction.CallbackContext context) {
            if (GlobalController.Instance.optionsManager.gameObject.activeSelf)
                return;

            options[selected].trigger.OnPointerClick(null);
        }

        public void OnCancel(InputAction.CallbackContext context) {
            if (GlobalController.Instance.optionsManager.gameObject.activeSelf)
                return;

            GameManager.Instance.Pause(false);
        }

        public void IncrementOption(int increment) {
            int newIndex = selected + increment;

            if (newIndex == 1 && !isHost)
                newIndex += increment;

            if (newIndex < 0 || newIndex >= options.Length)
                return;

            SelectOption(newIndex);
        }

        public void SelectOption(int index) {
            if (selected == index || selected < 0 || selected >= options.Length || (index == 1 && !isHost)) {
                skipSound = false;
                return;
            }

            selected = index;
            UpdateLabels(selected);

            if (!skipSound)
                GlobalController.Instance.PlaySound(Enums.Sounds.UI_Cursor);
            skipSound = false;
        }

        public void SelectOption(TMP_Text option) {
            skipSound = true;
            int index = -1;
            for (int i = 0; i < options.Length; i++) {
                if (options[i].text == option) {
                    index = i;
                    break;
                }
            }
            SelectOption(index);
        }

        private void UpdateLabels(int selected) {
            for (int i = 0; i < options.Length; i++) {
                PauseMenuOptionWrapper option = options[i];
                option.text.text = (selected == i) ? ("» " + option.originalText + " «") : option.originalText;
            }
        }

        private void OnLanguageChanged(TranslationManager tm) {
            foreach (PauseMenuOptionWrapper option in options) {
                option.originalText = tm.GetTranslation(option.translationKey);
            }
            UpdateLabels(selected);
        }

        [Serializable]
        public class PauseMenuOptionWrapper {
            public TMP_Text text;
            public EventTrigger trigger;
            public string translationKey;
            [NonSerialized] public string originalText;
        }
    }
}
