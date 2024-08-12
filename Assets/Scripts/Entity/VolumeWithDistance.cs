using UnityEngine;

using NSMB.Utils;
using Photon.Deterministic;
using Quantum;
using NSMB.Extensions;
using System.Linq;

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
    private VersusStageData stage;

    public void OnValidate() {
        if (audioSources?.Length <= 0) {
            audioSources = GetComponentsInChildren<AudioSource>();
        }

        this.SetIfNull(ref soundOrigin);
    }

    public void Awake() {
        soundRangeInverse = 1f / soundRange;
        originalVolumes = new float[audioSources.Length];

        for (int i = 0; i < audioSources.Length; i++) {
            originalVolumes[i] = audioSources[i].volume;
        }

        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
    }

    public void OnUpdateView(CallbackUpdateView e) {
        /*
        bool anyPlaying = false;
        foreach (var item in audioSources) {
            if (item.isPlaying) {
                anyPlaying = true;
                break;
            }
        }

        if (!anyPlaying) {
            return;
        }
        */

        float minDistance = float.MaxValue;
        FP xDifference = 0;
        foreach (var pe in PlayerElements.AllPlayerElements) {
            float distance = QuantumUtils.WrappedDistance(stage, pe.Camera.transform.position.ToFPVector2(), soundOrigin.position.ToFPVector2(), out FP tempXDifference).AsFloat;
            if (distance < minDistance) {
                minDistance = distance;
                xDifference = tempXDifference;
            }
        }

        if (minDistance > soundRange) {
            foreach (AudioSource source in audioSources) {
                source.volume = 0;
                source.panStereo = 0;
            }
            return;
        }

        float percentage = 1f - (minDistance * soundRangeInverse);
        float volume = Utils.QuadraticEaseOut(percentage);
        float panning = Settings.Instance.audioPanning ? Utils.QuadraticEaseOut(-xDifference.AsFloat * soundRangeInverse) * maxPanning : 0f;

        for (int i = 0; i < audioSources.Length; i++) {
            AudioSource source = audioSources[i];
            source.volume = volume * originalVolumes[i];
            source.panStereo = panning;
        }
    }
}
