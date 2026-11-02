using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NSMB.UI.Options {
    public class ButtonPauseOption : PauseOption {

        //---Serialized Variables
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite selectedSprite, deselectedSprite;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private UnityEvent action;

        public override void OnDisable() {
            base.OnDisable();
            OnCursorExit();
        }

        public void OnCursorEnter() {
            backgroundImage.sprite = selectedSprite;
            buttonLabel.color = Color.black;
        }

        public void OnCursorExit() {
            backgroundImage.sprite = deselectedSprite;
            buttonLabel.color = Color.white;
        }

        public override void OnClick() {
            manager.SetCurrentOption(this);
            action.Invoke();
        }
    }
}
