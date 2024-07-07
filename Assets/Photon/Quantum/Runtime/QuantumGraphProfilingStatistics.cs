namespace Quantum.Profiling {
  using global::Unity.Profiling;
  using UnityEngine;
  using UnityEngine.UI;

  /// <summary>
  /// A Unity script that renders additional statistics to the graph UI.
  /// </summary>
  public sealed class QuantumGraphProfilingStatistics : MonoBehaviour {
    [SerializeField]
    private bool _enableOnAwake;
    [SerializeField]
    private GameObject _renderObject;
    [SerializeField]
    private Image _toggleVisibility;
    [SerializeField]
    private Text _entityCount;
    [SerializeField]
    private Text _entityMemory;
    [SerializeField]
    private Text _totalUsedMemory;
    [SerializeField]
    private Text _gfxUsedMemory;
    [SerializeField]
    private Text _batches;
    [SerializeField]
    private Text _drawCalls;
    [SerializeField]
    private Text _triangles;
    [SerializeField]
    private Text _setPassCalls;

    private ProfilerRecorder _totalUsedMemoryRecorder;
    private ProfilerRecorder _gfxUsedMemoryRecorder;
    private ProfilerRecorder _batchesRecorder;
    private ProfilerRecorder _drawCallsRecorder;
    private ProfilerRecorder _trianglesRecorder;
    private ProfilerRecorder _setPassCallsRecorder;

    /// <summary>
    /// Toggle the visibility of the UI.
    /// </summary>
    public void ToggleVisibility() {
      _renderObject.SetActive(_renderObject.activeSelf == false);

      LateUpdate();
    }

    private void Awake() {
      _renderObject.SetActive(_enableOnAwake);

      LateUpdate();
    }

    /// <summary>
    /// Unity <see cref="OnEnable"/>, create and start profiler.
    /// </summary>
    public void OnEnable() {
      _totalUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
      _gfxUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
      _batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
      _drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
      _trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
      _setPassCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
    }


    /// <summary>
    /// Unity <see cref="OnDisable"/>, dispose and stop profilers.
    /// </summary>
    public void OnDisable() {
      _totalUsedMemoryRecorder.Dispose();
      _gfxUsedMemoryRecorder.Dispose();
      _batchesRecorder.Dispose();
      _drawCallsRecorder.Dispose();
      _trianglesRecorder.Dispose();
      _setPassCallsRecorder.Dispose();
    }

    private void LateUpdate() {
      if (_renderObject.activeSelf == false)
        return;

      Core.FrameBase.Stats memoryStats = default;

      QuantumRunner quantumRunner = QuantumRunner.Default;
      if (quantumRunner != null && quantumRunner.Game != null && quantumRunner.Game.Frames != null) {
        Frame predictedFrame = quantumRunner.Game.Frames.Predicted;
        if (predictedFrame != null) {
          memoryStats = predictedFrame.GetMemoryStats();
        }
      }

      _entityCount.text = memoryStats.EntityCount.ToString();
      _entityMemory.text = (memoryStats.EntityTotalMemory / 1024).ToString();
      _totalUsedMemory.text = _totalUsedMemoryRecorder.Valid == true ? string.Format("{0}", _totalUsedMemoryRecorder.LastValue / 1048576) : "---";
      _gfxUsedMemory.text = _gfxUsedMemoryRecorder.Valid == true ? string.Format("{0}", _gfxUsedMemoryRecorder.LastValue / 1048576) : "---";
      _batches.text = _batchesRecorder.Valid == true ? _batchesRecorder.LastValue.ToString() : "---";
      _drawCalls.text = _drawCallsRecorder.Valid == true ? _drawCallsRecorder.LastValue.ToString() : "---";
      _triangles.text = _trianglesRecorder.Valid == true ? string.Format("{0}k", _trianglesRecorder.LastValue / 1000) : "---";
      _setPassCalls.text = _setPassCallsRecorder.Valid == true ? _setPassCallsRecorder.LastValue.ToString() : "---";
    }
  }
}