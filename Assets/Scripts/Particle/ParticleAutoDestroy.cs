using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleAutoDestroy : MonoBehaviour {
    List<ParticleSystem> systems = new List<ParticleSystem>();
    void Start() {
        systems.AddRange(GetComponents<ParticleSystem>());
        systems.AddRange(GetComponentsInChildren<ParticleSystem>());
    }

    void Update() {
        if (systems.TrueForAll(SystemStopped)) {
            Destroy(gameObject);
        }
    }

    private static bool SystemStopped(ParticleSystem ps) {
        return ps.isStopped;
    }
}
