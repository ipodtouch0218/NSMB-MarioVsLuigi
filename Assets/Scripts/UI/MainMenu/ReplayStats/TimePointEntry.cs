using NSMB.Replay;
using NSMB.Replay.Stats;
using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.RoomList {
    public class TimePointEntry : MonoBehaviour {
        //---Public Variables
        public TimePoint timePoint;

        //---Serialized Variables
        [SerializeField] private TMP_Text entryNumText, timeText, symbolsText, descriptionText, additionalText;
        [SerializeField] private Image icon;
        [Header("Info")]
        [SerializeField] private EntryInfoButton infoButton;

        //---Private Variables
        private readonly StringBuilder stringBuilder = new();

        public void OnValidate() {
            this.SetIfNull(ref icon);
        }

        public void UpdateUI(TimePoint timePointInfo, int entryNum, TimePoint.DisplayArgs displayArg) {
            timePoint = timePointInfo;

            TranslationManager tm = GlobalController.Instance.translationManager;

            entryNumText.text = entryNum.ToString();
            
            //--printing the time
            stringBuilder.Clear();
            timePointInfo.SetTimeText(tm, stringBuilder, displayArg);
            timeText.SetText(stringBuilder);

            //--setting the symbols
            stringBuilder.Clear();
            timePointInfo.SetSymbolsText(tm, stringBuilder, displayArg);
            symbolsText.SetText(stringBuilder);

            //--setting the description
            stringBuilder.Clear();
            timePointInfo.SetDescriptionText(tm, stringBuilder, displayArg);
            descriptionText.SetText(stringBuilder);

            //--setting the additional info
            stringBuilder.Clear();
            timePointInfo.SetAdditionalText(tm, stringBuilder, displayArg);
            additionalText.SetText(stringBuilder);

            var tooltip = timePointInfo.GetTooltip(tm, displayArg);
            if (tooltip != null) {
                infoButton.Initialize(this, displayArg);
            } else {
                infoButton.gameObject.SetActive(false);
            }
        }

        #region Button Methods

        public void StartReplayAtPart() {
            ActiveReplayManager.Instance.StartReplayPlayback(timePoint.StatsRecorder.ReplayFile, timePoint.OccurenceFrame - timePoint.StatsRecorder.ReplayStart, timePoint.PlayerRef, true);
        }

        #endregion
    }
}
