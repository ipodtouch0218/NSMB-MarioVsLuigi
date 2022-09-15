#if FUSION_PROFILER_INTEGRATION
using Unity.Profiling;
using UnityEngine;

public static class FusionProfiler {
  [RuntimeInitializeOnLoadMethod]
  static void Init() {
    Fusion.EngineProfiler.InterpolationOffsetCallback      = f => InterpolationOffset.Sample(f);
    Fusion.EngineProfiler.InterpolationTimeScaleCallback   = f => InterpolationTimeScale.Sample(f);
    Fusion.EngineProfiler.InterpolationMultiplierCallback  = f => InterpolationMultiplier.Sample(f);
    Fusion.EngineProfiler.InterpolationUncertaintyCallback = f => InterpolationUncertainty.Sample(f);

    Fusion.EngineProfiler.ResimulationsCallback     = i => Resimulations.Sample(i);
    Fusion.EngineProfiler.WorldSnapshotSizeCallback = i => WorldSnapshotSize.Sample(i);

    Fusion.EngineProfiler.RoundTripTimeCallback = f => RoundTripTime.Sample(f);

    Fusion.EngineProfiler.InputSizeCallback  = i => InputSize.Sample(i);
    Fusion.EngineProfiler.InputQueueCallback = i => InputQueue.Sample(i);

    Fusion.EngineProfiler.RpcInCallback  = i => RpcIn.Value  += i;
    Fusion.EngineProfiler.RpcOutCallback = i => RpcOut.Value += i;

    Fusion.EngineProfiler.SimualtionTimeScaleCallback = f => SimulationTimeScale.Sample(f);
    
    Fusion.EngineProfiler.InputOffsetCallback          = f => InputOffset.Sample(f);
    Fusion.EngineProfiler.InputOffsetDeviationCallback = f => InputOffsetDeviation.Sample(f);

    Fusion.EngineProfiler.InputRecvDeltaCallback          = f => InputRecvDelta.Sample(f);
    Fusion.EngineProfiler.InputRecvDeltaDeviationCallback = f => InputRecvDeltaDeviation.Sample(f);
  }

  public static readonly ProfilerCategory Category = ProfilerCategory.Scripts;

  public static readonly ProfilerCounter<float> InterpolationOffset      = new ProfilerCounter<float>(Category, "Interp Offset",      ProfilerMarkerDataUnit.Count);
  public static readonly ProfilerCounter<float> InterpolationTimeScale   = new ProfilerCounter<float>(Category, "Interp Time Scale",  ProfilerMarkerDataUnit.Count);
  public static readonly ProfilerCounter<float> InterpolationMultiplier  = new ProfilerCounter<float>(Category, "Interp Multiplier",  ProfilerMarkerDataUnit.Count);
  public static readonly ProfilerCounter<float> InterpolationUncertainty = new ProfilerCounter<float>(Category, "Interp Uncertainty", ProfilerMarkerDataUnit.Undefined);

  public static readonly ProfilerCounter<int> InputSize  = new ProfilerCounter<int>(Category, "Client Input Size",  ProfilerMarkerDataUnit.Bytes);
  public static readonly ProfilerCounter<int> InputQueue = new ProfilerCounter<int>(Category, "Client Input Queue", ProfilerMarkerDataUnit.Count);

  public static readonly ProfilerCounter<int>   WorldSnapshotSize = new ProfilerCounter<int>(Category, "Client Snapshot Size", ProfilerMarkerDataUnit.Bytes);
  public static readonly ProfilerCounter<int>   Resimulations     = new ProfilerCounter<int>(Category, "Client Resims",        ProfilerMarkerDataUnit.Count);
  public static readonly ProfilerCounter<float> RoundTripTime     = new ProfilerCounter<float>(Category, "Client RTT", ProfilerMarkerDataUnit.Count);

  public static readonly ProfilerCounterValue<int> RpcIn  = new ProfilerCounterValue<int>(Category, "RPCs In",  ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);
  public static readonly ProfilerCounterValue<int> RpcOut = new ProfilerCounterValue<int>(Category, "RPCs Out", ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

  public static readonly ProfilerCounter<float> SimulationTimeScale = new ProfilerCounter<float>(Category, "Simulation Time Scale", ProfilerMarkerDataUnit.Count);

  public static readonly ProfilerCounter<float> InputOffset          = new ProfilerCounter<float>(Category, "Input Offset",     ProfilerMarkerDataUnit.Count);
  public static readonly ProfilerCounter<float> InputOffsetDeviation = new ProfilerCounter<float>(Category, "Input Offset Dev", ProfilerMarkerDataUnit.Count);

  public static readonly ProfilerCounter<float> InputRecvDelta          = new ProfilerCounter<float>(Category, "Input Recv Delta",     ProfilerMarkerDataUnit.Count);
  public static readonly ProfilerCounter<float> InputRecvDeltaDeviation = new ProfilerCounter<float>(Category, "Input Recv Delta Dev", ProfilerMarkerDataUnit.Count);
}
#endif