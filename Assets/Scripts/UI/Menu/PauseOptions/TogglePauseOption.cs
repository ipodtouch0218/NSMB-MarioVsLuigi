using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Pause.Options {
    public class TogglePauseOption : PauseOption {

        //---Serialized Variables
        [SerializeField] private Toggle toggle;

        public override void OnValidate() {
            base.OnValidate();
            if (!toggle) toggle = GetComponentInChildren<Toggle>();
        }

        public override void OnClick() {
            toggle.isOn = !toggle.isOn;
        }
    }
}
