using NSMB.Translation;
using NSMB.UI.Prompts;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;


namespace NSMB.UI.Pause.Options {

    public class PauseOptionControlsTab : PauseOptionTab {

        //---Static Variables
        private static readonly WaitForSecondsRealtime WaitForOneSecond = new(1f);

        //---Serialized Variables
        [SerializeField] private PauseOptionMenuManager manager;
        [SerializeField] private Transform scrollPaneContent;

        [Header("Templates")]
        [SerializeField] private NonselectableOption spacerTemplate;
        [SerializeField] private RebindPauseOption controlTemplate;
        [SerializeField] private NonselectableOption headerTemplate;

        [Header("Rebind Prompt")]
        [SerializeField] private GameObject rebindPrompt;
        [SerializeField] private TMP_Text rebindPromptText;
        [SerializeField] private TMP_Text rebindPromptCountdownText;

        [Header("Composite Prompt")]
        [SerializeField] public CompositeRebindPrompt rebindCompositePrompt;
        [SerializeField] private Transform rebindCompositeContent;
        [SerializeField] private TMP_Text rebindCompositeText;
        [SerializeField] private RebindCompositeOption rebindCompositeTemplate;

        [Header("Settings")]
        [SerializeField] private int timeoutTime = 6;

        //---Private Variables
        private RebindPauseOptionButton currentRebindingButton;
        private RebindingOperation currentRebinding;
        private int currentRebindingIndex;
        private Coroutine countdown;
        private bool initialized;

        //---Properties
        public InputActionAsset Controls => ControlSystem.controls.asset;
        private bool IsCompositeRebind => rebindCompositePrompt.gameObject.activeSelf;

        public override void Selected() {
            if (!initialized) {
                // Create options from controls
                CreateActions();
                initialized = true;
            }

            base.Selected();
        }

        public override void Deselected() {
            if (!initialized) {
                // Create options from controls
                CreateActions();
                initialized = true;
            }

            base.Deselected();
        }

        private void CreateActions() {

            List<PauseOption> newOptions = new();

            for (int i = 0; i < Controls.actionMaps.Count; i++) {
                InputActionMap map = Controls.actionMaps[i];

                // Controls starting with '!' are not rebindable
                if (map.name.StartsWith("!")) {
                    continue;
                }

                NonselectableOption newHeader = Instantiate(headerTemplate);
                newHeader.name = "ControlsHeader (" + map.name + ")";
                newHeader.gameObject.SetActive(true);
                newHeader.transform.SetParent(scrollPaneContent, false);
                newHeader.translationKey = "ui.options.controls." + map.name.ToLower() + ".header";

                newOptions.Add(newHeader);

                foreach (InputAction action in map.actions) {

                    // Controls starting with '!' are not rebindable
                    if (action.name.StartsWith("!")) {
                        continue;
                    }

                    RebindPauseOption newOption = Instantiate(controlTemplate);
                    newOption.name = "ControlsButton (" + map.name + ", " + action.name + ")";
                    newOption.transform.SetParent(scrollPaneContent, false);
                    newOption.action = action;
                    newOption.gameObject.SetActive(true);

                    newOptions.Add(newOption);
                }

                if (i == 0) {
                    // Move the default existing settings to be below me.
                    foreach (PauseOption option in options) {
                        option.transform.SetAsLastSibling();
                        newOptions.Add(option);
                    }
                }

                if (i < Controls.actionMaps.Count - 1) {
                    NonselectableOption newSpacer = Instantiate(spacerTemplate);
                    newSpacer.name = "ControlsSpacer";
                    newSpacer.transform.SetParent(scrollPaneContent, false);
                    newSpacer.gameObject.SetActive(true);

                    newOptions.Add(newSpacer);
                }
            }

            options.Clear();
            options.AddRange(newOptions);
        }

        public void SaveRebindings() {
            Settings.Instance.SaveSettings();
        }

        public void StartRebind(RebindPauseOptionButton option, int index = -1) {

            if (currentRebinding != null) {
                DisposeRebind(currentRebinding);
            }

            manager.SetCurrentOption(option.parent);
            GlobalController.Instance.PlaySound(SoundEffect.UI_Cursor);

            //if (manager.IsUnbinding) {
            //    targetAction.ApplyBindingOverride(index, path: "");
            //    UpdateText();
            //    //manager.ToggleUnbinding();
            //    return;
            //}

            InputAction targetAction = option.action;
            InputBinding targetBinding = option.Binding;

            if (targetBinding.isComposite) {
                if (index == -1) {
                    // Open composite prompt.
                    rebindCompositePrompt.Open(this, option);
                    currentRebindingButton = option;
                    return;
                } else {
                    targetBinding = targetAction.bindings[index];
                    rebindCompositePrompt.gameObject.SetActive(false);
                }
            } else if (index == -1) {
                index = option.bindingIndex;
            }

            currentRebindingButton = option;
            currentRebindingIndex = index;
            targetAction.actionMap.Disable();
            manager.EnableInput = false;

            rebindPrompt.SetActive(true);
            TranslationManager tm = GlobalController.Instance.translationManager;

            string control = tm.GetTranslation($"ui.options.controls.{targetAction.actionMap.name}.{targetAction.name.Replace(" ", "")}");
            tm.TryGetTranslation($"ui.generic.{targetAction.bindings[index].name}", out string binding);
            binding ??= "";
            string device = tm.GetTranslation($"ui.options.controls.rebind.device.{targetBinding.groups}");
            string deviceSlot = tm.GetTranslation($"ui.options.controls.header.{targetBinding.groups}{((option.bindingIndex % 2 == 0) ? "primary" : "secondary")}");

            string header = tm.GetTranslationWithReplacements("ui.options.controls.rebind.header", "control", control + (string.IsNullOrWhiteSpace(binding) ? "" : " " + binding), "device", deviceSlot);
            string body = tm.GetTranslationWithReplacements("ui.options.controls.rebind.body", "device", device);
            rebindPromptText.text = $"{header}\n\n{body}";

            currentRebinding = targetAction
                .PerformInteractiveRebinding()
                .WithControlsExcluding("Mouse")
                .WithControlsHavingToMatchPath($"<{targetBinding.groups}>")
                // .WithCancelingThrough()
                .WithAction(targetAction)
                .WithTargetBinding(index)
                .WithTimeout(timeoutTime)
                .OnMatchWaitForAnother(0.2f)
                // .OnApplyBinding(ApplyRebind)
                .OnCancel(DisposeRebind)
                .OnComplete(OnRebindComplete)
                .Start();

            countdown = StartCoroutine(TimeoutCountdown());
        }

        private IEnumerator TimeoutCountdown() {
            for (int i = timeoutTime; i > 0; i--) {
                rebindPromptCountdownText.text = i.ToString();
                yield return WaitForOneSecond;
            }
        }

        private void OnRebindComplete(RebindingOperation operation) {
            currentRebindingButton.UpdateLabel();
            DisposeRebind(operation);
            SaveRebindings();

            GlobalController.Instance.PlaySound(SoundEffect.UI_Decide);
        }

        private void DisposeRebind(RebindingOperation operation) {

            currentRebindingButton.action.actionMap.Enable();
            operation.Dispose();
            currentRebinding = null;
            rebindPrompt.SetActive(false);

            if (countdown != null) {
                StopCoroutine(countdown);
                countdown = null;
            }

            manager.EnableInput = true;

            if (currentRebindingButton.Binding.isComposite) {
                // Re-open the composite screen
                rebindCompositePrompt.Open(this, currentRebindingButton, currentRebindingIndex - currentRebindingButton.bindingIndex - 1);
            } else {
                currentRebindingButton = null;
                currentRebindingIndex = -1;
            }
        }

        public override bool OnLeftPress(bool held) {
            if (!IsCompositeRebind) {
                return false;
            }

            return true;
        }

        public override bool OnRightPress(bool held) {
            if (!IsCompositeRebind) {
                return false;
            }

            return true;
        }

        public override bool OnUpPress(bool held) {
            if (!IsCompositeRebind) {
                return false;
            }

            if (!held) {
                rebindCompositePrompt.OnUpPress();
            }

            return true;
        }

        public override bool OnDownPress(bool held) {
            if (!IsCompositeRebind) {
                return false;
            }

            if (!held) {
                rebindCompositePrompt.OnDownPress();
            }

            return true;
        }

        public override bool OnSubmit() {
            if (!IsCompositeRebind) {
                return false;
            }

            rebindCompositePrompt.OnSubmit();
            return true;
        }

        public override bool OnCancel() {
            if (!IsCompositeRebind) {
                return false;
            }

            rebindCompositePrompt.OnCancel();
            return true;
        }
    }
}

