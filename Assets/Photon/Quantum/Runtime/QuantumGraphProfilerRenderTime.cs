namespace Quantum.Profiling {
  /// <summary>
  /// A Quantum graph profiler that shows the render time
  /// </summary>
  public sealed class QuantumGraphProfilerRenderTime : QuantumGraphProfilerValueSeries {
    /// <inheritdoc/>
    protected override void OnUpdate() {
      AddValue(QuantumGraphProfilers.RenderTimer.GetLastSeconds());
    }

    /// <inheritdoc/>
    protected override void OnTargetFPSChanged(int fps) {
      float frameMs = 1.0f / fps;
      Graph.SetThresholds(frameMs * 0.75f, frameMs, frameMs * 1.5f);
    }
  }
}
