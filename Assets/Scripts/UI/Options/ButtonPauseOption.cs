using NSMB.UI.Elements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Options {
    public class ButtonPauseOption : PauseOption {

        //---Serialized Variables
        [SerializeField] private Clickable button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite selectedSprite, deselectedSprite;
        [SerializeField] private TMP_Text buttonLabel;

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
            base.OnClick();
            button.Click();
        }
    }
}
