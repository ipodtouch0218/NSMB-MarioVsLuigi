using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu {
    public class TMP_InputFieldFixer : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private float deadzone = 0.35f;

        //---Private Variables
        private bool activated;

        public void OnEnable() {
            Settings.Controls.UI.Navigate.performed += OnNavigate;
            Settings.Controls.UI.Navigate.canceled += OnNavigate;
        }

        public void OnDisable() {
            Settings.Controls.UI.Navigate.performed -= OnNavigate;
            Settings.Controls.UI.Navigate.canceled -= OnNavigate;
        }

        public void OnNavigate(InputAction.CallbackContext context) {
            var osk = FindFirstObjectByType<OnScreenKeyboard>();
            if (osk && osk.IsOpen) {
                return;
            }

            Vector2 vec = context.ReadValue<Vector2>();
            float y = vec.y;
            EventSystem system = EventSystem.current;

            // "context.control.name.Length != 1" is bullshit... i don't trust this.
            if (Mathf.Abs(y) > deadzone && context.control.name.Length != 1) {
                if (!activated) {
                    // https://discussions.unity.com/t/tab-between-input-fields/547817/10
                    if (system.currentSelectedGameObject && system.currentSelectedGameObject.TryGetComponent(out TMP_InputField selected)) {
                        Selectable next;
                        if (y > 0) {
                            // up
                            next = selected.FindSelectableOnUp();
                        } else {
                            // down
                            next = selected.FindSelectableOnDown();
                        }

                        if (next) {
                            system.SetSelectedGameObject(next.gameObject);
                            if (next.TryGetComponent(out TMP_InputField nextInputField)) {
                                nextInputField.OnPointerClick(new PointerEventData(system));
                            }
                            system.sendNavigationEvents = false;
                        }
                    }
                    activated = true;
                }
            } else {
                activated = false;
                system.sendNavigationEvents = true;
            }
        }
    }
}
