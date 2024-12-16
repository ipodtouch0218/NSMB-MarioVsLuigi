namespace Quantum.Profiling {
  /// <summary>
  /// A Quantum graph profiler that records the frame time.
  /// </summary>
  public sealed class QuantumGraphProfilerFrameTime : QuantumGraphProfilerValueSeries {
    /// <inheritdoc/>
    protected override void OnUpdate() {
      AddValue(QuantumGraphProfilers.FrameTimer.GetLastSeconds());
    }

    /// <inheritdoc/>
    protected override void OnTargetFPSChanged(int fps) {
      float frameMs = 1.0f / fps;
      Graph.SetThresholds(frameMs * 0.75f, frameMs, frameMs * 1.5f);
    }
  }
}