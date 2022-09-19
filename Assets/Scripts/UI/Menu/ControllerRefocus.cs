using UnityEngine;
using UnityEngine.EventSystems;

// If there is no selected item, set the selected item to the event system's first selected item
public class ControllerRefocus : MonoBehaviour {

    private GameObject lastselect;

    public void Update () {
        if (EventSystem.current.currentSelectedGameObject) {
            lastselect = EventSystem.current.currentSelectedGameObject;
        } else {
            EventSystem.current.SetSelectedGameObject(lastselect);
        }
    }
}
