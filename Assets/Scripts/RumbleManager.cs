using System.Collections;
using System.Collections.Generic;
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
        if (device is Gamepad) pad = (Gamepad)device;
    }

    public void RumbleForSeconds(float lowStrength, float highStrength, float duration) {
        if (strengthMultiplier <= 0) return;
        if (currentlyRumbling != null) StopCoroutine(currentlyRumbling);
        currentlyRumbling = StartCoroutine(Rumble(lowStrength, highStrength, duration));
    }

    private IEnumerator Rumble(float lowStrength, float highStrength, float duration) {
        pad.SetMotorSpeeds(lowStrength * strengthMultiplier, highStrength * strengthMultiplier);
        yield return new WaitForSeconds(duration);
        pad.SetMotorSpeeds(0f, 0f);
    }
}
