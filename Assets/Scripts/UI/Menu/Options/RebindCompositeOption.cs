using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace NSMB.UI.Pause.Options {
    public class RebindCompositeOption : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text label, buttonLabel;

        //---Private Variables
        private PauseOptionControlsTab tab;
        private RebindPauseOptionButton button;
        private int bindingIndex;

        public void OnClick() {
            tab.StartRebind(button, bindingIndex);
        }

        public void Instantiate(PauseOptionControlsTab tab, RebindPauseOptionButton button, InputAction action, int bindingIndex) {
            this.tab = tab;
            this.button = button;
            this.bindingIndex = bindingIndex;

            string name = action.bindings[bindingIndex].name;

            if (GlobalController.Instance.translationManager.TryGetTranslation($"ui.generic.{name}", out string translation)) {
                label.text = translation;
            } else {
                label.text = name;
            }

            string key = InputControlPath.ToHumanReadableString(
                            action.bindings[bindingIndex].effectivePath,
                            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
            buttonLabel.text = key;
        }
    }
}
