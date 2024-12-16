namespace Quantum.Profiling {
  /// <summary>
  /// A Quantum graph profiler that shows how far the simulation is in predicting.
  /// </summary>
  public sealed class QuantumGraphProfilerPredictedFrames : QuantumGraphProfilerValueSeries {
    /// <inheritdoc/>
    protected override void OnUpdate() {
      int predictedFrames = 0;

      QuantumRunner quantumRunner = QuantumRunner.Default;
      if (quantumRunner != null && quantumRunner.Game != null && quantumRunner.Game.Session != null) {
        predictedFrames = quantumRunner.Game.Session.PredictedFrames;
      }

      AddValue(predictedFrames);
    }
  }
}
