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

            entryNumText.text = entryNum.ToString();
            
            //--printing the time
            stringBuilder.Clear();
            timePointInfo.SetTimeText(tm, stringBuilder);
            timeText.SetText(stringBuilder);

            //--setting the symbols
            stringBuilder.Clear();
            timePointInfo.SetSymbolsText(tm, stringBuilder);
            symbolsText.SetText(stringBuilder);

            //--setting the description
            stringBuilder.Clear();
            timePointInfo.SetDescriptionText(tm, stringBuilder);
            descriptionText.SetText(stringBuilder);

            //--setting the additional info
            stringBuilder.Clear();
            timePointInfo.SetAdditionalText(tm, stringBuilder);
            descriptionText.SetText(stringBuilder);
        }
    }
}
