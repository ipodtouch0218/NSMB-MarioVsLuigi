using UnityEngine;

public class FreezeZ : MonoBehaviour {

    private float z;

    public void OnEnable() {
        z = transform.position.z;
    }

    public void LateUpdate() {
        Vector3 pos = transform.position;
        pos.z = z;
        transform.position = pos;
    }
}