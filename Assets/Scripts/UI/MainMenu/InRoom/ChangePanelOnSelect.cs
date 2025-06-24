using NSMB.Utilities.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class ChangePanelOnSelect : MonoBehaviour, ISelectHandler {

        //---Serialized Variables
        [SerializeField] private InRoomSubmenu roomMenu;
        [SerializeField] private InRoomSubmenuPanel panelToSelect;

        public void OnValidate() {
            this.SetIfNull(ref roomMenu, UnityExtensions.GetComponentType.Parent);
        }

        public void OnSelect(BaseEventData eventData) {
            roomMenu.SelectPanel(panelToSelect, false);
        }
    }
}
