using System.Collections.Generic;
using UnityEngine;

public class ParticleAutoDestroy : MonoBehaviour {

    [SerializeField] private bool onlyDisable = false;

    private readonly List<ParticleSystem> systems = new();

    public void OnEnable() {
        systems.AddRange(GetComponents<ParticleSystem>());
        systems.AddRange(GetComponentsInChildren<ParticleSystem>());
    }

    public void Update() {
        if (systems.TrueForAll(ps => ps.isStopped)) {
            if (onlyDisable) {
                gameObject.SetActive(false);
            } else {
                Destroy(gameObject);
            }
        }
    }
}
