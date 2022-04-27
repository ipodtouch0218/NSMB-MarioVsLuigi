using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputSystem : MonoBehaviour {

    public static Controls controls;
    
    public void Awake() {
        controls = new();
        controls.Enable();
        if (GlobalController.Instance.controlsJson != null)
            controls.LoadBindingOverridesFromJson(GlobalController.Instance.controlsJson);
    }
}