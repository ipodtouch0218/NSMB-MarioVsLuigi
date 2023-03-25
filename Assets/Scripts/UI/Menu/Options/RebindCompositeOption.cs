using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.UI.Pause.Options {
    public class RebindCompositeOption : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text label, buttonLabel;

        //---Private Variables
        private InputAction action;
        private int bindingIndex;

        public void OnClick() {

        }

        public void Instantiate(InputAction action, int bindingIndex) {
            this.action = action;
            this.bindingIndex = bindingIndex;

            label.text = action.bindings[bindingIndex].name;

            string button = InputControlPath.ToHumanReadableString(
                            action.bindings[bindingIndex].effectivePath,
                            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
            buttonLabel.text = button;
        }
    }
}
