using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace NSMB.UI.Pause.Options {
    public class ScrollablePauseOption : PauseOption {

        //---Public Variables
        public int currentOptionIndex;
        public List<string> options;

        //---Serialized Variables
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private TMP_Text display;

        //---Events
        public UnityEvent OnValueChanged;

        public void Awake() {
            if (!loader)
                ChangeIndex(0);
        }

        public override void Selected() {
            base.Selected();
            ChangeIndex(currentOptionIndex);
        }

        public override void OnLeftPress() {
            ChangeIndex(currentOptionIndex - 1);
        }

        public override void OnRightPress() {
            ChangeIndex(currentOptionIndex + 1);
        }

        public void ChangeIndex(int newIndex) {
            int previous = currentOptionIndex;
            currentOptionIndex = Mathf.Clamp(newIndex, 0, options.Count - 1);

            display.text = options.Count > 0 ? options[currentOptionIndex] : "No options set.";
            leftButton.interactable = currentOptionIndex != 0;
            rightButton.interactable = currentOptionIndex != options.Count - 1;

            if (previous != currentOptionIndex)
                OnValueChanged?.Invoke();
        }
    }
}
