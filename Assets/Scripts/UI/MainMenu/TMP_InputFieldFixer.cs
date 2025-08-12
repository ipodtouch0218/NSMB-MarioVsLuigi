using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu {
    public class TMP_InputFieldFixer : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private float deadzone = 0.35f;

        //---Private Variables
        private GameObject selectedGameObject;
        private TMP_InputField selectedInputField;
        private bool activated;

        public void OnEnable() {
            Settings.Controls.UI.Navigate.performed += OnNavigate;
            Settings.Controls.UI.Navigate.canceled += OnNavigate;
        }

        public void OnDisable() {
            Settings.Controls.UI.Navigate.performed -= OnNavigate;
            Settings.Controls.UI.Navigate.canceled -= OnNavigate;
        }

        public void Update() {
            var eventSystem = EventSystem.current;
            if (eventSystem && selectedGameObject != eventSystem.currentSelectedGameObject) {
                selectedGameObject = eventSystem.currentSelectedGameObject;
                if (selectedGameObject) {
                    selectedGameObject.TryGetComponent(out selectedInputField);
                } else {
                    selectedInputField = null;
                }
            }
        }

        private readonly ProfilerMarker marker1 = new("TMP_InputFieldFixer.OnNavigate");
        private readonly ProfilerMarker marker2 = new("TMP_InputFieldFixer.OnNavigate.PerformNavigation");
        public void OnNavigate(InputAction.CallbackContext context) {
            using var x = marker1.Auto();
            if (!selectedInputField) {
                return;
            }

            var osk = OnScreenKeyboard.Instance;
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
                    using var z = marker2.Auto();

                    Selectable next;
                    if (y > 0) {
                        // up
                        next = selectedInputField.FindSelectableOnUp();
                    } else {
                        // down
                        next = selectedInputField.FindSelectableOnDown();
                    }

                    if (next) {
                        system.SetSelectedGameObject(next.gameObject);
                        if (next.TryGetComponent(out TMP_InputField nextInputField)) {
                            nextInputField.OnPointerClick(new PointerEventData(system));us
                        }
                        system.sendNavigationEvents = false;
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
