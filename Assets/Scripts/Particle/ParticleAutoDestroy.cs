using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleAutoDestroy : MonoBehaviour {
    void Start() {
        float duration = 0;
        foreach (ParticleSystem systems in GetComponents<ParticleSystem>()) {
            duration = Mathf.Max(duration, systems.main.duration);
        }
        foreach (ParticleSystem systems in GetComponentsInChildren<ParticleSystem>()) {
            duration = Mathf.Max(duration, systems.main.duration);
        }
        Destroy(gameObject, duration);
    }
}
