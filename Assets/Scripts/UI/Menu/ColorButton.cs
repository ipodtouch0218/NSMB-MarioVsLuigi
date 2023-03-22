using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ColorButton : MonoBehaviour, ISelectHandler, IDeselectHandler {

    [SerializeField] private Sprite overlayUnpressed, overlayPressed;
    [SerializeField] private Image shirt, overalls, overlay;
    public GameObject ColorNameString;

    public PlayerColorSet palette;

    public void Instantiate(CharacterData player) {
        if (palette == null) {
            shirt.enabled = false;
            overalls.enabled = false;
            return;
        }

        PlayerColors col = palette.GetPlayerColors(player);
        shirt.color = col.hatColor;
        overalls.color = col.overallsColor;
        overlay.enabled = false;
    }

    public void OnSelect(BaseEventData eventData) {
        overlay.enabled = true;
        overlay.sprite = overlayUnpressed;
        ColorNameString.GetComponent<TMP_Text>().text = palette.ColorText;
    }

    public void OnDeselect(BaseEventData eventData) {
        overlay.enabled = false;
        overlay.sprite = overlayUnpressed;
    }

    public void OnPress() {
        overlay.sprite = overlayPressed;
        ColorNameString.GetComponent<TMP_Text>().text = palette.ColorText;
    }
}
