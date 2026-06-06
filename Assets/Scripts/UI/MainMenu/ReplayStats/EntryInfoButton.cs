using TMPro;
using UnityEngine;
using NSMB.Replay.Stats;

namespace NSMB.UI.MainMenu.Submenus.RoomList {
    public class EntryInfoButton : MonoBehaviour {
        [SerializeField] private TMP_Text tooltipText;
        [SerializeField] private TMP_Text labelText;

        private TimePointEntry timePointEntry;

        public void Initialize(TimePointEntry timePointEntry, string text, string label) {
            this.timePointEntry = timePointEntry;

            tooltipText.SetText(text);
            labelText.SetText(label);
        }
    }
}
