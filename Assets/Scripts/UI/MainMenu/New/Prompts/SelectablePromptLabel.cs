using NSMB.Extensions;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class SelectablePromptLabel : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text label;
        [SerializeField] private List<GameObject> selectionTargets;

        [SerializeField] private bool changeText = true;
        [SerializeField] private string translationKey;
        [SerializeField] private bool twoSided;

        //---Private Variables
        private bool selected;

        public void OnValidate() {
            this.SetIfNull(ref label);
        }

        public void Update() {
            bool currentlySelected = selectionTargets.Contains(EventSystem.current.currentSelectedGameObject);
            
            if (!selected && currentlySelected) {
                Select();
            } else if (selected && !currentlySelected) {
                Deselect();
            }
        }

        public void Select() {
            if (changeText) {
                if (twoSided) {
                    label.text = "» " + GlobalController.Instance.translationManager.GetTranslation(translationKey) + " «";
                } else {
                    label.text = "» " + GlobalController.Instance.translationManager.GetTranslation(translationKey);
                }
            } else {
                label.enabled = true;
            }
            selected = true;
        }

        public void Deselect() {
            if (changeText) {
                label.text = GlobalController.Instance.translationManager.GetTranslation(translationKey);
            } else {
                label.enabled = false;
            }
            selected = false;
        }
    }
}
