using NSMB.Replay;
using NSMB.Replay.Stats;
using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.RoomList {
    public class TimePointEntry : MonoBehaviour {
        //---Public Variables
        public TimePoint timePoint;

        //---Properties
        public string EntryInfo { get; private set; }
        public TimePoint.DisplayArgs DisplayArg;

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
            DisplayArg = displayArg;

            TranslationManager tm = GlobalController.Instance.translationManager;

            EntryInfo = timePointInfo.GetEntryNum(entryNum, displayArg);
            entryNumText.SetText(EntryInfo);

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
            var tooltipLabel = timePointInfo.GetTooltipLabel(tm, displayArg);
            if (!string.IsNullOrEmpty(tooltip)) {
                infoButton.Initialize(this, tooltip, tooltipLabel);
            } else {
                infoButton.gameObject.SetActive(false);
            }
        }

        #region Button Methods

        public void StartReplayAtPart() {
            int targetFrame = timePoint.OccurenceFrame - timePoint.StatsRecorder.ReplayStart - timePoint.FrameOffset;
            ActiveReplayManager.Instance.StartReplayPlayback(timePoint.StatsRecorder.ReplayFile, Math.Clamp(targetFrame, 0, timePoint.StatsRecorder.ReplayEnd), true, timePoint.GetCameraPos(DisplayArg), timePoint);
        }

        #endregion
    }
}
