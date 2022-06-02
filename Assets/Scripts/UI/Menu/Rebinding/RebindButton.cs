using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;
using TMPro;

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
            .OnCancel(op => CleanRebind())
            .OnComplete(op => OnRebindComplete(op))
            .Start();

        countdown = StartCoroutine(TimeoutCountdown());
    }

    IEnumerator TimeoutCountdown() {
        for (int i = timeoutTime; i > 0; i--) {
            MainMenuManager.Instance.rebindCountdown.text = i.ToString();
            yield return new WaitForSecondsRealtime(1);
        }
    }

    void ApplyBind(string path) {
        Debug.Log("apply " + path);

        targetBinding = targetAction.bindings[index];
        targetBinding.overridePath = path;
        targetAction.ApplyBindingOverride(index, targetBinding);
        targetBinding = targetAction.bindings[index];
    }

    public void OnRebindComplete(RebindingOperation operation) {
        targetBinding = targetAction.bindings[index];
        SetText();
        CleanRebind();
    }

    void CleanRebind() {
        targetAction.actionMap.Enable();
        rebinding.Dispose();
        MainMenuManager.Instance.rebindPrompt.SetActive(false);
        StopCoroutine(countdown);
    }

    public void SetText() {
        ourText.text = InputControlPath.ToHumanReadableString(
            targetBinding.effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
    }

    InputControl GetBestCandidate(RebindingOperation operation) {
        int index = 0;
        for (int i = 1; i < operation.candidates.Count; i++) {
            if (operation.scores[i] > operation.scores[index])
                index = i;
        }
        return operation.candidates[index];
    }
}