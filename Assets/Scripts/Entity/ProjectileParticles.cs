using Quantum;
using System;
using System.Linq;
using UnityEngine;

public class ProjectileParticles : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private ParticlePair[] particles;
    
    public void Start() {
        QuantumEvent.Subscribe<EventProjectileDestroyed>(this, OnProjectileDestroyed, NetworkHandler.FilterOutReplayFastForward);
    }

    private void OnProjectileDestroyed(EventProjectileDestroyed e) {
        ParticlePair pp = particles.FirstOrDefault(p => p.particle == e.Particle);
        if (pp.prefab) {
            Instantiate(pp.prefab, e.Position.ToUnityVector3() + pp.offset, Quaternion.identity);
        }
    }

    [Serializable]
    public struct ParticlePair {
        public ParticleEffect particle;
        public GameObject prefab;
        public Vector3 offset;
    }
}
