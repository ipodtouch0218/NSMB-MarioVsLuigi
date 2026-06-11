using NSMB.UI.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.ReplayStats {
    public class StatsButton : MonoBehaviour {
        [SerializeField] private ReplayStatsManager statsManager;
        [SerializeField] private TMP_Text label;
        private string translationString;
        public int Index { get; private set; }

        public void Initialize(int index, string translationString) {
            this.Index = index;
            this.translationString = translationString;

            TranslationManager.OnLanguageChanged += UpdateUI;
            CheckButtonState();
        }

        public void UpdateUI(TranslationManager tm) {
            label.text = tm.GetTranslation(translationString);
        }

        public bool CheckButtonState() {
            var button = GetComponent<Button>();
            if (statsManager.selectedButton == Index) {
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
            var tm = GlobalController.Instance.translationManager;
            statsManager.selectedButton = Index;
            statsManager.ResetListValues();
            statsManager.UpdateLists(tm);
            statsManager.ResetStatsDropdownPos();
            statsManager.UpdateStatsDropdown(tm);
            statsManager.ChangedViewingStats(true);
        }
    }
}
