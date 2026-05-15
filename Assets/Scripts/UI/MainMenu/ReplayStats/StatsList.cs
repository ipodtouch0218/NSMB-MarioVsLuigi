using NSMB.UI.Translation;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.ReplayStats {

    public class StatsList : MonoBehaviour {
        //---property
        public int value => dropdownValues[dropdown.value];

        //---serialized
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Dropdown dropdown;

        //---private variables
        private string translationKey;
        private List<int> dropdownValues = new();

        public void Initialize(string translationKey, int value) {
            this.translationKey = translationKey;
            dropdown.SetValueWithoutNotify(value);

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
