using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ColorButton : MonoBehaviour, ISelectHandler, IDeselectHandler {

    //---Public Variables
    public PlayerColorSet palette;

    //---Serialized Variables
    [SerializeField] private TMP_Text colorNameString;
    [SerializeField] private Sprite overlayUnpressed, overlayPressed;
    [SerializeField] private Image shirt, overalls, overlay;

    public void Instantiate(CharacterData player) {
        if (palette == null) {
            shirt.enabled = false;
            overalls.enabled = false;
            return;
        }

        PlayerColors col = palette.GetPlayerColors(player);
        shirt.color = col.shirtColor;
        overalls.color = col.overallsColor;
        overlay.enabled = false;
    }

    public void OnSelect(BaseEventData eventData) {
        overlay.enabled = true;
        overlay.sprite = overlayUnpressed;
        colorNameString.text = palette ? palette.Name : GlobalController.Instance.translationManager.GetTranslation("skin.default");
    }

    public void OnDeselect(BaseEventData eventData) {
        overlay.enabled = false;
        overlay.sprite = overlayUnpressed;
    }

    public void OnPress() {
        overlay.sprite = overlayPressed;
        colorNameString.text = palette ? palette.Name : GlobalController.Instance.translationManager.GetTranslation("skin.default");
    }
}
