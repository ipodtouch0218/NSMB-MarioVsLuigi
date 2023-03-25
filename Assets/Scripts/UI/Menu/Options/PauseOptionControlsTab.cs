using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;
using TMPro;

namespace NSMB.UI.Pause.Options {
    public class PauseOptionControlsTab : PauseOptionTab {

        //---Static Variables
        private static readonly WaitForSeconds WaitForOneSecond = new(1f);

        //---Serialized Variables
        [SerializeField] private Transform scrollPaneContent;
        [SerializeField] private InputActionAsset controls;

        [Header("Templates")]
        [SerializeField] private NonselectableOption spacerTemplate;
        [SerializeField] private RebindPauseOption controlTemplate;
        [SerializeField] private NonselectableOption headerTemplate;

        [Header("Rebind Prompt")]
        [SerializeField] private GameObject rebindPrompt;
        [SerializeField] private TMP_Text rebindPromptText;
        [SerializeField] private TMP_Text rebindPromptCountdownText;

        [Header("Composite Prompt")]
        [SerializeField] private GameObject rebindCompositePrompt;
        [SerializeField] private Transform rebindCompositeContent;
        [SerializeField] private TMP_Text rebindCompositeText;
        [SerializeField] private RebindCompositeOption rebindCompositeTemplate;

        [Header("Settings")]
        [SerializeField] private int timeoutTime = 6;

        //---Private Variables
        private RebindPauseOptionButton currentRebindingButton;
        private RebindingOperation currentRebinding;
        private Coroutine countdown;

        private bool initialized;

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

            for (int i = 0; i < controls.actionMaps.Count; i++) {
                InputActionMap map = controls.actionMaps[i];

                // Controls starting with '!' are not rebindable
                if (map.name.StartsWith("!"))
                    continue;

                NonselectableOption newHeader = Instantiate(headerTemplate);
                newHeader.name = "ControlsHeader (" + map.name + ")";
                newHeader.gameObject.SetActive(true);
                newHeader.transform.SetParent(scrollPaneContent, false);
                newHeader.label.text = map.name;

                options.Add(newHeader);

                foreach (InputAction action in map.actions) {

                    // Controls starting with '!' are not rebindable
                    if (action.name.StartsWith("!"))
                        continue;

                    RebindPauseOption newOption = Instantiate(controlTemplate);
                    newOption.name = "ControlsButton (" + map.name + ", " + action.name + ")";
                    newOption.transform.SetParent(scrollPaneContent, false);
                    newOption.action = action;
                    newOption.gameObject.SetActive(true);

                    options.Add(newOption);
                }


                if (i < controls.actionMaps.Count - 1) {
                    NonselectableOption newSpacer = Instantiate(spacerTemplate);
                    newSpacer.name = "ControlsSpacer";
                    newSpacer.transform.SetParent(scrollPaneContent, false);
                    newSpacer.gameObject.SetActive(true);

                    options.Add(newSpacer);
                }

                //if (map.name == "Player")
                //    playerSettings.transform.SetAsLastSibling();
            }
        }

        public void SaveRebindings() {
            Settings.Instance.SaveSettings();
        }

        public void StartRebind(RebindPauseOptionButton option, int index = -1) {

            if (currentRebinding != null)
                DisposeRebind(currentRebinding);

            GlobalController.Instance.PlaySound(Enums.Sounds.UI_Cursor);

            //if (manager.IsUnbinding) {
            //    targetAction.ApplyBindingOverride(index, path: "");
            //    UpdateText();
            //    //manager.ToggleUnbinding();
            //    return;
            //}


            InputAction targetAction = option.action;
            InputBinding targetBinding = option.Binding;

            if (index == -1) {
                if (targetBinding.isComposite) {
                    // Open composite prompt.
                    OpenCompositePrompt(option);
                    return;
                } else {
                    index = option.bindingIndex;
                }
            }

            currentRebindingButton = option;
            targetAction.actionMap.Disable();

            rebindPrompt.SetActive(true);
            rebindPromptText.text = $"Rebinding {targetAction.name} {targetBinding.name} ({targetBinding.groups})\n\nPress any {targetBinding.groups} button / key\n\nTo cancel, wait for the coutdown to expire:";

            currentRebinding = targetAction
                .PerformInteractiveRebinding()
                .WithControlsExcluding("Mouse")
                .WithControlsHavingToMatchPath($"<{targetBinding.groups}>")
                // .WithCancelingThrough()
                .WithAction(targetAction)
                .WithTargetBinding(index)
                .WithTimeout(timeoutTime)
                .OnMatchWaitForAnother(0.2f)
                // .OnApplyBinding((op,str) => ApplyBind(str))
                .OnCancel(DisposeRebind)
                .OnComplete(OnRebindComplete)
                .Start();

            countdown = StartCoroutine(TimeoutCountdown());
        }

        public void OpenCompositePrompt(RebindPauseOptionButton option) {
            currentRebindingButton = option;
            InputAction action = option.action;
            int compositeIndex = option.bindingIndex;

            rebindCompositePrompt.SetActive(true);
            rebindCompositeText.text = $"Rebinding {action.name} ({action.bindings[compositeIndex + 1].groups})";

            // Destroy existing children except for the template
            for (int i = rebindCompositeContent.childCount - 1; i >= 1; i--) {
                Destroy(rebindCompositeContent.GetChild(i).gameObject);
            }

            while (action.bindings[++compositeIndex].isPartOfComposite) {
                InputBinding binding = action.bindings[compositeIndex];
                RebindCompositeOption newOption = Instantiate(rebindCompositeTemplate);
                newOption.name = "CompositeButton (" + action.name + ", " + binding.name + ")";
                newOption.transform.SetParent(rebindCompositeContent, false);
                newOption.Instantiate(action, compositeIndex);
                newOption.gameObject.SetActive(true);
            }
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

            GlobalController.Instance.PlaySound(Enums.Sounds.UI_Cursor);
        }

        private void DisposeRebind(RebindingOperation operation) {
            currentRebindingButton.action.actionMap.Enable();
            operation.Dispose();
            rebindPrompt.SetActive(false);

            if (countdown != null) {
                StopCoroutine(countdown);
                countdown = null;
            }
        }
    }
}

