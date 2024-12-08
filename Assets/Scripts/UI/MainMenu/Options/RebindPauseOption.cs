using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.UI.Pause.Options {

    public class RebindPauseOption : PauseOption {

        //---Static Variables
        private static int selectedIndex;

        //---Public Variables
        [HideInInspector] public InputAction action;

        //---Serialized Variables
        [SerializeField] private RebindPauseOptionButton[] buttons;
        [SerializeField] private PauseOptionControlsTab rebindManager;

        public override void Awake() {
            base.Awake();

            int indexCounter = 0;
            for (int i = 0; i < buttons.Length; i++) {
                buttons[i].action = action;

                while (action.bindings[indexCounter].isPartOfComposite)
                    indexCounter++;

                buttons[i].bindingIndex = indexCounter++;
            }

            translationKey = "ui.options.controls." + action.actionMap.name.ToLower().Replace(" ", "") + "." + action.name.ToLower().Replace(" ", "");
            //label.text = originalText;
        }

        public override void Selected() {
            base.Selected();
            buttons[selectedIndex].Selected();
        }

        public override void Deselected() {
            base.Deselected();

            foreach (var button in buttons) {
                button.Deselected();
            }
        }

        public override void OnDisable() {
            base.OnDisable();
            selectedIndex = 0;
        }

        public override void OnLeftPress() {
            SetCurrentOption(selectedIndex - 1);
        }

        public override void OnRightPress() {
            SetCurrentOption(selectedIndex + 1);
        }

        public override void OnClick() {
            OnButtonClick(buttons[selectedIndex]);
        }

        public void OnButtonClick(RebindPauseOptionButton button) {
            int index = Array.IndexOf(buttons, button);

            SetCurrentOption(index);

            // Start rebinding:
            rebindManager.StartRebind(button);
        }

        private void SetCurrentOption(int index) {
            if (index < 0 || index >= buttons.Length) {
                return;
            }

            foreach (var button in buttons) {
                button.Deselected();
            }

            selectedIndex = index;
            buttons[selectedIndex].Selected();
        }

        public void SetCurrentOption(RebindPauseOptionButton button) {
            SetCurrentOption(Array.IndexOf(buttons, button));
        }
    }
}
