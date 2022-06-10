using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ColorButton : MonoBehaviour, ISelectHandler, IDeselectHandler {

    [SerializeField] Image shirt, overalls, overlay;
    public CustomColors.PlayerColor palette;
    [SerializeField] Sprite overlayUnpressed, overlayPressed;

    public void Instantiate() {
        shirt.color = palette.hat;
        overalls.color = palette.overalls;
        overlay.enabled = false;
    }

    public void OnSelect(BaseEventData eventData) {
        overlay.enabled = true;
        overlay.sprite = overlayUnpressed;
    }

    public void OnDeselect(BaseEventData eventData) {
        overlay.enabled = false;
        overlay.sprite = overlayUnpressed;
    }

    public void OnPress() {
        overlay.sprite = overlayPressed;
    }
}