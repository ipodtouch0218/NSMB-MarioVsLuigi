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
      if (quantumRunner?.Game?.Session != null) {
        Frame verifiedFrame = quantumRunner.Game.Frames.Verified;
        if (verifiedFrame != null) {
          if (_lastVerifiedFrameNumber == 0) {
            _lastVerifiedFrameNumber = quantumRunner.Game.Session.RollbackWindow - 1;
          }

          verifiedFramesSimulated = verifiedFrame.Number - _lastVerifiedFrameNumber;

          _lastVerifiedFrameNumber = verifiedFrame.Number;
        }
      }

      AddValue(verifiedFramesSimulated);
    }
  }
}