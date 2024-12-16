using UnityEngine;
using TMPro;
using NSMB.Extensions;

namespace NSMB.Translation {

    [RequireComponent(typeof(TMP_Text))]
    public class TMP_Translatable : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private string key;
        [SerializeField] private TMP_Text text;

        //---Private Variables
        private HorizontalAlignmentOptions originalTextAlignment;

        public void Awake() {
            originalTextAlignment = text.horizontalAlignment;
        }

        public void OnValidate() {
            this.SetIfNull(ref text);
        }

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(TranslationManager tm) {
            text.text = tm.GetTranslation(key);

            if (originalTextAlignment == HorizontalAlignmentOptions.Left) {
                text.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
            } else if (originalTextAlignment == HorizontalAlignmentOptions.Right) {
                text.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right;
            }
        }
    }
}
