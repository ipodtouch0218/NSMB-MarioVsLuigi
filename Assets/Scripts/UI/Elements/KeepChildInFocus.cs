using JimmysUnityUtilities;
using NSMB.Utilities.Extensions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.Elements {
    [RequireComponent(typeof(ScrollRect))]
    public class KeepChildInFocus : MonoBehaviour, IScrollHandler, IPointerMoveHandler {

        //---Serialized Variables
        [SerializeField] private float scrollAmount = 15;

        //---Private Variables
        private readonly List<ScrollRect> components = new();
        private ScrollRect rect;
        private GameObject previousObject;
        private EventSystem eventSystem;
        private bool usingMouse;

        public void Awake() {
            this.SetIfNull(ref rect);
        }

        public void OnEnable() {
            eventSystem = EventSystem.current;
        }

        public void OnDisable() {
            usingMouse = false;
        }

        public void Update() {
            if (!rect.content) {
                return;
            }

            GameObject obj = eventSystem.currentSelectedGameObject;
            if (obj != previousObject) {
                usingMouse = false;
                previousObject = obj;
            }

            if (usingMouse || !obj) {
                return;
            }

            RectTransform target = (RectTransform) obj.transform;
            if (IsFirstParent(target) && !target.TryGetComponentInParent(out Scrollbar _)) {
                rect.verticalNormalizedPosition = Mathf.Lerp(rect.verticalNormalizedPosition, rect.ScrollIntoView(target, true, 32), scrollAmount * Time.deltaTime);
            }
        }

        private bool IsFirstParent(Transform target) {
            do {
                if (target.TryGetComponent(out IFocusIgnore _)) {
                    return false;
                }

                target.GetComponents(components);

                if (components.Count >= 1) {
                    return components.Contains(rect);
                }
            } while (target = target.parent);

            return false;
        }

        public void OnScroll(PointerEventData eventData) {
            usingMouse = true;
        }

        public void OnPointerMove(PointerEventData eventData) {
            usingMouse = true;
        }

        public interface IFocusIgnore { }
    }
}
