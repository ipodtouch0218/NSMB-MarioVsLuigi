using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ColorButton : MonoBehaviour, ISelectHandler, IDeselectHandler {

    [SerializeField] Image shirt, overalls, overlay;
    public CustomColors.PlayerColor palette;
    public Color deselectedColor, selectedColor;

    public void Instantiate() {
        shirt.color = palette.hat;
        overalls.color = palette.overalls;
        overlay.color = deselectedColor;
    }

    public void OnSelect(BaseEventData eventData) {
        overlay.color = selectedColor;
    }

    public void OnDeselect(BaseEventData eventData) {
        overlay.color = deselectedColor;
    }
}