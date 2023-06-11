using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

using NSMB.Translation;
using NSMB.UI.Pause.Options;

namespace NSMB.UI.Prompts {

    public class CompositeRebindPrompt : UIPrompt {

        //---Serialized Variables
        [SerializeField] private RebindCompositeOption optionTemplate;
        [SerializeField] private TMP_Text header;
        [SerializeField] private Transform content;

        //---Properties
        private int MaxIndex => options.Count + 1;

        //---Private Variables
        private readonly List<RebindCompositeOption> options = new();
        private int selectedIndex;

        public void Open(PauseOptionControlsTab tab, RebindPauseOptionButton option) {
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

            SetSelectedIndex(0, false);
            gameObject.SetActive(true);
        }

        public void Close() {
            gameObject.SetActive(false);
            GlobalController.Instance.PlaySound(Enums.Sounds.UI_Back);

            foreach (RebindCompositeOption option in options)
                Destroy(option.gameObject);
            options.Clear();
        }

        public void OnUpPress() {
            ChangeSelectedIndex(-1);
        }

        public void OnDownPress() {
            ChangeSelectedIndex(1);
        }

        public void OnSubmit() {

        }

        public void OnCancel() {
            if (selectedIndex >= MaxIndex) {
                Close();
                return;
            }

            SetSelectedIndex(MaxIndex);
        }

        private void ChangeSelectedIndex(int amount, bool playSound = true) {
            int oldIndex = selectedIndex;
            selectedIndex = Mathf.Clamp(selectedIndex + amount, 0, MaxIndex);

            if (playSound && selectedIndex != oldIndex)
                GlobalController.Instance.PlaySound(Enums.Sounds.UI_Back);
        }

        private void SetSelectedIndex(int index, bool playSound = true) {
            if (selectedIndex == index)
                return;

            selectedIndex = index;

            if (playSound)
                GlobalController.Instance.PlaySound(Enums.Sounds.UI_Back);
        }
    }
}
