using NSMB.UI.Translation;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.ReplayStats {

    public class StatsList : MonoBehaviour {
        //---property
        public ReplayStatsManager.ListType ListType { get; private set; }
        public int Value {
            get => dropdownValues[dropdown.value];
            set => dropdown.value = value;
        }

        //---serialized
        [SerializeField] private ReplayStatsManager statsManager;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Dropdown dropdown;

        //---private variables
        private string translationKey;
        private readonly List<int> dropdownValues = new();

        public void Initialize(string translationKey, int value, ReplayStatsManager.ListType listType) {
            this.translationKey = translationKey;
            ListType = listType;
            dropdown.SetValueWithoutNotify(value);

            if (!statsManager.IsReady) {
                dropdown.interactable = false;
            }

            TranslationManager tm = GlobalController.Instance.translationManager;
            TranslationManager.OnLanguageChanged += UpdateLabel;

            UpdateLabel(tm);
        }

        public void AddToDropdown(string text, int value) {
            dropdown.options.Add(new TMP_Dropdown.OptionData { text = text });
            dropdownValues.Add(value);
        }

        public void UpdateLabel(TranslationManager tm) {
            label.text = tm.GetTranslation(translationKey);
        }
    }
}
