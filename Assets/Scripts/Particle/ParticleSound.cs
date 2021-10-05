using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSound : MonoBehaviour {

    private ParticleSystem system;
    new private AudioSource audio;

    void Start() {
        system = GetComponent<ParticleSystem>();
        audio = GetComponent<AudioSource>();
    }

    void Update() {
        if (system.isEmitting && !audio.isPlaying) {
            audio.Play();
        }

        if (!system.isEmitting && audio.isPlaying) {
            audio.Stop();
        }
    }
}
