using NSMB.UI.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.ReplayStats {
    public class StatsButton : MonoBehaviour {
        [SerializeField] private ReplayStatsManager statsManager;
        [SerializeField] private TMP_Text label;
        private string translationString;
        public int index { get; private set; }

        public void Initialize(int index, string translationString) {
            this.index = index;
            this.translationString = translationString;

            TranslationManager.OnLanguageChanged += UpdateUI;
            CheckButtonState();
        }

        public void UpdateUI(TranslationManager tm) {
            label.text = tm.GetTranslation(translationString);
        }

        public bool CheckButtonState() {
            var button = GetComponent<Button>();
            if (statsManager.selectedButton == index) {
                button.interactable = false;
                return false;
            } else {
                button.interactable = true;
                return true;
            }
        }

        public void SwitchButtonStates() {
            var otherButtons = statsManager.statsButtons;
            GetComponent<Button>().interactable = false;
            foreach (var statButton in otherButtons) {
                if (statButton == this) {
                    continue;
                }
                var button = statButton.GetComponent<Button>();
                button.interactable = true;
            }
            statsManager.selectedButton = index;
            statsManager.UpdateStatsDropdown(GlobalController.Instance.translationManager);
            statsManager.ChangedViewingStats(true);
            statsManager.ResetStatsDropdownPos();
        }
    }
}
