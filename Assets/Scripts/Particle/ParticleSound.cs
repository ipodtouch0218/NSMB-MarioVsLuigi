using NSMB.Extensions;
using NSMB.Sound;
using Photon.Deterministic;
using Quantum;
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

    public void Start() {
        OnValidate();
    }

    public void Update() {
        if (systems == null || !sfx) {
            enabled = false;
            return;
        }

        sfx.Source.enabled = Time.deltaTime != 0 && !NetworkHandler.IsReplayFastForwarding;

        if (sfx.Source.enabled) {
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
}
