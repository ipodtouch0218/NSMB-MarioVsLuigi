using UnityEngine;
using TMPro;

namespace NSMB.Translation {

    [RequireComponent(typeof(TMP_Text))]
    public class TMP_SubTranslatable : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text text;

        //---Private Variables
        private string originalText;
        private HorizontalAlignmentOptions originalTextAlignment;

        public void OnValidate() {
            if (!text) text = GetComponent<TMP_Text>();
        }

        public void Awake() {
            originalText = text.text;
            originalTextAlignment = text.horizontalAlignment;
        }

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(TranslationManager tm) {
            text.text = tm.GetSubTranslations(originalText);

            if (originalTextAlignment == HorizontalAlignmentOptions.Left) {
                text.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
            } else if (originalTextAlignment == HorizontalAlignmentOptions.Right) {
                text.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right;
            }
        }
    }
}
