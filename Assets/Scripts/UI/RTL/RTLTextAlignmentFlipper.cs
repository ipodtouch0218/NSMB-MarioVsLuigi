using TMPro;
using UnityEngine;

namespace NSMB.UI.RTL {
    [RequireComponent(typeof(TMP_Text))]
    public class RTLTextAlignmentFlipper : RTLComponentFlipper<TMP_Text> {

        //---Private Variables
        private HorizontalAlignmentOptions orignialAlignment;

        public override void Awake() {
            base.Awake();
            orignialAlignment = component.horizontalAlignment;
        }

        protected override void ApplyDirection(bool rtl) {
            if (orignialAlignment == HorizontalAlignmentOptions.Left) {
                component.horizontalAlignment = rtl ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
            } else if (orignialAlignment == HorizontalAlignmentOptions.Right) {
                component.horizontalAlignment = rtl ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right;
            }
        }
    }
}