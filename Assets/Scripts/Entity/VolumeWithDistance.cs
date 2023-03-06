using UnityEngine;

using NSMB.Utils;

public class VolumeWithDistance : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private AudioSource[] audioSources;
    [SerializeField] private Transform soundOrigin;
    [SerializeField] private float soundRange = 12f;
    //[SerializeField] private float maxPanning = 0.8f;

    private float soundRangeInverse;
    private float maxPanning = 0.8f;

    public void OnValidate() => Awake();

    public void Awake() {
        soundRangeInverse = 1f / soundRange;
    }

    public void LateUpdate() {

        GameManager inst = GameManager.Instance;
        Vector3 listener = (inst != null && inst.localPlayer) ? inst.localPlayer.transform.position : Camera.main.transform.position;

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
        float panning = Utils.QuadraticEaseOut(-xDifference * soundRangeInverse) * maxPanning;

        foreach (AudioSource source in audioSources) {
            source.volume = volume;
            source.panStereo = panning;
        }
    }
}