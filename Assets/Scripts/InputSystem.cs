using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputSystem : MonoBehaviour {

    public static Controls controls;
    public static FileInfo file;

    public void Awake() {
        if (controls != null)
            return;

        controls = new();
        controls.Enable();

        file = new(Application.persistentDataPath + "/controls.json");

        if (GlobalController.Instance.controlsJson != null) {
            // we have old bindings...
            controls.LoadBindingOverridesFromJson(GlobalController.Instance.controlsJson);

        } else if (file.Exists) {
            //load bindings...
            try {
                controls.LoadBindingOverridesFromJson(File.ReadAllText(file.FullName));
                GlobalController.Instance.controlsJson = controls.SaveBindingOverridesAsJson();
            } catch (System.Exception e) {
                Debug.LogError(e.Message);
            }
        }
    }
}