using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.UI.Pause.Options {
    public class RebindOption : PauseOption {

        //---Public Variables
        [HideInInspector] public InputAction action;

        //---Serialized Variables
        [SerializeField] private RebindOptionButton[] buttons;

        //---Private Variables
        private int selectedIndex;

        public void Awake() {
            int indexCounter = 0;
            for (int i = 0; i < buttons.Length; i++) {
                buttons[i].action = action;

                while (action.bindings[indexCounter].isPartOfComposite)
                    indexCounter++;

                buttons[i].bindingIndex = indexCounter++;
            }

            originalText = action.name;
            label.text = originalText;
        }

        public override void OnEnable() {
            SetCurrentOption(0);
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

        public void OnButtonClick(RebindOptionButton button) {
            int index = Array.IndexOf(buttons, button);

            SetCurrentOption(index);
            //if (button.IsComposite) {
            //    // Open prompt for composite bindings
            //} else {
            //    // Immediately open rebind prompt
            //}
        }

        private void SetCurrentOption(int index) {
            if (index < 0 || index >= buttons.Length)
                return;

            buttons[selectedIndex].Deselected();
            selectedIndex = index;
            buttons[selectedIndex].Selected();
        }
    }
}
