using NSMB.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;

public class ChangePanelOnSelect : MonoBehaviour, ISelectHandler {

    //---Serialized Variables
    [SerializeField] private InRoomMenu roomMenu;
    [SerializeField] private InRoomPanel panelToSelect;

    public void OnValidate() {
        this.SetIfNull(ref roomMenu, UnityExtensions.GetComponentType.Parent);
    }

    public void OnSelect(BaseEventData eventData) {
        roomMenu.SelectPanel(panelToSelect, false);
    }
}
