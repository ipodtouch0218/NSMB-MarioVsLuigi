using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Pause.Options {
    public class TogglePauseOption : PauseOption {

        //---Serialized Variables
        [SerializeField] public Toggle toggle;

        public override void OnValidate() {
            base.OnValidate();
            if (!toggle) toggle = GetComponentInChildren<Toggle>();
        }

        public void Awake() {
            toggle.onValueChanged.AddListener(CallOnChanged);
        }

        public override void OnClick() {
            toggle.isOn = !toggle.isOn;
            CallOnChanged(toggle.isOn);
        }

        public void CallOnChanged(bool value) {
            if (loader)
                loader.OnValueChanged(this, !value);
        }
    }
}
