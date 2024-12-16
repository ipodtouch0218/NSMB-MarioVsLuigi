namespace Quantum.Profiling {
  using UnityEngine;

  /// <summary>
  /// The rendering of a marker series.
  /// </summary>
  public sealed class QuantumGraphSeriesMarker : QuantumGraphSeries {
    /// <summary>
    /// Set the marker colors in the material.
    /// Must have certain color fields: _Marker{0}Color.
    /// </summary>
    /// <param name="colors">Color parameter</param>
    public void SetColors(params Color[] colors) {
      for (int i = 0; i < colors.Length; ++i) {
        _material.SetColor(string.Format("_Marker{0}Color", i + 1), colors[i]);
      }
    }
  }
}
