using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSound : MonoBehaviour {

    private ParticleSystem system;
    private AudioSource sfx;

    void Start() {
        system = GetComponent<ParticleSystem>();
        sfx = GetComponent<AudioSource>();
    }

    void Update() {
        if (system.isEmitting && !sfx.isPlaying)
            sfx.Play();
        if (!system.isEmitting && sfx.isPlaying)
            sfx.Stop();
    }
}
