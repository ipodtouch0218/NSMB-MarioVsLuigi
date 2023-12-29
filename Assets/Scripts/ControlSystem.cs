using System.IO;
using UnityEngine;

public class ControlSystem : MonoBehaviour {

    public static Controls controls;

    public void Awake() {
        if (controls != null)
            return;

        controls = new();
        controls.Enable();
    }
}
