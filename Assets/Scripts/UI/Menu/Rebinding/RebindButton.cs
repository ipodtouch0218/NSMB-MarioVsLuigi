using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;

using NSMB.Extensions;


namespace NSMB.Rebinding {
    public class RebindButton : MonoBehaviour {

        //---Static Variables
        private static readonly WaitForSeconds WaitOneSecond = new(1);

        //---Public Variables
        public RebindManager manager;
        public InputAction targetAction;
        public InputBinding targetBinding;
        public int index;

        //---Serialized Variables
        [SerializeField] private int timeoutTime = 6;

        //---Private Variables
        private RebindingOperation rebinding;
        private TMP_Text ourText;
        private Coroutine countdownCoroutine;

        public void Start() {
            ourText = GetComponentInChildren<TMP_Text>();
            UpdateText();
            targetAction.actionMap.Enable();
        }

        public void StartRebind() {

            if (MainMenuManager.Instance)
                MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Cursor);

            if (manager.IsUnbinding) {
                targetAction.ApplyBindingOverride(index, path: "");
                UpdateText();
                //manager.ToggleUnbinding();
                return;
            }

            targetAction.actionMap.Disable();

            manager.rebindPrompt.SetActive(true);
            manager.rebindPromptText.text = $"Rebinding {targetAction.name} {targetBinding.name} ({targetBinding.groups})\nPress any button or key.";

            rebinding = targetAction
                .PerformInteractiveRebinding()
                .WithControlsExcluding("Mouse")
                .WithControlsHavingToMatchPath($"<{targetBinding.groups}>")
                // .WithCancelingThrough()
                .WithAction(targetAction)
                .WithTargetBinding(index)
                .WithTimeout(timeoutTime)
                .OnMatchWaitForAnother(0.2f)
                // .OnApplyBinding((op,str) => ApplyBind(str))
                .OnCancel(CleanRebind)
                .OnComplete(OnRebindComplete)
                .Start();

            countdownCoroutine = StartCoroutine(TimeoutCountdown());
        }

        private IEnumerator TimeoutCountdown() {
            for (int i = timeoutTime; i > 0; i--) {
                manager.rebindCountdownText.text = i.ToString();
                yield return WaitOneSecond;
            }
        }

        private void OnRebindComplete(RebindingOperation operation) {
            UpdateText();
            CleanRebind(operation);
            manager.SaveRebindings();

            if (MainMenuManager.Instance)
                MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Cursor);
        }

        private void CleanRebind(RebindingOperation operation) {
            targetAction.actionMap.Enable();
            rebinding.Dispose();
            manager.rebindPrompt.SetActive(false);

            if (countdownCoroutine != null) {
                StopCoroutine(countdownCoroutine);
                countdownCoroutine = null;
            }
        }

        public void UpdateText() {
            targetBinding = targetAction.bindings[index];
            ourText.text = InputControlPath.ToHumanReadableString(
                targetBinding.effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
        }
    }
}
