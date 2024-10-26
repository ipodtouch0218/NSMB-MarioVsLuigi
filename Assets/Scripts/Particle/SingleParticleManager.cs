using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SingleParticleManager : Singleton<SingleParticleManager> {

    //---Serialized Variables
    [SerializeField] private ParticlePair[] serializedSystems;

    //---Private Variables
    private Dictionary<ParticleEffect, ParticlePair> pairs;

    public void Start() {
        Set(this, false);
        pairs = serializedSystems.ToDictionary(pp => pp.particle, pp => pp);
    }

    public void Play(ParticleEffect particle, Vector3 position, Color? color = null, float rot = 0) {
        if (particle == ParticleEffect.None 
            || !pairs.TryGetValue(particle, out ParticlePair pair)) {
            return;
        }

        ParticleSystem.EmitParams emitParams = new() {
            position = position,
            rotation3D = new(0, 0, rot),
            applyShapeToPosition = true,
        };

        if (color.HasValue) {
            emitParams.startColor = color.Value;
        }

        pair.system.Emit(emitParams, UnityEngine.Random.Range(pair.particleMin, pair.particleMax + 1));
    }

    [Serializable]
    public struct ParticlePair {
        public ParticleEffect particle;
        public ParticleSystem system;
        public int particleMin, particleMax;
    }
}
