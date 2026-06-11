using NSMB.UI.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class ChangeTextOnSelect : MonoBehaviour, ISelectHandler, IDeselectHandler {

        //---Serialized Variables
        [SerializeField] protected TMP_Text text;
        [SerializeField] protected string translationKey;

        //---Protected Variables
        protected bool selected;

        public virtual void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public virtual void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void ApplyText() {
            text.text = GetText();
        }

        public virtual string GetText() {
            return GlobalController.Instance.translationManager.GetTranslation(translationKey);
        }

        void ISelectHandler.OnSelect(BaseEventData eventData) {
            selected = true;
            ApplyText();
        }

        void IDeselectHandler.OnDeselect(BaseEventData eventData) {
            selected = false;
        }

        private void OnLanguageChanged(TranslationManager tm) {
            ApplyText();
        }
    }
}