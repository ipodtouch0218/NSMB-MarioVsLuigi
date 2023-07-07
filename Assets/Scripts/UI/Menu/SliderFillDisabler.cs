using UnityEngine;
using UnityEngine.UI;

public class SliderFillDisabler : MonoBehaviour {

    [SerializeField] private Slider slider;

    [SerializeField] private Image image;
    [SerializeField] private Sprite enabledSprite, disabledSprite;

    public void Update() {
        image.sprite = slider.IsInteractable() ? enabledSprite : disabledSprite;
    }
}
