namespace Quantum {
  using System;
  using System.Diagnostics;
  using Photon.Client;
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
    /// Active state.
    /// </summary>
    public GameObject ToggleOn;
    /// <summary>
    /// Inactive state.
    /// </summary>
    public GameObject ToggleOff;
    /// <summary>
    /// Start the game with an open stats window.
    /// </summary>
    public Boolean StartEnabled = true;
    /// <summary>
    /// Use only the last second to measure <see cref="NetworkOut"/> and <see cref="NetworkOut"/> instead of the total time.
    /// </summary>
    public Boolean UseCurrentBandwidth = true;
    /// <summary>
    /// Shows frame and ping information on the toggle button.
    /// </summary>
    public Boolean ShowCompactStats = true;
    /// <summary>
    /// The text field set when <see cref="ShowCompactStats"/> is <see langword="true"/>."/>
    /// </summary>
    public Text CompactStatsText;

    double _lastTime = 0;
    int _lastFrame = 0;
    Stopwatch _networkTimer;
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
        gameObject.AddComponent<QuantumUnityInputSystemWithLegacyFallback>();
      }

      SetState(StartEnabled);
    }

    void LateUpdate() {
      if (QuantumRunner.Default && ToggleOff.activeSelf) {
        if (QuantumRunner.Default.IsRunning) {
          if (ShowCompactStats) {
            var currentFrame = QuantumRunner.Default.Game.Session.FrameVerified.Number;
            if (_lastFrame != currentFrame) {
              _lastFrame = currentFrame;
              var ping = QuantumRunner.Default.Game.Session.Stats.Ping;
              CompactStatsText.text =
                QuantumRunner.Default.Game.Session.IsOnline
                ? $"Frame {FormatFrame(currentFrame, QuantumRunner.Default.Game.Session.IsStalling),5}  Ping {FormatPing(ping),3}"
                : $"Frame {FormatFrame(currentFrame, QuantumRunner.Default.Game.Session.IsStalling),5}   (offline)";
            }
          }
        }
      }

      if (QuantumRunner.Default && ToggleOn.activeSelf) {
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
                NetworkIn.text = FormatBandwidth(snapShotDelta.BytesIn / (double)snapShotDelta.DeltaTime * 1000d);
                NetworkOut.text = FormatBandwidth(snapShotDelta.BytesOut / (double)snapShotDelta.DeltaTime * 1000d);
              }

              _snapshotDelta = QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.ToSnapshot();
              _lastTime = _networkTimer.Elapsed.TotalSeconds;
            }
          } else {
            QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.ToSnapshot();
            NetworkIn.text = FormatBandwidth(QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.BytesIn / _networkTimer.Elapsed.TotalSeconds);
            NetworkOut.text = FormatBandwidth(QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.BytesOut / _networkTimer.Elapsed.TotalSeconds);
          }
        }
      } else {
        _networkTimer = null;
      }
    }

    void SetState(bool state) {
      ToggleOn.SetActive(state);
      ToggleOff.SetActive(!state);
    }

    /// <summary>
    /// Toggle the stats window.
    /// </summary>
    public void Toggle() {
      ToggleOn.SetActive(!ToggleOn.activeSelf);
      ToggleOff.SetActive(!ToggleOff.activeSelf);
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

    static string FormatFrame(int frame, bool isStalling) {
      if (isStalling) {
        return $"<b><color=#FF9794>{frame}</color></b>";
      }
      return $"<b>{frame}</b>";
    }

    static string FormatPing(int ping) {
      if (ping <= 50) {
        return $"<b><color=#B6FFD3>{ping}</color></b>";
      } else if (ping <= 100) {
        return $"<b><color=#F9FFB6>{ping}</color></b>";
      } else if (ping <= 150) {
        return $"<b><color=#FFDAB6>{ping}</color></b>";
      }

      return $"<b><color=#FF9794>{ping}</color></b>";
    }

    static string[] BytesPerSecondUnits = { "B/s", "KB/s", "MB/s", "GB/s" };

    static string FormatBandwidth(double byteCount) {
      if (byteCount <= 0) {
        return "0 B/s";
      }

      var bytes = Math.Abs((long)byteCount);
      var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
      var num = Math.Round(bytes / Math.Pow(1024, place), 1);

      return $"{(Math.Sign(byteCount) * num):0.0} {BytesPerSecondUnits[Math.Min(place, BytesPerSecondUnits.Length - 1)]}";
    }
  }
}