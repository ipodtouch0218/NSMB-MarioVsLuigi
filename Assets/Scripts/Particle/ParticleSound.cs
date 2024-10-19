using NSMB.Extensions;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ParticleSound : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private ParticleSystem[] systems;
    [SerializeField] private LoopingSoundPlayer sfx;

    public void OnValidate() {
        this.SetIfNull(ref sfx);

        if (systems == null) {
            List<ParticleSystem> particles = new();
            particles.AddRange(GetComponentsInChildren<ParticleSystem>());
            systems = particles.ToArray();
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

        if (systems.Any(ps => ps.isEmitting) && !sfx.IsPlaying) {
            sfx.Play();
        }
        if (systems.All(ps => !ps.isEmitting) && sfx.IsPlaying) {
            sfx.Stop();
        }
    }
}
