using UnityEngine;

using NSMB.Utils;

public class VolumeWithDistance : MonoBehaviour {

    [SerializeField] private AudioSource[] audioSources;
    [SerializeField] private Transform soundOrigin;
    [SerializeField] private float soundRange = 12f;

    public void Update() {

        GameManager inst = GameManager.Instance;
        Vector3 listener = (inst != null && inst.localPlayer) ? inst.localPlayer.transform.position : Camera.main.transform.position;

        float volume = Utils.QuadraticEaseOut(1 - Mathf.Clamp01(Utils.WrappedDistance(listener, soundOrigin.position) / soundRange));

        foreach (AudioSource source in audioSources)
            source.volume = volume;
    }
}