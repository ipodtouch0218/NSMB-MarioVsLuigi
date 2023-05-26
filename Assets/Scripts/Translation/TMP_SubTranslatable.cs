using TMPro;
using UnityEngine;

namespace NSMB.Translation {

    [RequireComponent(typeof(TMP_Text))]
    public class TMP_SubTranslatable : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text text;

        //---Private Variables
        private string originalText;

        public void OnValidate() {
            if (!text) text = GetComponent<TMP_Text>();
        }

        public void Awake() {
            originalText = text.text;
        }

        public void OnEnable() {
            GlobalController.Instance.translationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void OnDisable() {
            GlobalController.Instance.translationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(TranslationManager tm) {
            text.text = tm.GetSubTranslations(originalText);
            text.isRightToLeftText = tm.RightToLeft;
        }
    }
}
