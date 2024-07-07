namespace Quantum.Profiling {
  using global::Unity.Profiling;

  /// <summary>
  /// A Quantum graph profiler that records the garbage collection time.
  /// </summary>
  public sealed class QuantumGraphProfilerGC : QuantumGraphProfilerValueSeries {
    private ProfilerRecorder _gcCollectRecorder;

    /// <inheritdoc/>
    protected override void OnActivated() {
      base.OnActivated();

      _gcCollectRecorder = ProfilerRecorder.StartNew(new ProfilerCategory("GC"), "GC.Collect");
    }

    /// <inheritdoc/>
    protected override void OnDeactivated() {
      _gcCollectRecorder.Dispose();

      base.OnDeactivated();
    }

    /// <inheritdoc/>
    protected override void OnUpdate() {
      AddValue(_gcCollectRecorder.Valid == true ? 0.000001f * _gcCollectRecorder.LastValue : 0.0f);
    }

    /// <inheritdoc/>
    protected override void OnTargetFPSChanged(int fps) {
      float frameMs = 1.0f / fps;
      Graph.SetThresholds(frameMs * 0.25f, frameMs * 0.375f, frameMs * 0.5f);
    }
  }
}