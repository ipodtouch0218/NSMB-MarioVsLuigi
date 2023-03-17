using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace NSMB.UI.Pause.Options {
    public class ScrollablePauseOption : PauseOption {

        //---Public Variables
        public int value;
        public List<string> options;

        //---Serialized Variables
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private TMP_Text display;

        //---Events
        public UnityEvent OnValueChanged;

        public void Awake() {
            if (!loader)
                SetValue(0, false);
        }

        public override void Selected() {
            base.Selected();
            SetValue(value, false);
        }

        public override void OnLeftPress() {
            SetValue(value - 1);
        }

        public override void OnRightPress() {
            SetValue(value + 1);
        }

        public void SetValue(int newIndex, bool callback = true) {
            int previous = value;
            value = Mathf.Clamp(newIndex, 0, options.Count - 1);

            display.text = options.Count > 0 ? options[value] : "No options set.";
            leftButton.interactable = value != 0;
            rightButton.interactable = value != options.Count - 1;

            if (callback && previous != value) {
                OnValueChanged?.Invoke();

                if (loader)
                    loader.OnValueChanged(this, value);

                Settings.Instance.SaveSettings();
            }
        }
    }
}
