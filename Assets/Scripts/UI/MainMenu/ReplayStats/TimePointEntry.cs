using NSMB.Replay.Stats;
using NSMB.UI.Translation;
using NSMB.Utilities;
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
        [SerializeField] private TMP_Text entryNumText, timeText, symbolsText, descriptionText, additionalInfo;
        [SerializeField] private Image icon;

        //---Private Variables
        private StringBuilder stringBuilder = new();

        public void OnValidate() {
            this.SetIfNull(ref icon);
        }

        public void UpdateUI(TimePoint timePointInfo, int entryNum) {
            timePoint = timePointInfo;

            TranslationManager tm = GlobalController.Instance.translationManager;
            ReplayStatsRecorder stats = ReplayStatsRecorder.Instance;

            entryNumText.text = entryNum.ToString();
            
            //--printing the time
            stringBuilder.Clear();
            stringBuilder.Append($"@ {EventManager.FrameToTime(timePoint.OccurenceFrame, stats.ReplayStart, timePoint.DeltaTime)}");

            if (timePoint.EndFrame != null) {
                stringBuilder.Append($"~{EventManager.FrameToTime(timePoint.EndFrame.Value, stats.ReplayStart, timePoint.DeltaTime)}");
            }

            stringBuilder.Append($" - F{timePoint.OccurenceFrame - stats.ReplayStart}");

            if (timePoint.EndFrame != null) {
                stringBuilder.Append($"~F{timePoint.EndFrame - stats.ReplayStart}");
            }

            timeText.SetText(stringBuilder);

            //--setting the symbols
            stringBuilder.Clear();
            if (timePoint is PointStarCollected starPoint) {
                stringBuilder.Append("<sprite name=room_stars>").Append(Utils.GetSymbolString(starPoint.StarCount.ToString(), Utils.smallSymbols));
            }

            symbolsText.SetText(stringBuilder);
            stringBuilder.Clear();

            if (timePoint is PointStarCollected starPoint2) {
                stringBuilder.Append($"Collected star having {starPoint2.TotalStarCount} total");
            }


            descriptionText.SetText(stringBuilder);
        }
    }
}
