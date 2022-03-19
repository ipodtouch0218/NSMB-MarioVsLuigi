using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FadeOutManager : MonoBehaviour {

    private Image image;
    public float fadeTimer, fadeTime, blackTime, totalTime;

    public void Start() {
        image = GetComponent<Image>();
    }

    public void Update() {
        fadeTimer = Mathf.Clamp(fadeTimer - Time.deltaTime, -totalTime, totalTime);
        image.color = new Color(0, 0, 0, 1 - Mathf.Clamp01((Mathf.Abs(fadeTimer)-blackTime) / fadeTime));
    }

    public void FadeOutAndIn(float fadeTime, float blackTime) {
        this.fadeTime = fadeTime;
        this.blackTime = blackTime;
        totalTime = fadeTime + blackTime;
        fadeTimer = totalTime;
    }
}