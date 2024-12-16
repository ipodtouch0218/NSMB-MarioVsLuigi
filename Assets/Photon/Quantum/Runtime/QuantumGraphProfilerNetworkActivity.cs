namespace Quantum.Profiling {
  using Photon.Client;

  /// <summary>
  /// A Quantum graph profiler that shows the time delta between the last received packet.
  /// </summary>
  public sealed class QuantumGraphProfilerNetworkActivity : QuantumGraphProfilerValueSeries {
    /// <inheritdoc/>
    protected override void OnUpdate() {
      float interval = 0;

      PhotonPeer peer = QuantumGraphProfilersUtility.GetNetworkPeer();
      if (peer != null) {
        interval = peer.ConnectionTime - peer.Stats.LastReceiveTimestamp;
        if (interval > 9999) {
          interval = default;
        }
      }

      AddValue(interval * 0.001f);
    }

    /// <inheritdoc/>
    protected override void OnTargetFPSChanged(int fps) {
      float frameMs = 1.0f / fps;
      Graph.SetThresholds(frameMs * 2.0f, frameMs * 4.0f, frameMs * 8.0f);
    }
  }
}
