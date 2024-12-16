using System.Collections.Generic;
using UnityEngine;

// TODO: potentially try to instead use the OnParticleSystemStopped callback?
// I don't know if children will call that though...
public class ParticleAutoDestroy : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private bool onlyDisable;

    //---Private Variables
    private readonly List<ParticleSystem> systems = new();

    public void OnEnable() {
        systems.AddRange(GetComponents<ParticleSystem>());
        systems.AddRange(GetComponentsInChildren<ParticleSystem>());
    }

    public void Update() {
        if (!systems.TrueForAll(ps => ps.isStopped)) {
            return;
        }

        if (onlyDisable) {
            gameObject.SetActive(false);
        } else {
            Destroy(gameObject);
        }
    }
}
