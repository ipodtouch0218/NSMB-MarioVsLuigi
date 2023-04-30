using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace NSMB.UI.Pause.Options {
    public class RebindPauseOptionButton : MonoBehaviour {

        //---Public Variables
        [HideInInspector] public InputAction action;
        public int bindingIndex = -1;
        public InputBinding Binding => action.bindings[bindingIndex];

        //---Serialized Variables
        [SerializeField] private TMP_Text label;

        public void OnEnable() {
            UpdateLabel();
        }

        public void Selected() {

        }

        public void Deselected() {

        }

        public void UpdateLabel() {
            InputBinding targetBinding = Binding;

            if (targetBinding.isComposite) {
                string combined = "";
                int count = bindingIndex;
                while ((targetBinding = action.bindings[++count]).isPartOfComposite) {
                    string addition = InputControlPath.ToHumanReadableString(
                            targetBinding.effectivePath,
                            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);

                    combined += addition + ",";
                }
                if (combined == "Up Arrow,Down Arrow,Left Arrow,Right Arrow,") {
                    combined = "Arrow Keys,";
                }
                if (combined.Length > 11)
                    combined = "....";

                label.text = combined[..^1];
            } else {
                label.text = InputControlPath.ToHumanReadableString(
                        targetBinding.effectivePath,
                        InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
            }
        }
    }
}
