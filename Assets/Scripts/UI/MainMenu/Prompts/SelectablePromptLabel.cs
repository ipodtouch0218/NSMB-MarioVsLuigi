using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
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
        [SerializeField] private bool changeColor = false;
        [SerializeField] private Color selectedColor = Color.white, deselectedColor = Color.gray;
        [SerializeField] public string translationKey;
        [SerializeField] private bool twoSided;

        //---Private Variables
        private bool selected;
        private string originalText;
        private HorizontalAlignmentOptions originalAlignment;

        public void OnValidate() {
            this.SetIfNull(ref label);
            if (!eventSystem) {
                eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            }
        }

        public void Awake() {
            originalAlignment = label.horizontalAlignment;
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
            TranslationManager tm = GlobalController.Instance.translationManager;
            bool rtl = tm.RightToLeft;
            if (changeText) {
                string text;
                if (string.IsNullOrWhiteSpace(translationKey)) {
                    text = originalText;
                } else {
                    text = tm.GetTranslation(translationKey);
                }

                if (selected) {
                    if (twoSided) {
                        text = "» " + text + " «";
                    } else {
                        if (rtl) {
                            text = text + " «";
                        } else {
                            text = "» " + text;
                        }
                    }
                }
                label.text = text;
            } else {
                label.enabled = selected;
            }

            if (changeColor) {
                label.color = selected ? selectedColor : deselectedColor;
            }

            if (originalAlignment == HorizontalAlignmentOptions.Left) {
                label.horizontalAlignment = rtl ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
            } else if (originalAlignment == HorizontalAlignmentOptions.Right) {
                label.horizontalAlignment = rtl ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right;
            }
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateLabel();
        }
    }
}
