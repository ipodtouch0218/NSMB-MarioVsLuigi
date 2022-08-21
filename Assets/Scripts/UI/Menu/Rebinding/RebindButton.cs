using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

using static UnityEngine.InputSystem.InputActionRebindingExtensions;

public class RebindButton : MonoBehaviour {

    public int timeoutTime = 6;
    public InputAction targetAction;
    public InputBinding targetBinding;
    public int index;
    private TMP_Text ourText;
    private RebindingOperation rebinding;
    private Coroutine countdown;

    public void Start() {
        ourText = GetComponentInChildren<TMP_Text>();
        SetText();
        targetAction.actionMap.Enable();
    }

    public void StartRebind() {

        targetAction.actionMap.Disable();

        MainMenuManager.Instance.rebindPrompt.SetActive(true);
        MainMenuManager.Instance.rebindText.text = $"Rebinding {targetAction.name} {targetBinding.name} ({targetBinding.groups})\nPress any button or key.";

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

        countdown = StartCoroutine(TimeoutCountdown());
    }

    private IEnumerator TimeoutCountdown() {
        for (int i = timeoutTime; i > 0; i--) {
            MainMenuManager.Instance.rebindCountdown.text = i.ToString();
            yield return new WaitForSecondsRealtime(1);
        }
    }

    private void OnRebindComplete(RebindingOperation operation) {
        SetText();
        CleanRebind(operation);
        RebindManager.Instance.SaveRebindings();
    }

    private void CleanRebind(RebindingOperation operation) {
        targetAction.actionMap.Enable();
        rebinding.Dispose();
        MainMenuManager.Instance.rebindPrompt.SetActive(false);
        StopCoroutine(countdown);
    }

    public void SetText() {
        targetBinding = targetAction.bindings[index];
        ourText.text = InputControlPath.ToHumanReadableString(
            targetBinding.effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
    }
}