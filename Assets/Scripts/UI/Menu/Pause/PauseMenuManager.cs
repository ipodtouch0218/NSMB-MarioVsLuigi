using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

using NSMB.Extensions;
using UnityEditor.Rendering.LookDev;

namespace NSMB.UI.Pause {
    public class PauseMenuManager : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text[] options;

        //---Private Variables
        private string[] originalText;
        private EventTrigger[] triggers;
        private bool inputted;
        private int selected;
        private bool skipSound;
        private bool isHost;

        public void Initialize() {
            originalText = new string[options.Length];
            triggers = new EventTrigger[options.Length];

            for (int i = 0; i < options.Length; i++) {
                originalText[i] = options[i].text;
                triggers[i] = options[i].GetComponent<EventTrigger>();
            }
        }

        public void OnEnable() {
            ControlSystem.controls.UI.Navigate.performed += OnNavigate;
            ControlSystem.controls.UI.Navigate.canceled += OnNavigate;
            ControlSystem.controls.UI.Submit.performed += OnSubmit;
            ControlSystem.controls.UI.Cancel.performed += OnCancel;

            if (originalText == null || originalText.Length == 0)
                Initialize();

            skipSound = true;
            isHost = NetworkHandler.Runner && NetworkHandler.Runner.GetLocalPlayerData().IsRoomOwner;
            SelectOption(0);
        }

        public void OnDisable() {
            ControlSystem.controls.UI.Navigate.performed -= OnNavigate;
            ControlSystem.controls.UI.Navigate.canceled -= OnNavigate;
            ControlSystem.controls.UI.Submit.performed -= OnSubmit;
            ControlSystem.controls.UI.Cancel.performed -= OnCancel;
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

            triggers[selected].OnPointerClick(null);
        }

        public void OnCancel(InputAction.CallbackContext context) {
            if (GlobalController.Instance.optionsManager.gameObject.activeSelf)
                return;

            GameManager.Instance.Pause(false);
        }

        public void IncrementOption(int increment) {
            int newIndex = selected + increment;

            if (newIndex == 1 && true || !isHost)
                newIndex += increment;

            if (newIndex < 0 || newIndex >= options.Length)
                return;

            SelectOption(newIndex);
        }

        public void SelectOption(int index) {
            for (int i = 0; i < options.Length; i++) {
                options[i].text = originalText[i];
            }
            options[index].text = "» " + originalText[index] + " «";
            selected = index;

            if (!skipSound)
                GlobalController.Instance.PlaySound(Enums.Sounds.UI_Cursor);
            skipSound = false;
        }

        public void SelectOption(TMP_Text option) {
            SelectOption(Array.IndexOf(options, option));
        }
    }
}
