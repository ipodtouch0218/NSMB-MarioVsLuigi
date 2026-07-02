using JimmysUnityUtilities;
using NSMB.Replay;
using NSMB.Replay.Stats;
using NSMB.Sound;
using NSMB.UI.Pause;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.Game.Replay {
    public class ReplayUI : QuantumSceneViewComponent {

        public bool IsOpen => replayUI.activeSelf;

        //---Serialized Variables
        [SerializeField] public PlayerElements playerElements;

        [SerializeField] private GameObject replayUI, simulatingCanvas;
        [SerializeField] private Transform trackArrow, simulationTargetTrackArrow;
        [SerializeField] private RectMask2D trackBufferMask;
        [SerializeField] private TMP_Text trackArrowText;
        [SerializeField] private float minTrackX = -180, maxTrackX = 180;
        [SerializeField] private TMP_Text replayTimecode;
        [SerializeField] private TMP_Text replayPauseButton;
        [SerializeField] private CanvasGroup replayCanvasGroup;
        [SerializeField] private GameObject defaultSelection, markerTemplate;

        [SerializeField] private GameObject tabBlocker;
        [SerializeField] private GameObject restartButton;

        //---Properties
        public bool TabOpen => activeTab;
        public float ReplaySpeed => replaySpeed;

        //---Private Variables
        private float replaySpeed = 1;
        private int fastForwardDestinationTick, previousTimestampSeconds;
        private bool replayPaused;
        private bool draggingArrow;
        private string replayLength;
        private StringBuilder builder = new();
        private ReplayUITab activeTab;
        private bool gameEnded;
        private bool disableTimePointMarkers;
        private Frame resetFrame;

        private FieldInfo sessionSimulationField;
        private MethodInfo simulationAdjustTimeMethod;
        private readonly List<GameObject> markers = new();
        private readonly List<Image> markerImages = new();

        //---Formulas
        private int GetFastForwardTick(int frameNum) => frameNum + ActiveReplayManager.Instance.ReplayStart;

        public void OnValidate() {
            this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
        }

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
        }

        public override void OnActivate(Frame f) {
            var replayManager = ActiveReplayManager.Instance;
            replayUI.SetActive(replayManager.IsReplay);
            if (!replayManager.IsReplay) {
                enabled = false;
                return;
            }

            resetFrame = Game.CreateFrame();
            replayCanvasGroup.interactable = true;
            replayLength = Utils.SecondsToMinuteSeconds(replayManager.ReplayLength / f.UpdateRate);
            disableTimePointMarkers = replayManager.SelectedTimePoint == null;
            if (disableTimePointMarkers) {
                restartButton.SetActive(false);
            }
            trackArrowText.gameObject.SetActive(false);
            Settings.Controls.UI.Pause.performed += OnPause;

            if (replayManager.ReplayStartFrame != null) {
                SetToFrame(f, replayManager.ReplayStartFrame.Value);
            }
        }

        public void OnDestroy() {
            Time.timeScale = 1;
            if (ActiveReplayManager.Instance) {
                ActiveReplayManager.Instance.IsReplayFastForwarding = false;
            }
            Settings.Controls.UI.Pause.performed -= OnPause;

            PurgeMarkers();
        }

        public void Update() {
            if (ActiveReplayManager.Instance.IsReplayFastForwarding) {
                float update = Time.deltaTime;
                var runner = QuantumRunner.Default;
                Frame f = runner.Game.Frames.Predicted;
                float maxDelta = (fastForwardDestinationTick - f.Number) * f.DeltaTime.AsFloat;

                bool done = update >= maxDelta;
                if (done) {
                    update = maxDelta;
                }

                runner.Session.Update(update);

                // Fix accumulated time applying
                ResetAdjustedTime(runner.Session);

                if (done) {
                    FinishFastForward();
                }
            }

            GameObject current = EventSystem.current.currentSelectedGameObject;
            if (!current || !current.activeInHierarchy) {
                EventSystem.current.SetSelectedGameObject(defaultSelection);
            }
        }

        private unsafe void FinishFastForward() {
            ActiveReplayManager.Instance.IsReplayFastForwarding = false;
            simulatingCanvas.SetActive(false);
            fastForwardDestinationTick = 0;
            Time.captureDeltaTime = 0;

            if (gameEnded) {
                Time.timeScale = 1;
            } else if (replayPaused) {
                Time.timeScale = 0;
            } else {
                Time.timeScale = replaySpeed;
            }

            QuantumRunner.Default.IsSessionUpdateDisabled = false;
            simulationTargetTrackArrow.gameObject.SetActive(false);

            if (QuantumRunner.DefaultGame.Frames.Predicted.Global->GameState != GameState.Playing) {
                FindFirstObjectByType<LoopingMusicPlayer>().Stop();
            }
        }

        public override void OnUpdateView() {
            Frame f = PredictedFrame;

            float currentFrameNumber = PredictedPreviousFrame.Number + Game.InterpolationFactor;
            int currentSeconds = Mathf.FloorToInt((currentFrameNumber - ActiveReplayManager.Instance.ReplayStart) / f.UpdateRate);
            if (previousTimestampSeconds != currentSeconds) {
                builder.Clear();
                builder.Append(Utils.SecondsToMinuteSeconds(currentSeconds));
                builder.Append('/').Append(replayLength);
                replayTimecode.SetText(builder);

                previousTimestampSeconds = currentSeconds;
            }

            if (disableTimePointMarkers || !ActiveReplayManager.Instance.SelectedTimePoint.HasEndFrame) {
                float bufferPercentage = (float) ActiveReplayManager.Instance.ReplayFrameCache.Count * f.UpdateRate * 5 / ActiveReplayManager.Instance.ReplayLength;
                trackBufferMask.rectTransform.SetAnchorMinX(0);
                trackBufferMask.rectTransform.SetAnchorMaxX(Mathf.Clamp01(bufferPercentage));
            }

            if (draggingArrow && (playerElements.PauseMenu.IsPaused || !replayCanvasGroup.interactable)) {
                CancelArrowDrag();
            }

            float percentage;
            if (draggingArrow) {
                Vector2 mousePositionPixels = Settings.Controls.UI.Point.ReadValue<Vector2>();
                Vector2 mousePositionTrack = trackArrow.transform.parent.InverseTransformPoint(mousePositionPixels);

                float newX = Mathf.Clamp(mousePositionTrack.x, minTrackX, maxTrackX);
                percentage = (newX - minTrackX) / (maxTrackX - minTrackX);

                trackArrowText.text = Utils.SecondsToMinuteSeconds(Mathf.FloorToInt(percentage * ActiveReplayManager.Instance.ReplayLength / f.UpdateRate));
            } else {
                percentage = (currentFrameNumber - ActiveReplayManager.Instance.ReplayStart) / ActiveReplayManager.Instance.ReplayLength;
            }

            trackArrow.localPosition = new Vector3(percentage * (maxTrackX - minTrackX) + minTrackX, 0, 0);

            if (disableTimePointMarkers) {
                return;
            }

            var (statMarkers, startFrame, endFrame) = ActiveReplayManager.Instance.SelectedTimePoint.ReplayMarkerData(f.Number, TimePoint.DisplayArgs.Normal);

            // UPdate the lighter part of the track if it has an end frame (which can be the end of the replay)
            if (ActiveReplayManager.Instance.SelectedTimePoint != null && endFrame != -1) {
                float anchorStart = (float) (startFrame - ActiveReplayManager.Instance.ReplayStart) / ActiveReplayManager.Instance.ReplayLength;
                float anchorEnd = statMarkers.Length < 2 ? 1 : (float) (endFrame - ActiveReplayManager.Instance.ReplayStart) / ActiveReplayManager.Instance.ReplayLength;
                trackBufferMask.rectTransform.SetAnchorMinX(Mathf.Clamp01(anchorStart - .011f));
                trackBufferMask.rectTransform.SetAnchorMaxX(Mathf.Clamp01(anchorEnd + .011f));
            }

            if (statMarkers != null) {
                for (int i = 0; i < statMarkers.Length; i++) {
                    var statMarkerInfo = statMarkers[i];
                    float markerNorm = (statMarkerInfo.frame + Game.InterpolationFactor - ActiveReplayManager.Instance.ReplayStart) / ActiveReplayManager.Instance.ReplayLength;
                    GameObject marker;
                    Image markerImage;

                    // use cache
                    if (i >= markers.Count) {
                        marker = Instantiate(markerTemplate, markerTemplate.transform.parent);
                        marker.SetActive(true);
                        marker.name = $"Marker{i}";
                        markerImage = marker.GetComponent<Image>();
                        markers.Add(marker);
                        markerImages.Add(markerImage);
                    } else {
                        marker = markers[i];
                        markerImage = markerImages[i];
                    }
                    marker.transform.localPosition = new Vector3(markerNorm * (maxTrackX - minTrackX) + minTrackX, 0, 0);
                    markerImage.color = statMarkerInfo.color;
                }
            }
        }

        public void OpenTab(ReplayUITab tab) {
            CloseTab();
            activeTab = tab;
            tab.gameObject.SetActive(true);
            tabBlocker.SetActive(true);
        }

        public void CloseTab() {
            if (!activeTab) {
                return;
            }

            activeTab.gameObject.SetActive(false);
            activeTab = null;
            tabBlocker.SetActive(false);
            PauseMenuManager.UnpauseTime = Time.unscaledTime;
        }

        public bool ToggleReplayControls() {
            replayUI.SetActive(!replayUI.activeSelf);
            //playerElements.spectationUI.SetActive(replayUI.activeSelf);
            return replayUI.activeSelf;
        }

        public void RestartAtTimePoint() {
            if (!ActiveReplayManager.Instance.IsReplay || disableTimePointMarkers) {
                return;
            }

            SetToFrame(QuantumRunner.DefaultGame.Frames.Predicted, ActiveReplayManager.Instance.ReplayStartFrame.Value);
        }

        public void RewindReplay() {
            if (!ActiveReplayManager.Instance.IsReplay) {
                return;
            }

            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            int currentIndex = (f.Number - ActiveReplayManager.Instance.ReplayStart) / (5 * f.UpdateRate);
            int newIndex = Mathf.Max(currentIndex - 1, 0);
            //int newFrame = (newIndex * (5 * f.UpdateRate)) + ActiveReplayManager.Instance.ReplayStart;

            var session = QuantumRunner.Default.Session;

            ActiveReplayManager.Instance.IsReplayFastForwarding = true;
            resetFrame.Deserialize(ActiveReplayManager.Instance.ReplayFrameCache[newIndex]);
            session.ResetReplay(resetFrame);

            // Fix accumulated time applying
            ResetAdjustedTime(session);

            ActiveReplayManager.Instance.IsReplayFastForwarding = false;
        }

        public void FastForwardReplay() {
            if (!ActiveReplayManager.Instance.IsReplay) {
                return;
            }

            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            int currentIndex = (f.Number - ActiveReplayManager.Instance.ReplayStart) / (5 * f.UpdateRate);
            int newIndex = currentIndex + 1;
            int newFrame = Mathf.Min((newIndex * (5 * f.UpdateRate)) + ActiveReplayManager.Instance.ReplayStart, ActiveReplayManager.Instance.ReplayEnd);
            
            var session = QuantumRunner.Default.Session;
            if (newIndex < ActiveReplayManager.Instance.ReplayFrameCache.Count) {
                // We already have this frame
                ActiveReplayManager.Instance.IsReplayFastForwarding = true;
                resetFrame.Deserialize(ActiveReplayManager.Instance.ReplayFrameCache[newIndex]);
                session.ResetReplay(resetFrame);

                // Fix accumulated time applying
                ResetAdjustedTime(session);

                ActiveReplayManager.Instance.IsReplayFastForwarding = false;
            } else {
                // We have to simulate up to this frame
                ActiveReplayManager.Instance.IsReplayFastForwarding = true;
                simulatingCanvas.SetActive(true);
                fastForwardDestinationTick = newFrame;
                QuantumRunner.Default.IsSessionUpdateDisabled = true;
                Time.captureDeltaTime = 1/30f;
                Time.timeScale = 8;
                simulationTargetTrackArrow.position = trackArrow.position;
                simulationTargetTrackArrow.gameObject.SetActive(true);
            }
        }

        public void PausePlayReplay() {
            replayPaused = !replayPaused;
            if (replayPaused) {
                Time.timeScale = 0;
                replayPauseButton.text = "►";
            } else {
                Time.timeScale = replaySpeed;
                replayPauseButton.text = "II";
            }
        }

        public void FrameAdvance() {
            replayPaused = true;
            Time.timeScale = 0;
            replayPauseButton.text = "►";

            StartCoroutine(FrameAdvanceCoroutine());
        }

        private IEnumerator FrameAdvanceCoroutine() {
            Frame f = PredictedFrame;
            Time.timeScale = 1;
            Time.captureDeltaTime = f.DeltaTime.AsFloat;
            yield return null;
            if (!Game.Session.IsReplayFinished) {
                Time.timeScale = 0;
                Time.captureDeltaTime = 0;
            }
        }

        public void ChangeReplaySpeed(int index) {
            float[] speeds = { 0.25f, 0.5f, 1f, 2f, 4f };
            replaySpeed = speeds[index];

            if (!replayPaused) {
                Time.timeScale = replaySpeed;
            }
        }

        public void StartArrowDrag() {
            if (replayCanvasGroup.interactable) {
                draggingArrow = true;
                trackArrowText.gameObject.SetActive(true);
            }
        }

        public void CancelArrowDrag() {
            draggingArrow = false;
            trackArrowText.gameObject.SetActive(false);
        }

        public void StopArrowDrag() {
            draggingArrow = false;
            trackArrowText.gameObject.SetActive(false);

            QuantumRunner runner = QuantumRunner.Default;
            Frame f = runner.Game.Frames.Predicted;

            float newX = Mathf.Clamp(trackArrow.localPosition.x, minTrackX, maxTrackX);
            float percentage = (newX - minTrackX) / (maxTrackX - minTrackX);
            int newFrame = Mathf.RoundToInt(percentage * ActiveReplayManager.Instance.ReplayLength) + ActiveReplayManager.Instance.ReplayStart;
            int frameOffset = newFrame - ActiveReplayManager.Instance.ReplayStart;

            // Find the closest cached frame
            int newFrameCacheIndex = frameOffset / (5 * f.UpdateRate);

            newFrameCacheIndex = Mathf.Clamp(newFrameCacheIndex, 0, ActiveReplayManager.Instance.ReplayFrameCache.Count - 1);
            int cachedFrame = (newFrameCacheIndex * (5 * f.UpdateRate)) + ActiveReplayManager.Instance.ReplayStart;

            var session = runner.Session;

            if (!disableTimePointMarkers) {
                if (newFrame > ActiveReplayManager.Instance.SelectedTimePoint.EndFrame || newFrame < ActiveReplayManager.Instance.SelectedTimePoint.OccurenceFrame) {
                    PurgeMarkers();
                    restartButton.SetActive(false);
                    disableTimePointMarkers = true;
                }
            }

            if (cachedFrame > f.Number || newFrame < f.Number) {
                ActiveReplayManager.Instance.IsReplayFastForwarding = true;
                resetFrame.Deserialize(ActiveReplayManager.Instance.ReplayFrameCache[newFrameCacheIndex]);
                session.ResetReplay(resetFrame);

                // Fix accumulated time applying
                ResetAdjustedTime(session);

                ActiveReplayManager.Instance.IsReplayFastForwarding = false;
            }

            // Simulate up to the target frame
            if (newFrame != cachedFrame) {
                ActiveReplayManager.Instance.IsReplayFastForwarding = true;
                simulatingCanvas.SetActive(true);
                fastForwardDestinationTick = newFrame;
                QuantumRunner.Default.IsSessionUpdateDisabled = true;
                Time.captureDeltaTime = 1/30f;
                Time.timeScale = 8;
                simulationTargetTrackArrow.position = trackArrow.position;
                simulationTargetTrackArrow.gameObject.SetActive(true);
            } else {
                QuantumRunner.Default.IsSessionUpdateDisabled = false;
                Time.captureDeltaTime = 0;
                Time.timeScale = 1;
            }
        }

        public void ResetReplay() {
            QuantumRunner runner = QuantumRunner.Default;
            var session = runner.Session;

            ActiveReplayManager.Instance.IsReplayFastForwarding = true;
            resetFrame.Deserialize(ActiveReplayManager.Instance.ReplayFrameCache[0]);
            session.ResetReplay(resetFrame);

            // Fix accumulated time applying
            ResetAdjustedTime(session);

            ActiveReplayManager.Instance.IsReplayFastForwarding = false;
            replayPaused = false;
            Time.timeScale = replaySpeed;
        }

        private bool SetToFrame(Frame f, int newFrame) {
            int actualFrame = newFrame - ActiveReplayManager.Instance.ReplayStart;
            int currentIndex = (actualFrame + ActiveReplayManager.Instance.ReplayStart) / (5 * f.UpdateRate);
            int cachedIndex = Mathf.Clamp(currentIndex, 0, ActiveReplayManager.Instance.ReplayFrameCache.Count - 1);

            QuantumRunner runner = QuantumRunner.Default;
            var session = runner.Session;

            // set the replay to the closest cache
            ActiveReplayManager.Instance.IsReplayFastForwarding = true;
            resetFrame.Deserialize(ActiveReplayManager.Instance.ReplayFrameCache[cachedIndex]);
            session.ResetReplay(resetFrame);

            // Fix accumulated time applying
            ResetAdjustedTime(session);

            ActiveReplayManager.Instance.IsReplayFastForwarding = false;

            // now simulate if not valid
            if (currentIndex != cachedIndex) {
                ActiveReplayManager.Instance.IsReplayFastForwarding = true;
                simulatingCanvas.SetActive(true);
                fastForwardDestinationTick = GetFastForwardTick(newFrame);
                QuantumRunner.Default.IsSessionUpdateDisabled = true;
                Time.captureDeltaTime = 1/30f;
                Time.timeScale = 8;
                simulationTargetTrackArrow.position = trackArrow.position;
                simulationTargetTrackArrow.gameObject.SetActive(true);
            } else {
                ActiveReplayManager.Instance.IsReplayFastForwarding = true;
                simulatingCanvas.SetActive(true);
                fastForwardDestinationTick = GetFastForwardTick(newFrame);
                QuantumRunner.Default.IsSessionUpdateDisabled = true;
                Time.captureDeltaTime = 1/30f;
                Time.timeScale = 8;
                simulationTargetTrackArrow.position = trackArrow.position;
                simulationTargetTrackArrow.gameObject.SetActive(true);
                return true;
            }

            return false;
        }

        private void ResetAdjustedTime(DeterministicSession session) {
            sessionSimulationField ??= typeof(DeterministicSession).GetField("_simulator", BindingFlags.Instance | BindingFlags.NonPublic);
            simulationAdjustTimeMethod ??= sessionSimulationField.FieldType.GetMethod("AdjustClock", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(double) }, null);

            var simulation = sessionSimulationField.GetValue(session);
            simulationAdjustTimeMethod.Invoke(simulation, new object[] { -session.AccumulatedTime });
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            Time.timeScale = 1;
            Time.captureDeltaTime = 0;
        }

        private unsafe void OnGameResynced(CallbackGameResynced e) {
            if (ActiveReplayManager.Instance.IsReplay) {
                gameEnded = false;
                replayCanvasGroup.interactable = true;
                if (ActiveReplayManager.Instance.IsReplayFastForwarding) {
                    if (e.Game.Frames.Predicted.Global->GameState != GameState.Playing) {
                        FindFirstObjectByType<LoopingMusicPlayer>().Stop();
                    }
                } else {
                    Time.timeScale = replaySpeed;
                }
            }
        }

        private void OnGameEnded(EventGameEnded e) {
            if (ActiveReplayManager.Instance.IsReplay) {
                gameEnded = true;
                replayCanvasGroup.interactable = false;
                FinishFastForward();
                replayPaused = false;
                Time.timeScale = 1;
                CloseTab();
            }
        }

        private void OnPause(InputAction.CallbackContext context) {
            if (ActiveReplayManager.Instance.IsReplayFastForwarding) {
                FinishFastForward();
            }
        }

        private void PurgeMarkers() {
            for (int i = 0; i < markers.Count; i++) {
                Destroy(markers[i]);
            }
            markers.Clear();
            markerImages.Clear();
        }
    }
}
