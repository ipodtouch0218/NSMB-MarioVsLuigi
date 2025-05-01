using Quantum;
using System;
using UnityEngine;

public class MiscParticles : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private ParticlePair[] particles;
    
    public void Start() {
        QuantumEvent.Subscribe<EventProjectileDestroyed>(this, OnProjectileDestroyed, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventCollectableDespawned>(this, OnCollectableDespawned, NetworkHandler.FilterOutReplayFastForward);
    }

    private bool TryGetParticlePair(ParticleEffect particleEffect, out ParticlePair particlePair) {
        foreach (var pair in particles) {
            if (particleEffect == pair.particle) {
                particlePair = pair;
                return true;
            }
        }
        particlePair = null;
        return false;
    }

    private void OnProjectileDestroyed(EventProjectileDestroyed e) {
        if (TryGetParticlePair(e.Particle, out ParticlePair pp)) {
            Instantiate(pp.prefab, e.Position.ToUnityVector3() + pp.offset, Quaternion.identity);
        }
    }

    private void OnCollectableDespawned(EventCollectableDespawned e) {
        if (!e.Collected && TryGetParticlePair(ParticleEffect.Puff, out ParticlePair pp)) {
            Instantiate(pp.prefab, e.Position.ToUnityVector3() + pp.offset, Quaternion.identity);
        }
    }

    [Serializable]
    public class ParticlePair {
        public ParticleEffect particle;
        public GameObject prefab;
        public Vector3 offset;
    }
}
