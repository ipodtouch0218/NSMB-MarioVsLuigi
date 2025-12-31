namespace Quantum.Profiling {
  /// <summary>
  /// A Quantum graph profiler that shows how many verified frames have been simulated during the last update.
  /// </summary>
  public sealed class QuantumGraphProfilerSimulatedFrames : QuantumGraphProfilerValueSeries {
    private int _lastVerifiedFrameNumber;

    /// <summary>
    /// Using two value dimensions: one for verified frames, one for predicted frames.
    /// </summary>
    protected override int ValueDimensions => 2;

    /// <inheritdoc/>
    protected override void OnUpdate() {
      int verifiedFramesSimulated = 0;
      int predictedFramesSimulated = 0;

      QuantumRunner quantumRunner = QuantumRunner.Default;
      if (quantumRunner?.Game?.Session != null) {
        predictedFramesSimulated = quantumRunner.Game.Session.PredictedFrames;

        Frame verifiedFrame = quantumRunner.Game.Frames.Verified;
        if (verifiedFrame != null) {
          if (_lastVerifiedFrameNumber == 0) {
            _lastVerifiedFrameNumber = quantumRunner.Game.Session.RollbackWindow - 1;
          }

          verifiedFramesSimulated = verifiedFrame.Number - _lastVerifiedFrameNumber;

          _lastVerifiedFrameNumber = verifiedFrame.Number;
        }
      }

      AddValues(
        value1: verifiedFramesSimulated, 
        value2: predictedFramesSimulated);
    }
  }
}