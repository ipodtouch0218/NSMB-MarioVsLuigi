using UnityEngine;
using UnityEngine.EventSystems;

// If there is no selected item, set the selected item to the event system's first selected item
public class ControllerRefocus : MonoBehaviour {
    GameObject lastselect;
     
    void Start() {
        lastselect = new GameObject();
    }
    
    void Update () {
        if (EventSystem.current.currentSelectedGameObject == null) {
            EventSystem.current.SetSelectedGameObject(lastselect);
        } else {
            lastselect = EventSystem.current.currentSelectedGameObject;
        }
    }
}
