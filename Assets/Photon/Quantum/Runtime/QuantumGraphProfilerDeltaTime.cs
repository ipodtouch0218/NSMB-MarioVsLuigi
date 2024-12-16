namespace Quantum.Profiling {
  using UnityEngine;

  /// <summary>
  /// A graph profiler that records the delta time.
  /// </summary>
  public sealed class QuantumGraphProfilerDeltaTime : QuantumGraphProfilerValueSeries {
    /// <inheritdoc/>
    protected override void OnUpdate() {
      AddValue(Time.unscaledDeltaTime);
    }

    /// <inheritdoc/>
    protected override void OnTargetFPSChanged(int fps) {
      float frameMs = 1.0f / fps;
      Graph.SetThresholds(frameMs * 1.25f, frameMs * 1.5f, frameMs * 2.0f);
    }
  }
}