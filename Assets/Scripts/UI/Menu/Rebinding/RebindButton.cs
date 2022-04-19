using System.Collections;
using System.Collections.Generic;
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
        EndRebind(false);
    }

    public void StartRebind() {

        targetAction.actionMap.Disable();

        MainMenuManager.Instance.rebindPrompt.SetActive(true);
        MainMenuManager.Instance.rebindText.text = $"Rebinding {targetAction.name} {targetBinding.name} ({targetBinding.groups})\nPress any button or key.";

        rebinding = targetAction
            .PerformInteractiveRebinding()
            .WithControlsExcluding("Mouse")
            .WithControlsHavingToMatchPath($"<{targetBinding.groups}>")
            .WithTargetBinding(index)
            .WithTimeout(timeoutTime)
            .OnMatchWaitForAnother(0.1f)
            .OnApplyBinding((op,str) => ApplyBind(str))
            .OnCancel(op => CancelRebind())
            .OnComplete(op => EndRebind(true))
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

        targetBinding = targetAction.bindings[index];
        targetBinding.overridePath = path;
        targetAction.ApplyBindingOverride(index, targetBinding);
        targetBinding = targetAction.bindings[index];

        //Debug.Log("applied? " + targetBinding.overridePath);
    }

    public void EndRebind(bool cancel) {
        ourText.text = InputControlPath.ToHumanReadableString(
            targetBinding.effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
        targetAction.actionMap.Enable();
        if (cancel) {
            CancelRebind();
            MainMenuManager.Instance.rebindManager.SaveRebindings();
        }
    }

    void CancelRebind() {
        targetAction.actionMap.Enable();
        rebinding.Dispose();
        MainMenuManager.Instance.rebindPrompt.SetActive(false);
        StopCoroutine(countdown);
    }
}