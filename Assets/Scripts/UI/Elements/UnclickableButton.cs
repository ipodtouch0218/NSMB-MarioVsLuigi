using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Elements {
    public class UnclickableButton : Button {
        public override void OnPointerClick(PointerEventData eventData) {
            // Nothing
        }
    }
}
