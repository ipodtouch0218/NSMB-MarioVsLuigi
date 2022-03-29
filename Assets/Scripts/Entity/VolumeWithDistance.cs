using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumeWithDistance : MonoBehaviour {

    public float soundRange = 12f;

    public Transform soundOrigin;
    public AudioSource[] audioSources;

    public void Update() {

        Vector3 listener;
        if (GameManager.Instance && GameManager.Instance.localPlayer) {
            listener = GameManager.Instance.localPlayer.transform.position;
            if (Mathf.Abs(listener.x - soundOrigin.position.x) > GameManager.Instance.levelWidthTile / 4f)
                listener.x -= GameManager.Instance.levelWidthTile / 2f * Mathf.Sign(listener.x - soundOrigin.position.x);

        } else {
            listener = Camera.main.transform.position;
        }

        float volume = Utils.QuadraticEaseOut(1 - Mathf.Clamp01(Vector2.Distance(listener, soundOrigin.position) / soundRange));

        foreach (AudioSource source in audioSources)
            source.volume = volume;
    }
}