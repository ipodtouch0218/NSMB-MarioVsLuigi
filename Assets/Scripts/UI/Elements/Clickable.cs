using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NSMB.UI.Elements {
    public class Clickable : MonoBehaviour, IPointerDownHandler, IPointerExitHandler, IPointerUpHandler {

        //---Public Variables
        public UnityEvent OnClick;
        public bool Interactable = true, RepeatOnHold = false, OnRelease = false;

        //---Private Variables
        private bool held;
        private float holdTime;

        public void Update() {
            if (held) {
                if (!Interactable || !RepeatOnHold) {
                    held = false;
                    return;
                }

                if ((holdTime += Time.unscaledDeltaTime) > 0.5f) {
                    OnClick?.Invoke();
                    holdTime = 0.4f;
                }
            }
        }

        public void Click() {
            OnClick?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData) {
            if (Interactable && !OnRelease) {
                Click();
                held = true;
                holdTime = 0;
            }
        }

        public void OnPointerExit(PointerEventData eventData) {
            held = false;
        }

        public void OnPointerUp(PointerEventData eventData) {
            if (Interactable && OnRelease) {
                Click();
            }
            held = false;
        }
    }
}
