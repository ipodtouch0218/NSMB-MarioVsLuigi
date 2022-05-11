using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleAutoDestroy : MonoBehaviour {
    private readonly List<ParticleSystem> systems = new();
    public bool onlyDisable = false;
    void OnEnable() {
        systems.AddRange(GetComponents<ParticleSystem>());
        systems.AddRange(GetComponentsInChildren<ParticleSystem>());
    }

    void Update() {
        if (systems.TrueForAll(SystemStopped)) {
            if (onlyDisable) {
                gameObject.SetActive(false);
            } else {
                Destroy(gameObject);
            }
        }
    }

    private static bool SystemStopped(ParticleSystem ps) {
        return ps.isStopped;
    }
}
