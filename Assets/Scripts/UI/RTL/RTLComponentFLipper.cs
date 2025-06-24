using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using UnityEngine;

namespace NSMB.UI.RTL {
    public abstract class RTLComponentFlipper<T> : MonoBehaviour where T : Component {

        //---Private Variables
        protected T component;

        public virtual void Awake() {
            this.SetIfNull(ref component);
        }

        public virtual void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            TryApplyDirection();
        }

        public virtual void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
            TryApplyDirection();
        }

        public void TryApplyDirection() {
#if UNITY_EDITOR
            if (!GlobalController.Instance || !GlobalController.Instance.translationManager) {
                return;
            }
#endif
            ApplyDirection(GlobalController.Instance.translationManager.RightToLeft);
        }

        protected abstract void ApplyDirection(bool rtl);

        private void OnLanguageChanged(TranslationManager tm) {
            ApplyDirection(tm.RightToLeft);
        }
    }
}
