using NSMB.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu {
    public class BackButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {

        //---Properties
        public bool BackHeld {
            get => backHeldViaButton || backHeldViaClick;
            set => backHeldViaButton = backHeldViaClick = value;
        }

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private Image goBackImageFill, goBackButton;
        [SerializeField] private AudioSource backButtonSfx;
        [SerializeField] private AudioClip growClip, shrinkClip;
        [SerializeField] private Color clickColor;

        //---Private Variables
        private float timer, backHoldRequirement;
        private bool backHeldViaButton, backHeldViaClick;
        private Color originalColor;

        public void OnValidate() {
            this.SetIfNull(ref canvas, UnityExtensions.GetComponentType.Parent);
        }

        public void Start() {
            growClip.LoadAudioData();
            shrinkClip.LoadAudioData();
            originalColor = goBackButton.color;
        }

        public void OnEnable() {
            ControlSystem.controls.UI.Cancel.performed += OnCancel;
            ControlSystem.controls.UI.Cancel.canceled += OnCancel;
        }

        public void OnDisable() {
            ControlSystem.controls.UI.Cancel.performed -= OnCancel;
            ControlSystem.controls.UI.Cancel.canceled -= OnCancel;

            BackHeld = false;
            timer = 0;
        }

        public void Update() {
            if (canvas.SubmenuStack.Count <= 1) {
                return;
            }

            if (BackHeld && GlobalController.Instance.optionsManager.IsActive()) {
                BackHeld = false;
            }

            if (BackHeld) {
                backHoldRequirement = canvas.SubmenuStack[^1].BackHoldTime;
                if (!backButtonSfx.isPlaying || backButtonSfx.clip != growClip) {
                    backButtonSfx.Stop();
                    backButtonSfx.clip = growClip;
                    backButtonSfx.Play();
                    backButtonSfx.time = timer;
                }

                if ((timer += Time.deltaTime) >= backHoldRequirement) {
                    canvas.GoBack();
                    BackHeld = false;
                    timer = 0;
                    backButtonSfx.Stop();
                }
                goBackButton.color = clickColor;

            } else {
                if (timer > 0) {
                    if (!backButtonSfx.isPlaying || (backButtonSfx.time > 0.2f && backButtonSfx.clip != shrinkClip)) {
                        backButtonSfx.Stop();
                        backButtonSfx.clip = shrinkClip;
                        backButtonSfx.Play();
                    }

                    if ((timer -= Time.deltaTime) <= 0) {
                        timer = 0;
                        backButtonSfx.Stop();
                    }
                }
                goBackButton.color = originalColor;
            }

            goBackImageFill.fillAmount = backHoldRequirement > 0 ? (timer / backHoldRequirement) : 0;
        }

        //---Callbacks
        public void OnPointerDown(PointerEventData eventData) {
            backHeldViaClick = true;

        }

        public void OnPointerUp(PointerEventData eventData) {
            backHeldViaClick = false;
        }

        public void OnPointerExit(PointerEventData eventData) {
            backHeldViaClick = false;
        }

        private void OnCancel(InputAction.CallbackContext context) {
            GameObject currentObject = EventSystem.current.currentSelectedGameObject;
            TMP_InputField inputField;
            bool isInsideTextbox = currentObject && (inputField = currentObject.GetComponentInParent<TMP_InputField>()) && inputField.isFocused;
            if (context.performed && !GlobalController.Instance.optionsManager.IsActive() && !isInsideTextbox) {
                backHeldViaButton = true;
            } else if (context.canceled) {
                backHeldViaButton = false;
            }
        }
    }
}
