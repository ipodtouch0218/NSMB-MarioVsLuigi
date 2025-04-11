using JimmysUnityUtilities;
using NSMB.Extensions;
using Quantum;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ResultsHandler : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private GameObject parent;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private ResultsEntry[] entries;
    [SerializeField] private RectTransform header, ui;
    [SerializeField] private CanvasGroup fadeGroup; 
    [SerializeField] private LoopingMusicData musicData;
    [SerializeField] private float delayUntilStart = 5.5f, delayPerEntry = 0.05f, replayDelayUntilStart = 3f;

    //---Private Variables
    private Coroutine endingCoroutine, moveUiCoroutine, moveHeaderCoroutine, fadeCoroutine;

    public void OnValidate() {
        this.SetIfNull(ref parentCanvas, UnityExtensions.GetComponentType.Parent);
    }

    public unsafe void Start() {
        QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
        QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
        parent.SetActive(false);

        if (NetworkHandler.Game != null) {
            Frame f = NetworkHandler.Game.Frames.Predicted;
            if (f.Global->GameState == GameState.Ended) {
                endingCoroutine = StartCoroutine(RunEndingSequence(f, 0));
            }
        }
    }

    private void OnGameEnded(EventGameEnded e) {
        if (!e.EndedByHost || NetworkHandler.IsReplay) {
            endingCoroutine = StartCoroutine(RunEndingSequence(e.Game.Frames.Predicted, NetworkHandler.IsReplay ? replayDelayUntilStart : delayUntilStart));
        }
    }

    private IEnumerator RunEndingSequence(Frame f, float delay) {
        yield return new WaitForSeconds(delay);

        parent.SetActive(true);
        FindObjectOfType<LoopingMusicPlayer>().Play(musicData);
        InitializeResultsEntries(f, 0);
        moveHeaderCoroutine = StartCoroutine(MoveObjectToTarget(header, -1.25f, 0, 1/3f));
        moveUiCoroutine = StartCoroutine(MoveObjectToTarget(ui, 1.25f, 0, 1/3f));
        fadeCoroutine = StartCoroutine(OtherUIFade());
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) header.parent);
    }

    public unsafe void InitializeResultsEntries(Frame f, float additionalDelay) {
        // Generate scores
        Dictionary<int, int> teamRankings = null;
        if (f.Global->HasWinner) {
            byte[] teamScores = new byte[10];
            QuantumUtils.GetTeamStars(f, teamScores);

            Dictionary<int, int> teamScoresDict = new();
            for (int i = 0; i < teamScores.Length; i++) {
                teamScoresDict[i] = teamScores[i];
            }

            teamRankings = new();
            int previousStarCount = -1;
            int repeatedCount = 0;
            int currentRanking = 1;
            foreach ((int teamIndex, int stars) in teamScoresDict.OrderByDescending(x => x.Value)) {
                if (previousStarCount == stars) {
                    repeatedCount++;
                    teamRankings[teamIndex] = currentRanking - 1;
                } else {
                    currentRanking += repeatedCount;
                    teamRankings[teamIndex] = currentRanking;
                    currentRanking++;
                    previousStarCount = stars;
                    repeatedCount = 0;
                }
            }
        }

        // Initialize results screen by player star counts
        int initializeCount = 0;
        List<PlayerInformation> infos = new();
        for (int i = 0; i < f.Global->RealPlayers; i++) {
            infos.Add(f.Global->PlayerInfo[i]);
        }
        foreach (var info in infos.OrderByDescending(x => x.GetStarCount(f))) {
            int rank = teamRankings != null ? teamRankings[info.Team] : -1;
            entries[initializeCount].Initialize(f, info, rank, (initializeCount * delayPerEntry) + additionalDelay, info.GetStarCount(f));
            initializeCount++;
        }

        // Initialize remaining scores
        for (int i = initializeCount; i < entries.Length; i++) {
            entries[i].Initialize(f, null, -1, (i * delayPerEntry) + additionalDelay);
        }
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
        while ((delay -= Time.deltaTime) > 0) {
            obj.SetAnchoredPositionX(start * obj.rect.size.x);
            yield return null;
        }

        float timer = moveTime;
        while (timer > 0) {
            timer -= Time.deltaTime;
            obj.SetAnchoredPositionX(Mathf.Lerp(end, start, timer / moveTime) * obj.rect.size.x);
            yield return null;
        }
    }

    private unsafe void OnGameResynced(CallbackGameResynced e) {
        Frame f = e.Game.Frames.Predicted;
        fadeGroup.alpha = 1f;
        if (f.Global->GameState == GameState.Ended) {
            endingCoroutine = StartCoroutine(RunEndingSequence(f, 0));
        } else {
            parent.SetActive(false);
            this.StopCoroutineNullable(ref endingCoroutine);
            this.StopCoroutineNullable(ref moveHeaderCoroutine);
            this.StopCoroutineNullable(ref moveUiCoroutine);
            this.StopCoroutineNullable(ref fadeCoroutine);
        }
    }
}