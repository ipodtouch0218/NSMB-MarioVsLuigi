using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Elements {
    public class SliderFillDisabler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private Slider slider;

        [SerializeField] private Image image;
        [SerializeField] private Sprite enabledSprite, disabledSprite;

        public void Update() {
            // Updates the sprite every frame. Disgusting way of doing this.
            image.sprite = slider.IsInteractable() ? enabledSprite : disabledSprite;
        }
    }
}
