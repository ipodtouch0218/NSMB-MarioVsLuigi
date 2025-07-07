using NSMB.UI.Options.Loaders;
using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using TMPro;
using UnityEngine;

namespace NSMB.UI.Options {
    public class PauseOption : MonoBehaviour {

        //---Properties
#if PLATFORM_WEBGL
        public virtual bool IsSelectable => !hideOnWebGL;
#else
        public virtual bool IsSelectable => true;
#endif

        //---Serialized Variables
        [SerializeField] internal PauseOptionMenuManager manager;
        [SerializeField] internal TMP_Text label;
        [SerializeField] protected PauseOptionLoader loader;
        [SerializeField] public string translationKey;
        [SerializeField] internal bool hideOnWebGL, requireReconnect;

        //---Properties
        public bool IsSelected { get; private set; }

        public virtual void OnValidate() {
            this.SetIfNull(ref manager, UnityExtensions.GetComponentType.Parent);
            this.SetIfNull(ref loader);
        }

        public virtual void OnEnable() {
            if (loader) {
                loader.LoadOptions(this);
            }

            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public virtual void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public virtual void UpdateLabel() {
            if (!label) {
                return;
            }

            bool rtl = GlobalController.Instance.translationManager.RightToLeft;
            if (IsSelected) {
                if (rtl) {
                    label.text = GetTranslatedString() + " «";
                } else {
                    label.text = "» " + GetTranslatedString();
                }
            } else {
                label.text = GetTranslatedString();
            }
            label.horizontalAlignment = rtl ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
        }

        public virtual void Selected() {
            IsSelected = true;
            UpdateLabel();
        }

        public virtual void Deselected() {
            IsSelected = false;
            UpdateLabel();
        }

        private string GetTranslatedString() {
            return GlobalController.Instance.translationManager.GetTranslation(translationKey);
        }

        public virtual void OnClick() { }
        public virtual void OnLeftPress() { }
        public virtual void OnLeftHeld() { }
        public virtual void OnRightPress() { }
        public virtual void OnRightHeld() { }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateLabel();
        }
    }
}
