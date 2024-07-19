using UnityEngine;

using NSMB.Utils;
using Photon.Deterministic;
using Quantum;

public class VolumeWithDistance : QuantumCallbacks {

    //---Serialized Variables
    [SerializeField] private AudioSource[] audioSources;
    [SerializeField] private Transform soundOrigin;
    [SerializeField] private float soundRange = 12f;
    [SerializeField] private float maxPanning = 0.8f;
    [SerializeField] private bool useDistanceToCamera;

    //---Private Variables
    private float soundRangeInverse;
    private float[] originalVolumes;

    public void OnValidate() {
        if (audioSources?.Length <= 0) {
            audioSources = GetComponentsInChildren<AudioSource>();
        }

        if (!soundOrigin) {
            soundOrigin = transform;
        }
    }

    public void Awake() {
        soundRangeInverse = 1f / soundRange;
        originalVolumes = new float[audioSources.Length];

        for (int i = 0; i < audioSources.Length; i++) {
            originalVolumes[i] = audioSources[i].volume;
        }
    }

    public void LateUpdate() {
        // TODO FPVector2 listener = (!useDistanceToCamera && gm.localPlayer) ? gm.localPlayer.transform.position : Camera.main.transform.position;
        FPVector2 listener = Camera.main.transform.position.ToFPVector2();

        Frame f = QuantumRunner.DefaultGame.Frames.Predicted;

        float distance = QuantumUtils.WrappedDistance(f, listener, soundOrigin.position.ToFPVector2(), out FP xDifference).AsFloat;
        if (distance > soundRange) {
            foreach (AudioSource source in audioSources) {
                source.volume = 0;
                source.panStereo = 0;
            }
            return;
        }

        float percentage = 1f - (distance * soundRangeInverse);
        float volume = Utils.QuadraticEaseOut(percentage);
        float panning = Settings.Instance.audioPanning ? Utils.QuadraticEaseOut(-xDifference.AsFloat * soundRangeInverse) * maxPanning : 0f;

        for (int i = 0; i < audioSources.Length; i++) {
            AudioSource source = audioSources[i];
            source.volume = volume * originalVolumes[i];
            source.panStereo = panning;
        }
    }
}
