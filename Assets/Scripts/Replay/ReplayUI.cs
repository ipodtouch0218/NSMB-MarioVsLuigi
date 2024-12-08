using NSMB.Extensions;
using NSMB.UI.Game;
using NSMB.Utils;
using Quantum;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ReplayUI : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private PlayerElements playerElements;

    [SerializeField] private GameObject replayUI;
    [SerializeField] private Transform trackArrow;
    [SerializeField] private RectMask2D trackBufferMask;
    [SerializeField] private TMP_Text trackArrowText;
    [SerializeField] private float minTrackX = -180, maxTrackX = 180;
    [SerializeField] private TMP_Text replayTimecode;
    [SerializeField] private TMP_Text replayPauseButton;

    [SerializeField] private InputActionReference mousePositionAction;

    //---Private Variables
    private float replaySpeed = 1;
    private bool replayPaused;
    private bool draggingArrow;
    private StringBuilder builder = new();

    public void OnValidate() {
        this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
    }

    public void Start() {
        replayUI.SetActive(NetworkHandler.IsReplay);
        if (!NetworkHandler.IsReplay) {
            enabled = false;
            return;
        }

        trackArrowText.gameObject.SetActive(false);
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
    }

    public void OnDestroy() {
        Time.timeScale = 1;
    }

    public void Update() {
        NetworkHandler.IsReplayFastForwarding = false;
    }

    private void OnUpdateView(CallbackUpdateView e) {
        Frame f = e.Game.Frames.Predicted;
        Frame fp = e.Game.Frames.PredictedPrevious;

        float currentFrameNumber = fp.Number + e.Game.InterpolationFactor;
        builder.Clear();
        builder.Append(Utils.SecondsToMinuteSeconds(Mathf.FloorToInt((currentFrameNumber - NetworkHandler.ReplayStart) / f.UpdateRate)));
        builder.Append('/');
        builder.Append(Utils.SecondsToMinuteSeconds(NetworkHandler.ReplayLength / f.UpdateRate));
        replayTimecode.text = builder.ToString();

        float width = maxTrackX - minTrackX;
        float bufferPercentage = (float) NetworkHandler.ReplayFrameCache.Count * f.UpdateRate * 5 / NetworkHandler.ReplayLength;
        Vector4 newPadding = trackBufferMask.padding;
        newPadding.z = Mathf.Max((1f - bufferPercentage) * width + 8, 16);
        trackBufferMask.padding = newPadding;

        float percentage;
        if (draggingArrow) {
            Vector2 mousePositionPixels = mousePositionAction.action.ReadValue<Vector2>();
            Vector2 mousePositionTrack = trackArrow.transform.parent.InverseTransformPoint(mousePositionPixels);
            
            float newX = Mathf.Clamp(mousePositionTrack.x, minTrackX, maxTrackX);
            percentage = (newX - minTrackX) / (maxTrackX - minTrackX);

            trackArrowText.text = Utils.SecondsToMinuteSeconds(Mathf.FloorToInt(percentage * NetworkHandler.ReplayLength / f.UpdateRate));
        } else {
            percentage = (currentFrameNumber - NetworkHandler.ReplayStart) / NetworkHandler.ReplayLength;
        }

        trackArrow.localPosition = new Vector3(percentage * (maxTrackX - minTrackX) + minTrackX, 0, 0);
    }

    public bool ToggleReplayControls() {
        replayUI.SetActive(!replayUI.activeSelf);
        return replayUI.activeSelf;
    }

    public void RewindReplay() {
        if (!NetworkHandler.IsReplay) {
            return;
        }

        Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
        int currentIndex = (f.Number - NetworkHandler.ReplayStart) / (5 * f.UpdateRate);
        int newIndex = Mathf.Max(currentIndex - 1, 0);
        int newFrame = (newIndex * (5 * f.UpdateRate)) + NetworkHandler.ReplayStart;

        var session = QuantumRunner.Default.Session;

        // It's a private method. Because of course it is.
        NetworkHandler.IsReplayFastForwarding = true;
        var resetMethod = session.GetType().GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance, null, new System.Type[] { typeof(byte[]), typeof(int), typeof(bool) }, null);
        resetMethod.Invoke(session, new object[] { NetworkHandler.ReplayFrameCache[newIndex], newFrame, true });
        NetworkHandler.IsReplayFastForwarding = false;

        // Fix accumulated time applying
        if (session.AccumulatedTime > 0) {
            var simulator = session.GetType().GetField("_simulator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            var adjustTimeMethod = simulator.GetType().GetMethod("AdjustClock", BindingFlags.Instance | BindingFlags.Public, null, new System.Type[] { typeof(double) }, null);
            adjustTimeMethod.Invoke(simulator, new object[] { -session.AccumulatedTime });
        }
    }

    public void FastForwardReplay() {
        if (!NetworkHandler.IsReplay) {
            return;
        }

        Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
        int currentIndex = (f.Number - NetworkHandler.ReplayStart) / (5 * f.UpdateRate);
        int newIndex = currentIndex + 1;
        int newFrame = Mathf.Min((newIndex * (5 * f.UpdateRate)) + NetworkHandler.ReplayStart, NetworkHandler.ReplayEnd);

        var session = QuantumRunner.Default.Session;
        if (newIndex < NetworkHandler.ReplayFrameCache.Count) {
            // We already have this frame
            // It's a private method. Because of course it is.
            NetworkHandler.IsReplayFastForwarding = true;
            var resetMethod = session.GetType().GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance, null, new System.Type[] { typeof(byte[]), typeof(int), typeof(bool) }, null);
            resetMethod.Invoke(session, new object[] { NetworkHandler.ReplayFrameCache[newIndex], newFrame, true });
            NetworkHandler.IsReplayFastForwarding = false;
        } else {
            // We have to simulate up to this frame
            NetworkHandler.IsReplayFastForwarding = true;
            session.Update((newFrame - f.Number) * f.DeltaTime.AsDouble);
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

    public void ReplayChangeSpeed(Slider slider) {
        float[] speeds = { 0.25f, 0.5f, 1f, 2f, 4f };
        replaySpeed = speeds[Mathf.RoundToInt(slider.value)];

        if (!replayPaused) {
            Time.timeScale = replaySpeed;
        }
    }

    public void StartArrowDrag() {
        draggingArrow = true;
        trackArrowText.gameObject.SetActive(true);
    }

    public void StopArrowDrag() {
        QuantumRunner runner = QuantumRunner.Default;
        Frame f = runner.Game.Frames.Predicted;

        draggingArrow = false;
        trackArrowText.gameObject.SetActive(false);

        float newX = Mathf.Clamp(trackArrow.localPosition.x, minTrackX, maxTrackX);
        float percentage = (newX - minTrackX) / (maxTrackX - minTrackX);
        int newFrame = Mathf.RoundToInt(percentage * NetworkHandler.ReplayLength) + NetworkHandler.ReplayStart;
        int frameOffset = newFrame - NetworkHandler.ReplayStart;

        // Find the closest cached frame
        int newFrameCacheIndex = frameOffset / (5 * f.UpdateRate);
        newFrameCacheIndex = Mathf.Clamp(newFrameCacheIndex, 0, NetworkHandler.ReplayFrameCache.Count - 1);
        int cachedFrame = (newFrameCacheIndex * (5 * f.UpdateRate)) + NetworkHandler.ReplayStart;

        // It's a private method. Because of course it is.
        var session = QuantumRunner.Default.Session;
        NetworkHandler.IsReplayFastForwarding = true;
        var resetMethod = session.GetType().GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance, null, new System.Type[] { typeof(byte[]), typeof(int), typeof(bool) }, null);
        resetMethod.Invoke(session, new object[] { NetworkHandler.ReplayFrameCache[newFrameCacheIndex], cachedFrame, true });
        NetworkHandler.IsReplayFastForwarding = false;

        // Fix accumulated time applying
        if (session.AccumulatedTime > 0) {
            var simulator = session.GetType().GetField("_simulator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            var adjustTimeMethod = simulator.GetType().GetMethod("AdjustClock", BindingFlags.Instance | BindingFlags.Public, null, new System.Type[] { typeof(double) }, null);
            adjustTimeMethod.Invoke(simulator, new object[] { -session.AccumulatedTime });
        }

        // Simulate up to the target frame
        if (newFrame != cachedFrame) {
            NetworkHandler.IsReplayFastForwarding = true;
            session.Update((newFrame - cachedFrame) * f.DeltaTime.AsDouble);
        }
    }
}
