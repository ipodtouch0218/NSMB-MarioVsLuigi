using NSMB.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.Pause.Options {

    public class RebindCompositeOption : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text label, buttonLabel;
        [SerializeField] private Image image;
        [SerializeField] private Sprite selectedSprite, deselectedSprite;

        //---Private Variables
        private PauseOptionControlsTab tab;
        private RebindPauseOptionButton button;
        private InputAction action;
        private int bindingIndex;
        private bool selected;

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Selected() {
            selected = true;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void Hover() {
            image.sprite = selectedSprite;
            buttonLabel.color = Color.black;
        }

        public void Deselected() {
            selected = false;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void Dehover() {
            image.sprite = deselectedSprite;
            buttonLabel.color = Color.white;
        }

        public void OnClick() {
            tab.StartRebind(button, bindingIndex);
            tab.rebindCompositePrompt.Close(false);
        }

        public void Instantiate(PauseOptionControlsTab tab, RebindPauseOptionButton button, InputAction action, int bindingIndex) {
            this.tab = tab;
            this.button = button;
            this.action = action;
            this.bindingIndex = bindingIndex;

            string key = InputControlPath.ToHumanReadableString(
                            action.bindings[bindingIndex].effectivePath,
                            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
            key = key.Replace("Up Arrow", "↑").Replace("Down Arrow", "↓").Replace("Left Arrow", "←").Replace("Right Arrow", "→");

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

            if (selected) {
                label.text = "» " + label.text;
            }
        }
    }
}
