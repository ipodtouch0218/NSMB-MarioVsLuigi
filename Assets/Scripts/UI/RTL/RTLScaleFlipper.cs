using UnityEngine;

namespace NSMB.UI.RTL {
    public class RTLScaleFlipper : RTLComponentFlipper<Transform> {

        //---Private Variables
        private float originalXScale;

        public override void Awake() {
            base.Awake();
            originalXScale = component.localScale.x;
        }

        protected override void ApplyDirection(bool rtl) {
            Vector3 scale = component.localScale;
            scale.x = rtl ? -originalXScale : originalXScale;
            component.localScale = scale;
        }
    }
}