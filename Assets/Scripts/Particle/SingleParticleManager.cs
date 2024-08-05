using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SingleParticleManager : Singleton<SingleParticleManager> {

    [SerializeField] private ParticlePair[] serializedSystems;
    private Dictionary<ParticleEffect, ParticleSystem> systems;
    private Dictionary<ParticleEffect, ParticlePair> pairs;

    public void Start() {
        Set(this, false);
        systems = serializedSystems.ToDictionary(pp => pp.particle, pp => pp.system);
        pairs = serializedSystems.ToDictionary(pp => pp.particle, pp => pp);
    }

    public void Play(ParticleEffect particle, Vector3 position, Color? color = null, float rot = 0) {
        if (!systems.ContainsKey(particle)) {
            return;
        }

        ParticleSystem system = systems[particle];
        ParticlePair pair = pairs[particle];

        ParticleSystem.EmitParams emitParams = new() {
            position = position,
            rotation3D = new(0, 0, rot),
            applyShapeToPosition = true,
        };

        if (color.HasValue) {
            emitParams.startColor = color.Value;
        }

        system.Emit(emitParams, UnityEngine.Random.Range(pair.particleMin, pair.particleMax + 1));
    }

    [Serializable]
    public struct ParticlePair {
        public ParticleEffect particle;
        public ParticleSystem system;
        public int particleMin, particleMax;
    }
}
