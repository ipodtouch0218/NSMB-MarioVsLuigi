using NSMB.Utilities.Extensions;
using TMPro;
using UnityEngine;

namespace NSMB.UI.Translation {

    [RequireComponent(typeof(TMP_Text))]
    public class TMP_Translatable : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] public string key;
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
            Run();
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Run() {
            OnLanguageChanged(GlobalController.Instance.translationManager);
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
