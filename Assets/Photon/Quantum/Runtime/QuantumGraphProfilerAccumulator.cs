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

    public bool ComputeDelta = true;

    float[] _lastValue;
    RingBuffer<Sample>[] _samples;
    Stopwatch _stopwatch;

    /// <inheritdoc/>
    protected override void OnActivated() {
      base.OnActivated();

      _lastValue = new float[ValueDimensions];
      _samples = new RingBuffer<Sample>[ValueDimensions];
      for (int i = 0; i < ValueDimensions; i++) {
        _samples[i] = new RingBuffer<Sample>(InitialCapacity);
      }
      _stopwatch ??= Stopwatch.StartNew();
    }

    /// <inheritdoc/>
    protected override void OnDeactivated() {

      base.OnDeactivated();
    }

    private bool AverageValue(float value, int index, out float average) {
      average = 0.0f;

      if (_samples[index] == null) {
        _lastValue[index] = value;
        return false;
      }

      if (_samples[index].IsFull && _samples[index].Capacity < 1000) {
        var oldBuffer = _samples[index];
        _samples[index] = new RingBuffer<Sample>(oldBuffer.Capacity + oldBuffer.Capacity);
        var iterator = oldBuffer.GetIterator();
        while (iterator.MoveNext()) {
          _samples[index].PushFront(iterator.Current);
        }
      }

      _samples[index].PushFront(new Sample() { Timestamp = _stopwatch.ElapsedMilliseconds, Value = value - (ComputeDelta ? _lastValue[index] : 0) });
      _lastValue[index] = value;

      while (_samples[index].PeekBack().Timestamp < _stopwatch.ElapsedMilliseconds - SampleIntervalMs) {
        _samples[index].PopBack();
      }
        
      var accumulated = 0.0f;

      {
        var iterator = _samples[index].GetIterator();
        while (iterator.MoveNext()) {
          accumulated += iterator.Current.Value;
        }
      }

      average = accumulated * 1000.0f / SampleIntervalMs;
      return true;
    }

    protected override void AddValues(float value, float? value2, float? value3, float? value4) {
      AverageValue(value, 0, out float accumulated1);

      float accumulated2 = 0.0f;
      float accumulated3 = 0.0f;
      float accumulated4 = 0.0f;

      if (value2.HasValue) {
        AverageValue(value2.Value, 1, out accumulated2);
      }

      if (value3.HasValue) {
        AverageValue(value3.Value, 2, out accumulated3);
      }

      if (value4.HasValue) {
        AverageValue(value4.Value, 3, out accumulated4); ;
      }

      Log.Info($"{accumulated1}");

      base.AddValues(accumulated1, value2.HasValue ? accumulated2 : null, value3.HasValue ? accumulated3 : null, value4.HasValue ? accumulated4 : null);
    }

    /// <inheritdoc/>
    protected override void AddValue(float value) {
      if (AverageValue(value, 0, out float accumulated)) {
        base.AddValue(accumulated);
      }
    }
  }
}