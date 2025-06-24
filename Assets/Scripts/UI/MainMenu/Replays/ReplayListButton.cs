using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus.Replays {
    public class ReplayListButton : MonoBehaviour, ISelectHandler {

        //---Serialized Variables
        [SerializeField] private ReplayListEntry entry;
        [SerializeField] private ReplayListManager handler;

        public void OnSelect(BaseEventData eventData) {
            handler.Select(entry, false);
        }
    }
}
