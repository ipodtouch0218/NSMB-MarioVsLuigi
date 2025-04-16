using UnityEngine;
using UnityEngine.EventSystems;

public class ReplayListButton : MonoBehaviour, ISelectHandler {

    //---Serialized Variables
    [SerializeField] private ReplayListEntry entry;
    [SerializeField] private ReplayListManager handler;

    public void OnSelect(BaseEventData eventData) {
        handler.UpdateInformation(entry.Replay);
        handler.Selected = entry;
    }
}
