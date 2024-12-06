using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ColorButton : MonoBehaviour, ISelectHandler {

    //---Public Variables
    public PlayerColorSet palette;

    //---Serialized Variables
    [SerializeField] private TMP_Text colorNameString;
    [SerializeField] private Image shirt, overalls;

    public void Instantiate(CharacterAsset player) {
        if (palette == null) {
            shirt.enabled = false;
            overalls.enabled = false;
            return;
        }

        PlayerColors col = palette.GetPlayerColors(player);
        shirt.color = col.shirtColor;
        overalls.color = col.overallsColor;
    }

    public void OnSelect(BaseEventData eventData) {
        colorNameString.text = palette ? palette.Name : GlobalController.Instance.translationManager.GetTranslation("skin.default");
    }

    public void OnPress() {
        colorNameString.text = palette ? palette.Name : GlobalController.Instance.translationManager.GetTranslation("skin.default");
    }
}
