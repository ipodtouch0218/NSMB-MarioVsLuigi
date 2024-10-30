using NSMB.Extensions;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSound : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private List<ParticleSystem> systems;
    [SerializeField] private LoopingSoundPlayer sfx;

    public void OnValidate() {
        this.SetIfNull(ref sfx);
        if (systems == null || systems.Count == 0) {
            GetComponentsInChildren(systems);
        }
    }

    public void Awake() {
        OnValidate();
    }

    public void Update() {
        if (systems == null || !sfx) {
            enabled = false;
            return;
        }

        foreach (ParticleSystem system in systems) {
            if (system.isEmitting) {
                if (!sfx.IsPlaying) {
                    sfx.Play();
                }
                return;
            }
        }
        if (sfx.IsPlaying) {
            sfx.Stop();
        }
    }
}
