using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;

namespace NSMB.UI.Options {
    public class ResetControlsButton : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private PauseOptionControlsTab tab;

        [Preserve]
        public void Click() {
            var controls = Settings.Controls.asset;
            for (int i = 0; i < controls.actionMaps.Count; i++) {
                InputActionMap map = controls.actionMaps[i];

                // Controls starting with '!' are not rebindable
                if (map.name.StartsWith("!")) {
                    continue;
                }

                foreach (InputAction action in map.actions) {
                    // Controls starting with '!' are not rebindable
                    if (action.name.StartsWith("!")) {
                        continue;
                    }

                    action.RemoveAllBindingOverrides();
                }
            }

            foreach (PauseOption option in tab.options) {
                if (option is RebindPauseOption controlButton) {
                    foreach (RebindPauseOptionButton bindingButton in controlButton.Children) {
                        bindingButton.UpdateLabel();
                    }
                }
            }

            Settings.Instance.SaveSettings();
            GlobalController.Instance.PlaySound(SoundEffect.UI_Decide);
        }
    }
}