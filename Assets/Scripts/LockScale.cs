using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockScale : MonoBehaviour {
    void Update() {
        Vector3 parentScale = transform.parent.localScale;
        transform.localScale = new Vector3(1f/parentScale.x, 1f/parentScale.y, 1f/parentScale.z);
    }
}
