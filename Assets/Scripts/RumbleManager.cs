using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class RumbleManager : MonoBehaviour {
    private Gamepad pad;
    private Coroutine currentlyRumbling;

    [SerializeField][Range(0,2)] private int strengthMultiplier = 1; 
    
    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Gamepad gamepad) pad = gamepad;
    }

    public void RumbleForSeconds(float bassStrength, float trebleStrength, float duration) {
        if (strengthMultiplier <= 0 || pad == null) return;
        if (currentlyRumbling != null) StopCoroutine(currentlyRumbling);
        currentlyRumbling = StartCoroutine(Rumble(bassStrength, trebleStrength, duration));
    }

    private IEnumerator Rumble(float lowFreq, float highFreq, float duration) {
        pad.SetMotorSpeeds(lowFreq * strengthMultiplier, highFreq * strengthMultiplier);
        yield return new WaitForSeconds(duration);
        pad.SetMotorSpeeds(0f, 0f);
    }
}
