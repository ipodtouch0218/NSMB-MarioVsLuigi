using System.IO;
using UnityEngine;

public class ControlSystem : MonoBehaviour {

    public static Controls controls;
    public static FileInfo file;

    public void Awake() {
        if (controls != null)
            return;

        controls = new();
        controls.Enable();

        file = new(Application.persistentDataPath + "/controls.json");
    }
}
