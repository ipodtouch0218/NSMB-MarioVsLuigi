using NSMB.Replay;
using NSMB.Sound;
using NSMB.UI.Pause;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using System.Collections;
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
        [SerializeField] private InputActionReference mousePositionAction;
        [SerializeField] private GameObject defaultSelection;

        [SerializeField] private GameObject tabBlocker;

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
        private Frame resetFrame;

        public void OnValidate() {
            this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
        }

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
        }

        public override void OnActivate(Frame f) {
            replayUI.SetActive(ActiveReplayManager.Instance.IsReplay);
            if (!ActiveReplayManager.Instance.IsReplay) {
                enabled = false;
                return;
            }

            resetFrame = Game.CreateFrame();
            replayCanvasGroup.interactable = true;
            replayLength = Utils.SecondsToMinuteSeconds(ActiveReplayManager.Instance.ReplayLength / f.UpdateRate);
            trackArrowText.gameObject.SetActive(false);
            Settings.Controls.UI.Pause.performed += OnPause;
        }

        public void OnDestroy() {
            Time.timeScale = 1;
            if (ActiveReplayManager.Instance) {
                ActiveReplayManager.Instance.IsReplayFastForwarding = false;
            }
            Settings.Controls.UI.Pause.performed -= OnPause;
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
                replayTimecode.text = builder.ToString();

                previousTimestampSeconds = currentSeconds;
            }

            float width = maxTrackX - minTrackX;
            float bufferPercentage = (float) ActiveReplayManager.Instance.ReplayFrameCache.Count * f.UpdateRate * 5 / ActiveReplayManager.Instance.ReplayLength;
            Vector4 newPadding = trackBufferMask.padding;
            newPadding.z = Mathf.Max((1f - bufferPercentage) * width + 8, 16);
            trackBufferMask.padding = newPadding;

            if (draggingArrow && !replayCanvasGroup.interactable) {
                StopArrowDrag();
            }

            float percentage;
            if (draggingArrow) {
                Vector2 mousePositionPixels = mousePositionAction.action.ReadValue<Vector2>();
                Vector2 mousePositionTrack = trackArrow.transform.parent.InverseTransformPoint(mousePositionPixels);

                float newX = Mathf.Clamp(mousePositionTrack.x, minTrackX, maxTrackX);
                percentage = (newX - minTrackX) / (maxTrackX - minTrackX);

                trackArrowText.text = Utils.SecondsToMinuteSeconds(Mathf.FloorToInt(percentage * ActiveReplayManager.Instance.ReplayLength / f.UpdateRate));
            } else {
                percentage = (currentFrameNumber - ActiveReplayManager.Instance.ReplayStart) / ActiveReplayManager.Instance.ReplayLength;
            }

            trackArrow.localPosition = new Vector3(percentage * (maxTrackX - minTrackX) + minTrackX, 0, 0);
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

        public void RewindReplay() {
            if (!ActiveReplayManager.Instance.IsReplay) {
                return;
            }

            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            int currentIndex = (f.Number - ActiveReplayManager.Instance.ReplayStart) / (5 * f.UpdateRate);
            int newIndex = Mathf.Max(currentIndex - 1, 0);
            int newFrame = (newIndex * (5 * f.UpdateRate)) + ActiveReplayManager.Instance.ReplayStart;

            var session = QuantumRunner.Default.Session;

            ActiveReplayManager.Instance.IsReplayFastForwarding = true;
            resetFrame.Deserialize(ActiveReplayManager.Instance.ReplayFrameCache[newIndex]);
            session.ResetReplay(resetFrame);
            /*
            // It's a private method. Because of course it is.
            var resetMethod = session.GetType().GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance, null, new System.Type[] { typeof(byte[]), typeof(int), typeof(bool) }, null);
            resetMethod.Invoke(session, new object[] { NetworkHandler.ReplayFrameCache[newIndex], newFrame, true });
            */

            // Fix accumulated time applying
            if (session.AccumulatedTime > 0) {
                var simulator = session.GetType().GetField("_simulator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                var adjustTimeMethod = simulator.GetType().GetMethod("AdjustClock", BindingFlags.Instance | BindingFlags.Public, null, new System.Type[] { typeof(double) }, null);
                adjustTimeMethod.Invoke(simulator, new object[] { -session.AccumulatedTime });
            }
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
                /*
                // It's a private method. Because of course it is.
                var resetMethod = session.GetType().GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance, null, new System.Type[] { typeof(byte[]), typeof(int), typeof(bool) }, null);
                resetMethod.Invoke(session, new object[] { NetworkHandler.ReplayFrameCache[newIndex], newFrame, false });
                */

                // Fix accumulated time applying
                if (session.AccumulatedTime > 0) {
                    var simulator = session.GetType().GetField("_simulator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                    var adjustTimeMethod = simulator.GetType().GetMethod("AdjustClock", BindingFlags.Instance | BindingFlags.Public, null, new System.Type[] { typeof(double) }, null);
                    adjustTimeMethod.Invoke(simulator, new object[] { -session.AccumulatedTime });
                }
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
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            Time.timeScale = 1;
            Time.captureDeltaTime = f.DeltaTime.AsFloat;
            yield return null;
            Time.timeScale = 0;
            Time.captureDeltaTime = 0;
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

        public void StopArrowDrag() {
            draggingArrow = false;
            trackArrowText.gameObject.SetActive(false);

            if (!replayCanvasGroup.interactable) {
                return;
            }

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
            if (cachedFrame > f.Number || newFrame < f.Number) {
                ActiveReplayManager.Instance.IsReplayFastForwarding = true;
                resetFrame.Deserialize(ActiveReplayManager.Instance.ReplayFrameCache[newFrameCacheIndex]);
                session.ResetReplay(resetFrame);
                /*
                // It's a private method. Because of course it is.
                var resetMethod = session.GetType().GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance, null, new System.Type[] { typeof(byte[]), typeof(int), typeof(bool) }, null);
                resetMethod.Invoke(session, new object[] { NetworkHandler.ReplayFrameCache[newFrameCacheIndex], cachedFrame, false });
                */

                // Fix accumulated time applying
                if (session.AccumulatedTime > 0) {
                    var simulator = session.GetType().GetField("_simulator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                    var adjustTimeMethod = simulator.GetType().GetMethod("AdjustClock", BindingFlags.Instance | BindingFlags.Public, null, new System.Type[] { typeof(double) }, null);
                    adjustTimeMethod.Invoke(simulator, new object[] { -session.AccumulatedTime });
                }
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
            }
        }

        public void ResetReplay() {
            QuantumRunner runner = QuantumRunner.Default;
            var session = runner.Session;

            ActiveReplayManager.Instance.IsReplayFastForwarding = true;
            resetFrame.Deserialize(ActiveReplayManager.Instance.ReplayFrameCache[0]);
            session.ResetReplay(resetFrame);

            /*
            // It's a private method. Because of course it is.
            var resetMethod = session.GetType().GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance, null, new System.Type[] { typeof(byte[]), typeof(int), typeof(bool) }, null);
            resetMethod.Invoke(session, new object[] { NetworkHandler.ReplayFrameCache[0], NetworkHandler.ReplayStart, false });
            */

            // Fix accumulated time applying
            if (session.AccumulatedTime > 0) {
                var simulator = session.GetType().GetField("_simulator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                var adjustTimeMethod = simulator.GetType().GetMethod("AdjustClock", BindingFlags.Instance | BindingFlags.Public, null, new System.Type[] { typeof(double) }, null);
                adjustTimeMethod.Invoke(simulator, new object[] { -session.AccumulatedTime });
            }

            ActiveReplayManager.Instance.IsReplayFastForwarding = false;
            replayPaused = false;
            Time.timeScale = replaySpeed;
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
    }
}
