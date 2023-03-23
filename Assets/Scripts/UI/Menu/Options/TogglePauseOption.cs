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

        public override void Awake() {
            base.Awake();

            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        public override void OnClick() {
            toggle.isOn = !toggle.isOn;
        }

        public void OnToggleValueChanged(bool value) {
            if (loader)
                loader.OnValueChanged(this, value);

            Settings.Instance.SaveSettings();
        }
    }
}
