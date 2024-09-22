namespace Quantum.Profiling {
  using System.Collections.Generic;
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// A Quantum graph profiler that records custom markers.
  /// </summary>
  public sealed class QuantumGraphProfilerMarkers : QuantumGraphProfilerMarkerSeries {
    /// <summary>
    /// All marker profiler instances.
    /// </summary>
    public static readonly List<QuantumGraphProfilerMarkers> Instances = new List<QuantumGraphProfilerMarkers>();

    [SerializeField]
    private bool _subscribeQuantumCallbacks;

    private int _quantumMarkers = 0;
    private bool[] _markers = new bool[8];

    /// <summary>
    /// Get a marker profiler by name.
    /// </summary>
    /// <param name="name">Name</param>
    /// <returns>Marker graph</returns>
    public static QuantumGraphProfilerMarkers Get(string name) {
      for (int i = 0; i < Instances.Count; ++i) {
        QuantumGraphProfilerMarkers profiler = Instances[i];
        if (profiler != null && profiler.name == name) {
          return profiler;
        }
      }

      return null;
    }

    /// <summary>
    /// Set the marker index to true on all marker profilers.
    /// </summary>
    /// <param name="index"></param>
    public static void Set(int index) {
      for (int i = 0; i < Instances.Count; ++i) {
        QuantumGraphProfilerMarkers profiler = Instances[i];
        if (profiler != null) {
          profiler.SetMarker(index);
          return;
        }
      }
    }

    /// <summary>
    /// Set the marker index to true.
    /// </summary>
    /// <param name="index">Index</param>
    public void SetMarker(int index) {
      int minIndex = _quantumMarkers;
      int maxIndex = _markers.Length - 1;

      if (index < minIndex || index > maxIndex) {
        if (index >= 0 && index < minIndex) {
          Debug.LogErrorFormat("Index {0} is reserved for Quantum callbacks, allowed is <{1}, {2}>", index, minIndex, maxIndex);
          return;
        }

        Debug.LogErrorFormat("Index {0} out of supported range, allowed is <{1}, {2}>", index, minIndex, maxIndex);
        return;
      }

      _markers[index] = true;
    }

    /// <inheritdoc/>
    protected override void OnInitialize() {
      Instances.Add(this);
    }

    /// <inheritdoc/>
    protected override void OnDeinitialize() {
      Instances.Remove(this);
    }

    /// <inheritdoc/>
    protected override void OnActivated() {
      base.OnActivated();

      QuantumCallback.UnsubscribeListener(this);
      _quantumMarkers = 0;

      if (_subscribeQuantumCallbacks == true) {
        ++_quantumMarkers;
        QuantumCallback.Subscribe(this, (CallbackInputConfirmed inputConfirmed) => {
          if (inputConfirmed.Game.PlayerIsLocal(inputConfirmed.Input.Player) == true) {
            _markers[0] = (inputConfirmed.Input.Flags & DeterministicInputFlags.ReplacedByServer) == DeterministicInputFlags.ReplacedByServer;
          }
        });

        ++_quantumMarkers;
        QuantumCallback.Subscribe(this, (CallbackChecksumComputed checksumComputed) => {
          _markers[1] = true;
        });
      }
    }

    /// <inheritdoc/>
    protected override void OnDeactivated() {
      QuantumCallback.UnsubscribeListener(this);
      _quantumMarkers = 0;

      base.OnDeactivated();
    }

    /// <inheritdoc/>
    protected override void OnUpdate() {
      SetMarkers(_markers);

      for (int i = 0; i < _markers.Length; ++i) {
        _markers[i] = false;
      }
    }
  }
}