using Quantum;
using System;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

public class MiscParticles : QuantumSceneViewComponent {

    //---Static
    public static MiscParticles Instance { get; private set; }

    //---Serialized Variables
    [SerializeField] private ParticlePair[] particles;
    
    public void Start() {
        QuantumEvent.Subscribe<EventProjectileDestroyed>(this, OnProjectileDestroyed, FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventCollectableDespawned>(this, OnCollectableDespawned, FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventEnemyKicked>(this, OnEnemyKicked, FilterOutReplayFastForward);
        Instance = this;
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

    public void Play(ParticleEffect particle, Vector3 position) {
        if (TryGetParticlePair(particle, out ParticlePair pp)) {
            Instantiate(pp.prefab, position + pp.offset, Quaternion.identity);
        }
    }

    private void OnProjectileDestroyed(EventProjectileDestroyed e) {
        Play(e.Particle, e.Position.ToUnityVector3());
    }

    private void OnCollectableDespawned(EventCollectableDespawned e) {
        if (!e.Collected) {
            Play(ParticleEffect.Puff, e.Position.ToUnityVector3());
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
