using NSMB.Extensions;
using NSMB.Translation;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class SelectablePromptLabel : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private TMP_Text label;
        [SerializeField] private List<GameObject> selectionTargets;

        [SerializeField] private bool changeText = true;
        [SerializeField] public string translationKey;
        [SerializeField] private bool twoSided;

        //---Private Variables
        private bool selected;
        private string originalText;

        public void OnValidate() {
            this.SetIfNull(ref label);
            if (!eventSystem) {
                eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            }
        }

        public void OnEnable() {
            eventSystem = FindFirstObjectByType<EventSystem>();
            TranslationManager.OnLanguageChanged += OnLanguageChanged;

            originalText ??= label.text;
            UpdateLabel();
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void LateUpdate() {
            bool currentlySelected = selectionTargets.Contains(eventSystem.currentSelectedGameObject);
            
            if (!selected && currentlySelected) {
                Select();
            } else if (selected && !currentlySelected) {
                Deselect();
            }
        }

        public void Select() {
            selected = true;
            UpdateLabel();
        }

        public void Deselect() {
            selected = false;
            UpdateLabel();
        }

        public void UpdateLabel() {
            if (changeText) {
                if (string.IsNullOrWhiteSpace(translationKey)) {
                    label.text = originalText;
                } else {
                    label.text = GlobalController.Instance.translationManager.GetTranslation(translationKey);
                }

                if (selected) {
                    if (twoSided) {
                        label.text = "» " + label.text + " «";
                    } else {
                        label.text = "» " + label.text;
                    }
                }
            } else {
                label.enabled = selected;
            }
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateLabel();
        }
    }
}
