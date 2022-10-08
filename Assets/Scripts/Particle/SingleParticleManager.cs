using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SingleParticleManager : MonoBehaviour {

    [SerializeField] private ParticlePair[] serializedSystems;
    private Dictionary<Enums.Particle, ParticleSystem> systems;

    public void Start() {
        systems = serializedSystems.ToDictionary(pp => pp.particle, pp => pp.system);
    }

    public void Play(Enums.Particle particle, Vector3 position, Color? color = null) {
        if (!systems.ContainsKey(particle))
            return;

        ParticleSystem system = systems[particle];

        if (color != null) {
            ParticleSystem.MainModule main = system.main;
            main.startColor = color.Value;
        }

        system.transform.position = position;
        system.Play();
    }

    [Serializable]
    public struct ParticlePair {
        public Enums.Particle particle;
        public ParticleSystem system;
    }
}
