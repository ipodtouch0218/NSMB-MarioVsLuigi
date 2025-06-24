using JimmysUnityUtilities;
using NSMB.Sound;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.UI.Game.Results {
    public class ResultsHandler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private GameObject parent;
        [SerializeField] private Canvas parentCanvas;
        [SerializeField] private ResultsEntry template;
        [SerializeField] private RectTransform header, ui;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private LoopingMusicData musicData;
        [SerializeField] private float delayUntilStart = 5.5f, delayPerEntry = 0.05f, replayDelayUntilStart = 3f;

        //---Private Variables
        private Coroutine endingCoroutine, moveUiCoroutine, moveHeaderCoroutine, fadeCoroutine;
        private readonly List<ResultsEntry> entries = new();

        public void OnValidate() {
            this.SetIfNull(ref parentCanvas, UnityExtensions.GetComponentType.Parent);
        }

        public unsafe void Start() {
            for (int i = 0; i < Constants.MaxPlayers; i++) {
                ResultsEntry newEntry = Instantiate(template, template.transform.parent);
                newEntry.gameObject.SetActive(true);
                entries.Add(newEntry);
            }
            template.gameObject.SetActive(false);

            QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            parent.SetActive(false);

            var game = QuantumRunner.DefaultGame;
            if (game != null) {
                Frame f = game.Frames.Predicted;
                if (f.Global->GameState == GameState.Ended) {
                    endingCoroutine = StartCoroutine(RunEndingSequence(f, 0));
                }
            }
        }

        private void OnGameEnded(EventGameEnded e) {
            if (!e.EndedByHost || IsReplay) {
                endingCoroutine = StartCoroutine(RunEndingSequence(e.Game.Frames.Predicted, IsReplay ? replayDelayUntilStart : delayUntilStart));
            }
        }

        private IEnumerator RunEndingSequence(Frame f, float delay) {
            yield return new WaitForSeconds(delay);

            parent.SetActive(true);
            FindFirstObjectByType<LoopingMusicPlayer>().Play(musicData);
            InitializeResultsEntries(f, 0);
            moveHeaderCoroutine = StartCoroutine(MoveObjectToTarget(header, -1.25f, 0, 1/3f));
            moveUiCoroutine = StartCoroutine(MoveObjectToTarget(ui, 1.25f, 0, 1/3f));
            fadeCoroutine = StartCoroutine(OtherUIFade());
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) header.parent);
        }

        public unsafe void InitializeResultsEntries(Frame f, float additionalDelay) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);

            // Generate scores
            Dictionary<int, int> teamRankings = null;
            if (f.Global->HasWinner) {
                Span<int> teamScores = stackalloc int[10];
                gamemode.GetAllTeamsObjectiveCounts(f, teamScores);

                Dictionary<int, int> teamScoresDict = new();
                for (int i = 0; i < teamScores.Length; i++) {
                    teamScoresDict[i] = teamScores[i];
                }

                teamRankings = new();
                int previousObjectiveCount = -1;
                int repeatedCount = 0;
                int currentRanking = 1;
                foreach ((int teamIndex, int objectiveCount) in teamScoresDict.OrderByDescending(x => x.Value)) {
                    if (objectiveCount < 0) {
                        teamRankings[teamIndex] = Constants.MaxPlayers;
                    } else if (previousObjectiveCount == objectiveCount) {
                        repeatedCount++;
                        teamRankings[teamIndex] = currentRanking - 1;
                    } else {
                        currentRanking += repeatedCount;
                        teamRankings[teamIndex] = currentRanking;
                        currentRanking++;
                        previousObjectiveCount = objectiveCount;
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
            infos.Sort((x, y) => {
                int objectiveDiff = gamemode.GetObjectiveCount(f, y.PlayerRef) - gamemode.GetObjectiveCount(f, x.PlayerRef);
                if (objectiveDiff != 0) {
                    return objectiveDiff;
                }

                int xRank = teamRankings != null ? teamRankings[x.Team] : Constants.MaxPlayers;
                int yRank = teamRankings != null ? teamRankings[y.Team] : Constants.MaxPlayers;
                return xRank - yRank;
            });
            foreach (var info in infos) {
                int rank = teamRankings != null ? teamRankings[info.Team] : Constants.MaxPlayers;
                entries[initializeCount].Initialize(f, gamemode, info, rank, (initializeCount * delayPerEntry) + additionalDelay, gamemode.GetObjectiveCount(f, info.PlayerRef));
                initializeCount++;
            }

            // Initialize remaining scores
            for (int i = initializeCount; i < entries.Count; i++) {
                entries[i].Initialize(f, gamemode, null, -1, (i * delayPerEntry) + additionalDelay);
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
}