using JimmysUnityUtilities;
using Quantum;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ResultsHandler : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private GameObject parent;
    [SerializeField] private ResultsEntry[] entries;
    [SerializeField] private RectTransform header;
    [SerializeField] private CanvasGroup fadeGroup; 
    [SerializeField] private LoopingMusicData musicData;
    [SerializeField] private float delayUntilStart = 5.5f, delayPerEntry = 0.05f;

    public void Start() {
        QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
        parent.SetActive(false);
    }

    private void OnGameEnded(EventGameEnded e) {
        StartCoroutine(Delay(e.Frame, delayUntilStart));
    }

    private IEnumerator Delay(Frame f, float delay) {
        yield return new WaitForSeconds(delay);

        parent.SetActive(true);
        FindObjectOfType<LoopingMusicPlayer>().Play(musicData);
        for (int i = 0; i < entries.Length; i++) {
            entries[i].Initialize(f, i, i, i * delayPerEntry);
        }

        StartCoroutine(MoveObjectToTarget(header, -500, 0, 1/3f));
        StartCoroutine(OtherUIFade());
    }

    private IEnumerator OtherUIFade() {
        float time = 0.333f;
        while (time > 0) {
            time -= Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(0, 1, time / 0.333f);
            yield return null;
        }
    }

    public static IEnumerator MoveObjectToTarget(RectTransform obj, float start, float end, float moveTime, float delay = 0) {
        obj.SetAnchoredPositionX(start);
        if (delay > 0) {
            yield return new WaitForSeconds(delay);
        }

        float timer = moveTime;
        while (timer > 0) {
            timer -= Time.deltaTime;
            obj.SetAnchoredPositionX(Mathf.Lerp(end, start, timer / moveTime));
            yield return null;
        }
    }
}