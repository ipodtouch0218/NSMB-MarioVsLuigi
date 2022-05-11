using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialDuplicator : MonoBehaviour {
    [ExecuteInEditMode]
    void Awake() {
        Renderer renderer = GetComponent<Renderer>();
        renderer.material = new(renderer.material);
    }
}
 