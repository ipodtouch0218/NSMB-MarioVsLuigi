using NSMB.Extensions;
using NSMB.UI.Game;
using NSMB.Utils;
using Photon.Deterministic;
using Quantum;
using UnityEngine;

public class VolumeWithDistance : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private AudioSource[] audioSources;
    [SerializeField] private Transform soundOrigin;
    [SerializeField] private float soundRange = 12f;
    [SerializeField] private float maxPanning = 0.8f;

    //---Private Variables
    private float soundRangeInverse;
    private float[] originalVolumes;
    private VersusStageData stage;

    public void OnValidate() {
        this.SetIfNull(ref audioSources);
        this.SetIfNull(ref soundOrigin);
    }

    public void Start() {
        soundRangeInverse = 1f / soundRange;
        originalVolumes = new float[audioSources.Length];

        for (int i = 0; i < audioSources.Length; i++) {
            originalVolumes[i] = audioSources[i].volume;
        }

        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
        LateUpdate();
    }

    public void LateUpdate() {
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
