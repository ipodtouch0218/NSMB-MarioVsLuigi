using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updates the sprite every frame. Disgusting way of doing this.
/// </summary>
public class SliderFillDisabler : MonoBehaviour {

    // --- Serialized Variables
    [SerializeField] private Slider slider;

    [SerializeField] private Image image;
    [SerializeField] private Sprite enabledSprite, disabledSprite;

    public void Update() {
        image.sprite = slider.IsInteractable() ? enabledSprite : disabledSprite;
    }
}
