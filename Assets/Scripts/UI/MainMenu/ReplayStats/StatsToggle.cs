using NSMB.UI.Elements;
using NSMB.UI.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.ReplayStats {
    public class StatsToggle : MonoBehaviour {
        //---property
        public bool Value {
            get => toggle.isOn;
            set => toggle.isOn = value;
        }

        //---serialized
        [SerializeField] private TMP_Text label;
        [SerializeField] private SpriteChangingToggle toggle;

        //---private variables
        private string translationKey;

        public void Initialize(string translationKey, bool value) {
            this.translationKey = translationKey;
            toggle.SetIsOnWithoutNotify(value);

            TranslationManager tm = GlobalController.Instance.translationManager;
            TranslationManager.OnLanguageChanged += UpdateLabel;

            UpdateLabel(tm);
        }

        public void UpdateLabel(TranslationManager tm) {
            label.text = tm.GetTranslation(translationKey);
        }
    }
}
