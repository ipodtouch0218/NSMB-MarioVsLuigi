using UnityEngine;

using NSMB.Game;
using NSMB.Utils;

public class VolumeWithDistance : MonoBehaviour {

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
        GameManager inst = GameManager.Instance;
        if (!inst) {
            return;
        }

        Vector3 listener = (!useDistanceToCamera && inst && inst.localPlayer) ? inst.localPlayer.transform.position : Camera.main.transform.position;

        float distance = Utils.WrappedDistance(listener, soundOrigin.position, out float xDifference);
        if (distance > soundRange) {
            foreach (AudioSource source in audioSources) {
                source.volume = 0;
                source.panStereo = 0;
            }
            return;
        }

        float percentage = 1f - (distance * soundRangeInverse);
        float volume = Utils.QuadraticEaseOut(percentage);
        float panning = Settings.Instance.audioPanning ? Utils.QuadraticEaseOut(-xDifference * soundRangeInverse) * maxPanning : 0f;

        for (int i = 0; i < audioSources.Length; i++) {
            AudioSource source = audioSources[i];
            source.volume = volume * originalVolumes[i];
            source.panStereo = panning;
        }
    }
}
