using UnityEngine;

public class ParticleSound : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private ParticleSystem system;
    [SerializeField] private AudioSource sfx;

    public void OnValidate() {
        if (!system) system = GetComponent<ParticleSystem>();
        if (!sfx) sfx = GetComponent<AudioSource>();
    }

    public void Awake() {
        OnValidate();
    }

    public void Update() {
        if (!system || !sfx) {
            enabled = false;
            return;
        }

        if (system.isEmitting && !sfx.isPlaying)
            sfx.Play();
        if (!system.isEmitting && sfx.isPlaying)
            sfx.Stop();
    }
}
