using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockScale : MonoBehaviour {
    void Update() {
        Vector3 parentScale = transform.parent.localScale;
        if (parentScale.z == 0)
            parentScale.z = 1;
        transform.localScale = new Vector3(1f/parentScale.x, 1f/parentScale.y, 1f/parentScale.z);
    }
}
