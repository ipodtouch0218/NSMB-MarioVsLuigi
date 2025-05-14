using Quantum;
using System;
using UnityEngine;

public class MiscParticles : QuantumSceneViewComponent {

    //---Serialized Variables
    [SerializeField] private ParticlePair[] particles;
    
    public void Start() {
        QuantumEvent.Subscribe<EventProjectileDestroyed>(this, OnProjectileDestroyed, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventCollectableDespawned>(this, OnCollectableDespawned, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventEnemyKicked>(this, OnEnemyKicked, NetworkHandler.FilterOutReplayFastForward);
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

    private unsafe void OnEnemyKicked(EventEnemyKicked e) {
        QuantumEntityView view = Updater.GetView(e.Entity);
        if (view) {
            Instantiate(
                Enums.PrefabParticle.Enemy_HardKick.GetGameObject(),
                view.transform.position + (Vector3.back * 5) + (Vector3.up * 0.1f),
                Quaternion.identity);
        }
    }

    [Serializable]
    public class ParticlePair {
        public ParticleEffect particle;
        public GameObject prefab;
        public Vector3 offset;
    }
}
