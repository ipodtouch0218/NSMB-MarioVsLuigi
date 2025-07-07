using UnityEngine.UI;

namespace NSMB.UI.RTL {
    public class RTLSliderFlipper : RTLComponentFlipper<Slider> {

        //---Private Variables
        private Slider.Direction originalDirection;

        public override void Awake() {
            base.Awake();
            originalDirection = component.direction;
        }

        protected override void ApplyDirection(bool rtl) {
            if (originalDirection == Slider.Direction.LeftToRight) {
                component.SetDirection(rtl ? Slider.Direction.RightToLeft : Slider.Direction.LeftToRight, true);
            } else if (originalDirection == Slider.Direction.RightToLeft) {
                component.SetDirection(rtl ? Slider.Direction.LeftToRight : Slider.Direction.RightToLeft, true);
            }
        }
    }
}