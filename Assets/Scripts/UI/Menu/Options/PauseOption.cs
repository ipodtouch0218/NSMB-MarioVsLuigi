using UnityEngine;
using TMPro;

using NSMB.Translation;
using NSMB.UI.Pause.Loaders;

namespace NSMB.UI.Pause.Options {
    public class PauseOption : MonoBehaviour {

        //---Public Variables
        public RectTransform rectTransform;

        //---Serialized Variables
        [SerializeField] private PauseOptionMenuManager manager;
        [SerializeField] internal TMP_Text label;
        [SerializeField] protected PauseOptionLoader loader;
        [SerializeField] public string translationKey;

        //---Properties
        public bool IsSelected { get; private set; }

        public virtual void OnValidate() {
            if (!manager) manager = GetComponentInParent<PauseOptionMenuManager>();
            if (!loader) loader = GetComponent<PauseOptionLoader>();
        }

        public virtual void Awake() {
            rectTransform = GetComponent<RectTransform>();
        }

        public virtual void OnEnable() {
            if (loader)
                loader.LoadOptions(this);

            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public virtual void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(TranslationManager tm) {
            if (IsSelected)
                Selected();
            else
                Deselected();
        }

        public virtual void Selected() {
            label.text = "» " + GetTranslatedString();
            label.isRightToLeftText = GlobalController.Instance.translationManager.RightToLeft;
            IsSelected = true;
        }

        public virtual void Deselected() {
            label.text = GetTranslatedString();
            label.isRightToLeftText = GlobalController.Instance.translationManager.RightToLeft;
            IsSelected = false;
        }

        public virtual void OnClick() { }
        public virtual void OnLeftPress() { }
        public virtual void OnLeftHeld() { }
        public virtual void OnRightPress() { }
        public virtual void OnRightHeld() { }

        private string GetTranslatedString() {
            return GlobalController.Instance.translationManager.GetTranslation(translationKey);
        }
    }
}
