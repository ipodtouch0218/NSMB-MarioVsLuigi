using TMPro;
using UnityEngine;
using NSMB.Replay.Stats;

namespace NSMB.UI.MainMenu.Submenus.RoomList {
    public class EntryInfoButton : MonoBehaviour {
        [SerializeField] private TMP_Text tooltipText;

        private TimePointEntry timePointEntry;

        public void Initialize(TimePointEntry timePointEntry, TimePoint.DisplayArgs displayArg) {
            this.timePointEntry = timePointEntry;
            this.gameObject.SetActive(true);

            tooltipText.text = timePointEntry.timePoint.GetTooltip(GlobalController.Instance.translationManager, displayArg);
        }
    }
}
