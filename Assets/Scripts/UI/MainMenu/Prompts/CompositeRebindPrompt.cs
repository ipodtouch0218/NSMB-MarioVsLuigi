using NSMB.Translation;
using NSMB.UI.Pause.Options;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.UI.Prompts {

    public class CompositeRebindPrompt : UIPrompt {

        //---Serialized Variables
        [SerializeField] private RebindCompositeOption optionTemplate;
        [SerializeField] private TMP_Text header, backText;
        [SerializeField] private Transform content;

        //---Properties
        private int MaxIndex => options.Count;

        //---Private Variables
        private readonly List<RebindCompositeOption> options = new();
        private int selectedIndex;

        public void Open(PauseOptionControlsTab tab, RebindPauseOptionButton option, int initialIndex = 0) {
            InputAction action = option.action;
            int compositeIndex = option.bindingIndex;

            TranslationManager tm = GlobalController.Instance.translationManager;
            string control = tm.GetTranslation($"ui.options.controls.{action.actionMap.name}.{action.name}");
            string device = tm.GetTranslation($"ui.options.controls.header.{action.bindings[compositeIndex + 1].groups}{(compositeIndex % 2 == 0 ? "primary" : "secondary")}");
            string headerText = tm.GetTranslationWithReplacements("ui.options.controls.rebind.header", "control", control, "device", device);
            header.text = headerText;

            while (action.bindings[++compositeIndex].isPartOfComposite) {
                InputBinding binding = action.bindings[compositeIndex];
                RebindCompositeOption newOption = Instantiate(optionTemplate);
                newOption.name = "CompositeButton (" + action.name + ", " + binding.name + ")";
                newOption.transform.SetParent(content, false);
                newOption.Instantiate(tab, option, action, compositeIndex);
                newOption.gameObject.SetActive(true);
                options.Add(newOption);
            }

            SetSelectedIndex(initialIndex, true);
            gameObject.SetActive(true);
        }

        public void Close(bool playSound = true) {
            gameObject.SetActive(false);

            if (playSound) {
                GlobalController.Instance.PlaySound(SoundEffect.UI_Back);
            }

            foreach (RebindCompositeOption option in options) {
                Destroy(option.gameObject);
            }

            options.Clear();
        }

        public void OnUpPress() {
            ChangeSelectedIndex(-1);
        }

        public void OnDownPress() {
            ChangeSelectedIndex(1);
        }

        public void OnSubmit() {
            if (selectedIndex >= MaxIndex) {
                Close();
                return;
            }

            options[selectedIndex].OnClick();
        }

        public void OnCancel() {
            if (selectedIndex >= MaxIndex) {
                Close();
                return;
            }

            SelectBackOption();
            GlobalController.Instance.PlaySound(SoundEffect.UI_Cursor);
        }

        public void SelectBackOption() {
            SetSelectedIndex(MaxIndex);
        }

        private void ChangeSelectedIndex(int amount) {
            SetSelectedIndex(Mathf.Clamp(selectedIndex + amount, 0, MaxIndex));
        }

        private void SetSelectedIndex(int index, bool forceSelect = false) {
            if (!forceSelect && selectedIndex == index) {
                return;
            }

            DeselectOption(selectedIndex);
            selectedIndex = index;
            SelectOption(selectedIndex);
        }

        private void SelectOption(int index) {
            if (index >= options.Count) {
                backText.text = "» " + GlobalController.Instance.translationManager.GetTranslation("ui.generic.back") + " «";
            } else {
                options[index].Selected();
            }
        }

        private void DeselectOption(int index) {
            if (index >= options.Count) {
                backText.text = GlobalController.Instance.translationManager.GetTranslation("ui.generic.back");
            } else {
                options[index].Deselected();
            }
        }
    }
}
