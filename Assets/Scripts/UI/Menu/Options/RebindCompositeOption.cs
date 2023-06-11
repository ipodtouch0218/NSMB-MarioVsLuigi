using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

using NSMB.Translation;

namespace NSMB.UI.Pause.Options {

    public class RebindCompositeOption : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text label, buttonLabel;

        //---Private Variables
        private PauseOptionControlsTab tab;
        private RebindPauseOptionButton button;
        private InputAction action;
        private int bindingIndex;

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void OnClick() {
            tab.StartRebind(button, bindingIndex);
        }

        public void Instantiate(PauseOptionControlsTab tab, RebindPauseOptionButton button, InputAction action, int bindingIndex) {
            this.tab = tab;
            this.button = button;
            this.action = action;
            this.bindingIndex = bindingIndex;

            string key = InputControlPath.ToHumanReadableString(
                            action.bindings[bindingIndex].effectivePath,
                            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
            buttonLabel.text = key;

            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        private void OnLanguageChanged(TranslationManager tm) {
            string name = action.bindings[bindingIndex].name;

            if (tm.TryGetTranslation($"ui.generic.{name}", out string translation)) {
                label.text = translation;
            } else {
                label.text = name;
            }
        }
    }
}
