namespace Quantum.Profiling {
  using Photon.Client;

  /// <summary>
  /// Gathers bandwidth statistics, uses an accumulator for feed averages into the graphs.
  /// </summary>
  public sealed class QuantumGraphProfilerBandwidth : QuantumGraphProfilerValueSeries {

    /// <summary>
    /// This profiler records two values: Incoming and Outgoing bandwidth in bytes per second.
    /// </summary>
    protected override int ValueDimensions => 2;

    TrafficStatsSnapshot _snapshotDelta;

    /// <inheritdoc/>
    protected override void OnActivated() {
      base.OnActivated();

      var peer = QuantumGraphProfilersUtility.GetNetworkPeer();

      if (peer != null) {
        _snapshotDelta = peer.Stats.ToSnapshot();
      }
    }

    /// <summary>
    /// Sample the values from the network peer.
    /// </summary>
    protected override void OnUpdate() {
      var peer = QuantumGraphProfilersUtility.GetNetworkPeer();

      var bytesIn = 0f;
      var bytesOut = 0f;

      if (peer != null) {
        if (_snapshotDelta != null) {
          var snapShotDelta = peer.Stats.ToDelta(_snapshotDelta);
          if (snapShotDelta.DeltaTime > 0) {
            bytesIn = snapShotDelta.BytesIn / snapShotDelta.DeltaTime * 1000f;
            bytesOut = snapShotDelta.BytesOut / snapShotDelta.DeltaTime * 1000f;
          }
        }
        _snapshotDelta = peer.Stats.ToSnapshot();
      }

      AddValues(bytesIn, bytesOut);
    }
  }
}