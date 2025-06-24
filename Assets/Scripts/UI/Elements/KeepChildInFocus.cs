using JimmysUnityUtilities;
using NSMB.Utilities.Extensions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.Elements {
    [RequireComponent(typeof(ScrollRect))]
    public class KeepChildInFocus : MonoBehaviour, IScrollHandler {

        //---Serialized Variables
        [SerializeField] private float scrollAmount = 15;

        //---Private Variables
        private readonly List<ScrollRect> components = new();
        private ScrollRect rect;
        private float scrollPos = 0;
        private GameObject previousObject;
        private bool scrolled;

        public void Awake() {
            this.SetIfNull(ref rect);
        }

        public void Update() {
            if (!rect.content) {
                return;
            }

            rect.verticalNormalizedPosition = Mathf.Lerp(rect.verticalNormalizedPosition, scrollPos, scrollAmount * Time.deltaTime);

            GameObject obj = EventSystem.current.currentSelectedGameObject;
            if (obj != previousObject) {
                scrolled = false;
                previousObject = obj;
            }
            if (!obj) {
                return;
            }

            RectTransform target = (RectTransform) obj.transform;
            if (!scrolled && IsFirstParent(target) && !target.TryGetComponentInParent(out Scrollbar _)) {
                scrollPos = rect.ScrollIntoView(target, true, 32);
            } else {
                scrollPos = rect.verticalNormalizedPosition;
            }
        }

        private bool IsFirstParent(Transform target) {
            do {
                if (target.GetComponent<IFocusIgnore>() != null) {
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
            scrolled = true;
        }

        public interface IFocusIgnore { }
    }
}
