using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI {
    public class ControllerRefocus : MonoBehaviour {

        //---Private Variables
        private GameObject lastSelection;

        public void Update() {
            var system = EventSystem.current;
            if (!system) {
                return;
            }

            if (system.currentSelectedGameObject) {
                // If there is no selected item, set the selected item to the event system's first selected item
                lastSelection = system.currentSelectedGameObject;
            } else if (lastSelection && lastSelection.activeInHierarchy) {
                Debug.Log("current selected game object is null! switching to last object, " + lastSelection);
                system.SetSelectedGameObject(lastSelection);
            }
        }
    }
}
