using NSMB.Extensions;
using NSMB.Utils;
using Quantum;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReplayUI : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private PlayerElements playerElements;

    [SerializeField] private GameObject replayUI;
    [SerializeField] private TMP_Text replayTimecode;
    [SerializeField] private TMP_Text replayPauseButton;

    //---Private Variables
    private float replaySpeed = 1;
    private bool replayPaused;

    public void OnValidate() {
        this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
    }

    public void Start() {
        replayUI.SetActive(NetworkHandler.IsReplay);
        if (!NetworkHandler.IsReplay) {
            enabled = false;
            return;
        }

        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
    }

    public void OnDestroy() {
        Time.timeScale = 1;
    }

    private void OnUpdateView(CallbackUpdateView e) {
        Frame f = e.Game.Frames.Predicted;
        Frame fp = e.Game.Frames.PredictedPrevious;

        replayTimecode.text =
            Utils.SecondsToMinuteSeconds(Mathf.FloorToInt((fp.Number + e.Game.InterpolationFactor - NetworkHandler.ReplayStart) / f.UpdateRate))
            + "/"
            + Utils.SecondsToMinuteSeconds(NetworkHandler.ReplayLength / f.UpdateRate);
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

        // It's a private method. Because of course it is.
        var resetMethod = QuantumRunner.Default.Session.GetType().GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance, null, new System.Type[] { typeof(byte[]), typeof(int), typeof(bool) }, null);
        resetMethod.Invoke(QuantumRunner.Default.Session, new object[] { NetworkHandler.ReplayFrameCache[newIndex], newFrame, true });

    }

    public void FastForwardReplay() {
        if (!NetworkHandler.IsReplay) {
            return;
        }

        Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
        int currentIndex = (f.Number - NetworkHandler.ReplayStart) / (5 * f.UpdateRate);
        int newIndex = currentIndex + 1;
        int newFrame = Mathf.Min((newIndex * (5 * f.UpdateRate)) + NetworkHandler.ReplayStart, NetworkHandler.ReplayEnd);
        
        if (newIndex < NetworkHandler.ReplayFrameCache.Count) {
            // We already have this frame
            // It's a private method. Because of course it is.
            var resetMethod = QuantumRunner.Default.Session.GetType().GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance, null, new System.Type[] { typeof(byte[]), typeof(int), typeof(bool) }, null);
            resetMethod.Invoke(QuantumRunner.Default.Session, new object[] { NetworkHandler.ReplayFrameCache[newIndex], newFrame, true });
        } else {
            // We have to simulate up to this frame
            QuantumRunner.Default.Session.Update((newFrame - f.Number) * f.DeltaTime.AsDouble);
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

}
