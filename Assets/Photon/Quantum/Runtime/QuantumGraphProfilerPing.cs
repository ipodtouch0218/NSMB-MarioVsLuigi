namespace Quantum.Profiling {
  using Photon.Client;

  /// <summary>
  /// A Quantum graph profiler that shows the RTT to the server.
  /// </summary>
  public sealed class QuantumGraphProfilerPing : QuantumGraphProfilerValueSeries {
    /// <inheritdoc/>
    protected override void OnUpdate() {
      long ping = 0;

      PhotonPeer peer = QuantumGraphProfilersUtility.GetNetworkPeer();
      if (peer != null) {
        ping = peer.Stats.RoundtripTime;
        if (ping > 9999) {
          ping = default;
        }
      }

      AddValue(ping);
    }
  }
}