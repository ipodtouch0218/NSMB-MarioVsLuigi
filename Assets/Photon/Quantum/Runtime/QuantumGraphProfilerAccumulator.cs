namespace Quantum.Profiling {
  using Quantum.Core;
  using System.Diagnostics;

  /// <summary>
  /// Accumulates values samples at random times to feed a constant average to the graphs.
  /// </summary>
  public class QuantumGraphProfilerAccumulator : QuantumGraphProfilerValueSeries {
    /// <summary>
    /// Initial buffer capacity, will automatically resize.
    /// </summary>
    public int InitialCapacity = 60;
    /// <summary>
    /// Accumulates values in this time windows in milliseconds.
    /// </summary>
    public int SampleIntervalMs = 50;

    struct Sample { public double Timestamp; public float Value; }

    float _lastValue;
    RingBuffer<Sample> _samples;
    Stopwatch _stopwatch;

    /// <inheritdoc/>
    protected override void OnActivated() {
      base.OnActivated();

      _samples ??= new RingBuffer<Sample>(InitialCapacity);
      _stopwatch ??= Stopwatch.StartNew();
    }

    /// <inheritdoc/>
    protected override void OnDeactivated() {

      base.OnDeactivated();
    }

    /// <inheritdoc/>
    protected override void AddValue(float value) {
      if (_samples == null) {
        _lastValue = value;
        return;
      }

      if (_samples.IsFull && _samples.Capacity < 1000) {
        var oldBuffer = _samples;
        _samples = new RingBuffer<Sample>(oldBuffer.Capacity + oldBuffer.Capacity);
        var iterator = oldBuffer.GetIterator();
        while (iterator.MoveNext()) {
          _samples.PushFront(iterator.Current);
        }
      } 

      _samples.PushFront(new Sample() { Timestamp = _stopwatch.ElapsedMilliseconds, Value = value - _lastValue });
      _lastValue = value;

      while (_samples.PeekBack().Timestamp < _stopwatch.ElapsedMilliseconds - SampleIntervalMs) {
        _samples.PopBack();
      }

      var accumulated = 0.0f;

      {
        var iterator = _samples.GetIterator();
        while (iterator.MoveNext()) {
          accumulated += iterator.Current.Value;
        }
      }

      base.AddValue(accumulated * 1000.0f / SampleIntervalMs);
    }
  }
}