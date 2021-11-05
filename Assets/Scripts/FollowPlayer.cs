using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowPlayer : MonoBehaviour {
    public GameObject target;
    public Vector3 offset = Vector3.zero;
    void LateUpdate() {
        if (target == null) {
            target = GameManager.Instance.localPlayer;
            return;
        }
        transform.position = target.transform.position + offset;
    }
}
