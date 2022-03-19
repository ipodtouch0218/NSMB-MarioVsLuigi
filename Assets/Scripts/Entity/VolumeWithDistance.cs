using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumeWithDistance : MonoBehaviour {

    public float soundRange = 12f;

    public Transform soundOrigin;
    public AudioSource[] audioSources;

    public void Update() {

        float volume;
        if (GameManager.Instance && GameManager.Instance.localPlayer) {
            Vector3 localPl = GameManager.Instance.localPlayer.transform.position;

            if (Mathf.Abs(localPl.x - soundOrigin.position.x) > GameManager.Instance.levelWidthTile / 4f)
                localPl.x -= GameManager.Instance.levelWidthTile / 2f * Mathf.Sign(localPl.x - soundOrigin.position.x);

            volume = Utils.QuadraticEaseOut(1 - Mathf.Clamp01(Mathf.Abs(localPl.x - soundOrigin.position.x) / soundRange));
        } else {
            Vector2 camera = Camera.main.transform.position;
            volume = Utils.QuadraticEaseOut(1 - Mathf.Clamp01(Vector2.Distance(camera, soundOrigin.position) / soundRange));
        }

        foreach (AudioSource source in audioSources)
            source.volume = volume;
    }
}