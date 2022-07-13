using NSMB.Utils;
using UnityEngine;

public class VolumeWithDistance : MonoBehaviour {

    public float soundRange = 12f;

    public Transform soundOrigin;
    public AudioSource[] audioSources;

    public void Update() {

        GameManager inst = GameManager.Instance;
        Vector3 listener = (inst && inst.localPlayer) ? inst.localPlayer.transform.position : Camera.main.transform.position;

        float volume = Utils.QuadraticEaseOut(1 - Mathf.Clamp01(Utils.WrappedDistance(listener, soundOrigin.position) / soundRange));

        foreach (AudioSource source in audioSources)
            source.volume = volume;
    }
}