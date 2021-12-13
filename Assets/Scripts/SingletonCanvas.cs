using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SingletonCanvas : Singleton<SingletonCanvas> {
    private Camera uiCamera;
    void Awake() {
        if (!base.InstanceCheck()) return;
        instance = this;
    }
    void LateUpdate() {
        if (!uiCamera) {
            foreach (Camera camera in FindObjectsOfType<Camera>()) {
                if (camera.tag == "uicamera") {
                    uiCamera = camera;
                    break;
                }
            }
        }
        if (!uiCamera)
            return;
        transform.position = uiCamera.transform.position - uiCamera.transform.eulerAngles;
    }
}
