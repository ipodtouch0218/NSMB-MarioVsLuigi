namespace Quantum.Profiling {

  /// <summary>
  /// Gathers bandwidth statistics, uses an accumulator for feed averages into the graphs.
  /// </summary>
  public sealed class QuantumGraphProfilerBandwidth : QuantumGraphProfilerAccumulator {
    /// <summary>
    /// Type of bandwidth collected.
    /// </summary>
    public enum BandwidthType {
      /// <summary>
      /// Incoming and outgoing bandwidth.
      /// </summary>
      Total,
      /// <summary>
      /// Only incoming bandwidth.
      /// </summary>
      Incoming,
      /// <summary>
      /// Only outgoing bandwidth.
      /// </summary>
      Outgoing
    }

    /// <summary>
    /// Bandwidth type to be collected.
    /// </summary>
    public BandwidthType Type;

    /// <summary>
    /// Sample a value.
    /// </summary>
    protected override void OnUpdate() {
      var peer = QuantumGraphProfilersUtility.GetNetworkPeer();

      if (peer != null) {
        var bytes = 0L;
        switch (Type) {
          case BandwidthType.Incoming: bytes = peer.Stats.BytesIn; break;
          case BandwidthType.Outgoing: bytes = peer.Stats.BytesOut; break;
          case BandwidthType.Total: bytes = peer.Stats.BytesIn + peer.Stats.BytesOut; break;
        }

        AddValue(bytes);
      }
    }
  }
}