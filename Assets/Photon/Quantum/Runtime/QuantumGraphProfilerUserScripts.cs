namespace Quantum.Profiling {
  /// <summary>
  /// A Quantum graph profiler that shows the time spent in Unity scripts.
  /// </summary>
  public sealed class QuantumGraphProfilerUserScripts : QuantumGraphProfilerValueSeries {
    /// <inheritdoc/>
    protected override void OnUpdate() {
      AddValue(QuantumGraphProfilers.ScriptsTimer.GetLastSeconds());
    }

    /// <inheritdoc/>
    protected override void OnTargetFPSChanged(int fps) {
      float frameMs = 1.0f / fps;
      Graph.SetThresholds(frameMs * 0.5f, frameMs * 0.75f, frameMs);
    }
  }
}
