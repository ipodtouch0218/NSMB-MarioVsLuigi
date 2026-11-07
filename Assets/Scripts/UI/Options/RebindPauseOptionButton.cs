using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.Options {
    public class RebindPauseOptionButton : MonoBehaviour {

        //---Public Variables
        [HideInInspector] public InputAction action;
        public RebindPauseOption parent;
        public int bindingIndex = -1;
        public InputBinding Binding => action.bindings[bindingIndex];

        //---Serialized Variables
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image image;
        [SerializeField] private Sprite selectedSprite, deselectedSprite;

        public void OnEnable() {
            UpdateLabel();
            parent.parent.OnDeleteModeChanged += OnDeleteModeChanged;
            OnDeleteModeChanged(parent.parent.DeleteMode);
        }

        public void OnDisable() {
            parent.parent.OnDeleteModeChanged -= OnDeleteModeChanged;
        }

        public void Selected() {
            label.color = Color.black;
            image.sprite = selectedSprite;
        }

        public void Deselected() {
            label.color = Color.white;
            image.sprite = deselectedSprite;
        }

        public void Hover() {
            if (Mouse.current.delta.value == Vector2.zero) {
                return;
            }

            if (parent.IsSelected) {
                parent.SetCurrentOption(this);
            } else {
                Selected();
            }
        }

        public void Dehover() {
            if (!parent.IsSelected) {
                Deselected();
            }
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

                    if (string.IsNullOrEmpty(addition)) {
                        addition = "\x01"; // "invalid" symbol
                    }
                    combined += addition + ",";
                }
                combined = combined.Replace("Up Arrow", "↑").Replace("Down Arrow", "↓").Replace("Left Arrow", "←").Replace("Right Arrow", "→");

                if (combined.Length > 11) {
                    label.text = "...";
                } else {
                    label.text = combined[..^1];
                }
            } else {
                label.text = InputControlPath.ToHumanReadableString(
                        targetBinding.effectivePath,
                        InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
            }
        }

        private void OnDeleteModeChanged(bool deleteMode) {
            image.color = deleteMode ? Color.red : Color.white;
        }
    }
}
