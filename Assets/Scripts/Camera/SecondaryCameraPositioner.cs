using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SecondaryCameraPositioner : MonoBehaviour {
    [SerializeField] float multiplier = 1;
    void Update() {
        if (GameManager.Instance) {
            if (!GameManager.Instance.loopingLevel) {
                Destroy(gameObject);
                return;
            }
            transform.localPosition = new Vector3(GameManager.Instance.levelWidthTile * multiplier, 0, 0);
            Destroy(this);
        }
    }
}