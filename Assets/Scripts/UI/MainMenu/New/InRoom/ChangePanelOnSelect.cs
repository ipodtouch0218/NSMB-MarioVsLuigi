using NSMB.Extensions;
using NSMB.UI.MainMenu.Submenus;
using UnityEngine;
using UnityEngine.EventSystems;

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
