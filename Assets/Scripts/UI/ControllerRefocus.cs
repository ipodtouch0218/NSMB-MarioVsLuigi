using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI {
    public class ControllerRefocus : MonoBehaviour {

        //---Private Variables
        private GameObject lastSelection;

        public void Update() {
            if (EventSystem.current.currentSelectedGameObject) {
                // If there is no selected item, set the selected item to the event system's first selected item
                lastSelection = EventSystem.current.currentSelectedGameObject;
            } else {
                EventSystem.current.SetSelectedGameObject(lastSelection);
            }
        }
    }
}
