using UnityEngine;
using UnityEngine.UI;

using NSMB.Extensions;

namespace NSMB.UI.Pause.Options {
    public class TogglePauseOption : PauseOption {

        //---Serialized Variables
        [SerializeField] public Toggle toggle;

        public override void OnValidate() {
            base.OnValidate();
            this.SetIfNull(ref toggle, UnityExtensions.GetComponentType.Children);
        }

        public override void Awake() {
            base.Awake();

            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        public override void OnClick() {
            toggle.isOn = !toggle.isOn;
        }

        public void OnToggleValueChanged(bool value) {
            if (loader) {
                loader.OnValueChanged(this, value);
            }

            Settings.Instance.SaveSettings();
        }
    }
}
