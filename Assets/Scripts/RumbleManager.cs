using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class RumbleManager : MonoBehaviour {

    //---Serialized Variables
    [SerializeField, Range(0,2)] private int strengthMultiplier = 1;

    //---Private Variables
    private Gamepad pad;
    private Coroutine currentlyRumbling;

    private void OnEnable() {
        InputSystem.onDeviceChange += OnDeviceChange;
        pad = Gamepad.current;
    }

    private void OnDisable() {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change) {
        if (device is Gamepad gamepad) {
            pad?.ResetHaptics();
            pad = gamepad;
        }
    }

    public void RumbleForSeconds(float bassStrength, float trebleStrength, float duration, RumbleSetting setting) {
        if (setting == RumbleSetting.None || setting > Settings.Instance.controlsRumble) return;
        if (strengthMultiplier <= 0 || pad == null) return;

        if (currentlyRumbling != null) StopCoroutine(currentlyRumbling);
        currentlyRumbling = StartCoroutine(Rumble(bassStrength, trebleStrength, duration));
    }

    private IEnumerator Rumble(float lowFreq, float highFreq, float duration) {
        pad.SetMotorSpeeds(lowFreq * strengthMultiplier, highFreq * strengthMultiplier);
        yield return new WaitForSeconds(duration);
        pad.ResetHaptics();
    }

    public enum RumbleSetting : int {
        None,
        Low,
        High
    }
}
