using UnityEngine;
using TMPro;

using NSMB.Translation;

namespace NSMB.UI {

    [RequireComponent(typeof(TMP_Text))]
    public class LoadStringFromFile : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TextAsset source;
        [SerializeField] private TMP_Text text;

        public void OnValidate() {
            if (!text) text = GetComponentInParent<TMP_Text>();
        }

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(TranslationManager tm) {
            text.text = tm.GetSubTranslations(source.text);
            //text.isRightToLeftText = tm.RightToLeft;
        }
    }
}
