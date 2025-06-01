using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.RTL {
    [RequireComponent(typeof(HorizontalLayoutGroup))]
    public class RTLLayoutGroupFlipper : RTLComponentFlipper<HorizontalLayoutGroup> {

        //---Serialized Variables
        [SerializeField] private bool flipChildrenNavigation, immediateChildrenOnly;

        //---Private Variables
        private bool isFlippedByDefault;
        private Dictionary<Selectable, Navigation> originalChildrenNavigations = new();

        public override void Awake() {
            base.Awake();
            isFlippedByDefault = component.reverseArrangement;
        }

        protected override void ApplyDirection(bool rtl) {
            component.reverseArrangement = rtl ? !isFlippedByDefault : isFlippedByDefault;

            if (flipChildrenNavigation) {
                Selectable[] children = GetComponentsInChildren<Selectable>();
                foreach (var child in children) {
                    if (immediateChildrenOnly && child.transform.parent != transform
                        || child.navigation.mode != Navigation.Mode.Explicit) {
                        continue;
                    }
                    originalChildrenNavigations[child] = child.navigation;
                }

                foreach ((var child, var navigation) in originalChildrenNavigations) {
                    Navigation newNav = navigation;
                    if (rtl) {
                        (newNav.selectOnLeft, newNav.selectOnRight) = (newNav.selectOnRight, newNav.selectOnLeft);
                    }
                    child.navigation = newNav;
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) component.transform);
        }
    }
}
