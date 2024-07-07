namespace Quantum {
  using Photon.Client;
  using System;
  using System.Diagnostics;
  using UnityEngine;
  using UnityEngine.EventSystems;
  using UnityEngine.UI;
  using static QuantumUnityExtensions;

  /// <summary>
  /// Measures and display basic Quantum statistics on a UI element.
  /// </summary>
  public class QuantumStats : QuantumMonoBehaviour {
    /// <summary>
    /// Current verified frame.
    /// </summary>
    public Text FrameVerified;
    /// <summary>
    /// Current predicted frame.
    /// </summary>
    public Text FramePredicted;
    /// <summary>
    /// Number of predicted frames.
    /// </summary>
    public Text Predicted;
    /// <summary>
    /// Number of resimulated frames.
    /// </summary>
    public Text Resimulated;
    /// <summary>
    /// The last simulation time.
    /// </summary>
    public Text SimulateTime;
    /// <summary>
    /// The state of the simulation.
    /// </summary>
    public Text SimulationState;
    /// <summary>
    /// The network ping measured by the simulation.
    /// </summary>
    public Text NetworkPing;
    /// <summary>
    /// The bytes received per second.
    /// </summary>
    public Text NetworkIn;
    /// <summary>
    /// The bytes send per second.
    /// </summary>
    public Text NetworkOut;
    /// <summary>
    /// The current input offset.
    /// </summary>
    public Text InputOffset;
    /// <summary>
    /// Toggle button text.
    /// </summary>
    public Text ToggleButtonText;
    /// <summary>
    /// The UI objects to toggle on/off.
    /// </summary>
    public GameObject[] Toggles;
    /// <summary>
    /// Start the game with an open stats window.
    /// </summary>
    public Boolean StartEnabled = true;
    /// <summary>
    /// Use only the last second to measure <see cref="NetworkOut"/> and <see cref="NetworkOut"/> instead of the total time.
    /// </summary>
    public Boolean UseCurrentBandwidth = true;

    Stopwatch _networkTimer;
    double _lastTime = 0;
    TrafficStatsSnapshot _snapshotDelta;

    /// <summary>
    /// Resets the bandwidth stats measurement.
    /// </summary>
    public void ResetNetworkStats() {
      _networkTimer = null;
      _snapshotDelta = null;
      _lastTime = 0;
    }

    void Start() {
      // create event system if none exists in the scene
      var eventSystem = FindFirstObjectByType<EventSystem>();
      if (eventSystem == null) {
        gameObject.AddComponent<EventSystem>();
        gameObject.AddComponent<StandaloneInputModule>();
      }

      SetState(StartEnabled);
    }

    void Update() {
      if (QuantumRunner.Default && Toggles[0].activeSelf) {
        if (QuantumRunner.Default.IsRunning) {
          var gameInstance = QuantumRunner.Default.Game;

          if (gameInstance.Session.FramePredicted != null) {
            FrameVerified.text = gameInstance.Session.FrameVerified.Number.ToString();
            FramePredicted.text = gameInstance.Session.FramePredicted.Number.ToString();
          }

          Predicted.text = gameInstance.Session.PredictedFrames.ToString();
          NetworkPing.text = gameInstance.Session.Stats.Ping.ToString();
          SimulateTime.text = Math.Round(gameInstance.Session.Stats.UpdateTime * 1000, 2) + " ms";
          InputOffset.text = gameInstance.Session.Stats.Offset.ToString();
          Resimulated.text = gameInstance.Session.Stats.ResimulatedFrames.ToString();

          if (gameInstance.Session.IsStalling) {
            SimulationState.text = "Stalling";
            SimulationState.color = Color.red;
          } else {
            SimulationState.text = "Running";
            SimulationState.color = Color.green;
          }
        }

        if (QuantumRunner.Default.NetworkClient != null && QuantumRunner.Default.NetworkClient.IsConnected) {
          if (_networkTimer == null) {
            _networkTimer = Stopwatch.StartNew();
          }

          if (UseCurrentBandwidth) {
            var deltaTime = _networkTimer.Elapsed.TotalSeconds - _lastTime;
            if (deltaTime > 1) {
              if (_snapshotDelta != null) {
                var snapShotDelta = QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.ToDelta(_snapshotDelta);
                NetworkIn.text = (int)(snapShotDelta.BytesIn / (double)snapShotDelta.DeltaTime * 1000d) + " bytes/s";
                NetworkOut.text = (int)(snapShotDelta.BytesOut / (double)snapShotDelta.DeltaTime * 1000d) + " bytes/s";
              }

              _snapshotDelta = QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.ToSnapshot();
              _lastTime = _networkTimer.Elapsed.TotalSeconds;
            }
          } else {
            QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.ToSnapshot();
            NetworkIn.text = (int)(QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.BytesIn / _networkTimer.Elapsed.TotalSeconds) + " bytes/s";
            NetworkOut.text = (int)(QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.BytesOut / _networkTimer.Elapsed.TotalSeconds) + " bytes/s";
          }
        }
      } else {
        _networkTimer = null;
      }
    }

    void SetState(bool state) {
      for (int i = 0; i < Toggles.Length; ++i) {
        Toggles[i].SetActive(state);
      }

      ToggleButtonText.text = state ? "Hide Stats" : "Show Stats";
    }

    /// <summary>
    /// Toggle the stats window.
    /// </summary>
    public void Toggle() {
      SetState(!Toggles[0].activeSelf);
    }

    /// <summary>
    /// Find or load the stats windows and enable it.
    /// </summary>
    public static void Show() {
      GetObject().SetState(true);
    }

    /// <summary>
    /// Find or load the stats windows and disable it.
    /// </summary>
    public static void Hide() {
      GetObject().SetState(false);
    }

    /// <summary>
    /// Find or create the stats window.
    /// </summary>
    /// <returns>The stats window object</returns>
    public static QuantumStats GetObject() {
      QuantumStats stats;

      // find existing or create new
      if (!(stats = FindFirstObjectByType<QuantumStats>())) {
        stats = Instantiate(UnityEngine.Resources.Load<QuantumStats>(nameof(QuantumStats)));
      }

      return stats;
    }
  }
}