namespace Quantum.Profiling {
  /// <summary>
  /// A Quantum graph profiler that records the frame time.
  /// </summary>
  public sealed class QuantumGraphProfilerUpdateTime : QuantumGraphProfilerValueSeries {
    protected override int ValueDimensions => 4;

    /// <inheritdoc/>
    protected override void OnUpdate() {

      var simulateTime = 0f;

      var session = QuantumRunner.Default?.Game?.Session;
      if (session != null) {
        simulateTime = (float)session.Stats.UpdateTime;
      }

      var frameTime = QuantumGraphProfilers.FrameTimer.GetLastSeconds();
      var renderTime = QuantumGraphProfilers.RenderTimer.GetLastSeconds();
      var scriptsTime = QuantumGraphProfilers.ScriptsTimer.GetLastSeconds() - simulateTime;


      AddValues(
        value1: frameTime - simulateTime - renderTime - scriptsTime,
        value2: simulateTime,
        value3: renderTime,
        value4: scriptsTime);
    }

    /// <inheritdoc/>
    protected override void OnTargetFPSChanged(int fps) {
      float frameMs = 1.0f / fps;
      Graph.SetThresholds(frameMs * 0.75f, frameMs, frameMs * 1.5f);
    }
  }
}