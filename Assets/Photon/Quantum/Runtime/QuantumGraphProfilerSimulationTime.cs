namespace Quantum.Profiling {
  /// <summary>
  /// A Quantum graph profiler that shows the simulation time.
  /// </summary>
  public sealed class QuantumGraphProfilerSimulationTime : QuantumGraphProfilerValueSeries {

    /// <summary>
    /// Adding three value dimensions: Update time, Verification time, Prediction time.
    /// </summary>
    protected override int ValueDimensions => 3;

    /// <inheritdoc/>
    protected override void OnUpdate() {
      var updateTime = 0f;
      var predictionTime = 0f;
      var verificationTime = 0f;

      var session = QuantumRunner.Default?.Game?.Session;

      if (session != null) {
        updateTime = (float)session.Stats.UpdateTime;
        predictionTime = (float)session.Stats.PredictionTime;
        verificationTime = (float)session.Stats.VerificationTime;
      }

      AddValues(
        value1: updateTime - verificationTime - predictionTime,
        value2: verificationTime,
        value3: predictionTime);
    }

    /// <inheritdoc/>
    protected override void OnTargetFPSChanged(int fps) {
      float frameMs = 1.0f / fps;
      Graph.SetThresholds(frameMs * 0.25f, frameMs * 0.375f, frameMs * 0.5f);
    }
  }
}