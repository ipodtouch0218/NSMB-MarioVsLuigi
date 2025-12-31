namespace Quantum.Profiling {
  /// <summary>
  /// A Quantum graph profiler that shows how many verified frames have been simulated during the last update.
  /// </summary>
  public sealed class QuantumGraphProfilerVerifiedFrames : QuantumGraphProfilerValueSeries {
    private int _lastVerifiedFrameNumber;

    /// <inheritdoc/>
    protected override void OnUpdate() {
      int verifiedFramesSimulated = 0;

      QuantumRunner quantumRunner = QuantumRunner.Default;
      if (quantumRunner != null && quantumRunner.Game != null) {
        Frame verifiedFrame = quantumRunner.Game.Frames.Verified;
        if (verifiedFrame != null) {
          verifiedFramesSimulated = verifiedFrame.Number - _lastVerifiedFrameNumber;
          _lastVerifiedFrameNumber = verifiedFrame.Number;
        }
      }

      AddValue(verifiedFramesSimulated);
    }
  }
}