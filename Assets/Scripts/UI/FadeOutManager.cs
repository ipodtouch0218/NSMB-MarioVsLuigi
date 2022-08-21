using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeOutManager : MonoBehaviour {

    public float fadeTimer, fadeTime, blackTime, totalTime;

    private Image image;
    private Coroutine fadeCoroutine;

    public void Start() {
        image = GetComponent<Image>();
    }

    private IEnumerator Fade() {
        while (fadeTimer > -totalTime) {
            fadeTimer -= Time.deltaTime;
            image.color = new(0, 0, 0, 1 - Mathf.Clamp01((Mathf.Abs(fadeTimer) - blackTime) / fadeTime));
            yield return null;
        }
        image.color = new(0, 0, 0, 0);
        fadeCoroutine = null;
    }

    public void FadeOutAndIn(float fadeTime, float blackTime) {
        this.fadeTime = fadeTime;
        this.blackTime = blackTime;
        totalTime = fadeTime + blackTime;
        fadeTimer = totalTime;

        if (fadeCoroutine == null)
            fadeCoroutine = StartCoroutine(Fade());
    }
}