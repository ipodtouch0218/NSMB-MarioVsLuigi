using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UnclickableButton : Button {
    public override void OnPointerClick(PointerEventData eventData) {
        // Nothing
    }
}
