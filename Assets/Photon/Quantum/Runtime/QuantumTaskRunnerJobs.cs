namespace Quantum {
  using System;
  using Photon.Analyzer;
  using Photon.Deterministic;
  using global::Unity.Collections;
  using global::Unity.Jobs;
  using global::Unity.Jobs.LowLevel.Unsafe;
  using UnityEngine;
  using UnityEngine.Profiling;
  using Object = UnityEngine.Object;
  using static QuantumUnityExtensions;

  [Serializable]
  public enum QuantumJobsMaxCountMode {
    AutoDetect,
    Custom,
  }

  public class QuantumTaskRunnerJobs : QuantumMonoBehaviour, IDisposable, IDeterministicPlatformTaskRunner {
    struct ActionJob : IJob {
      public int Index;

      public void Execute() {
        _delegates[Index]();
      }
    }

    public bool dontDestroyOnLoad = true;

    public QuantumJobsMaxCountMode quantumJobsMaxCountMode = QuantumJobsMaxCountMode.AutoDetect;

    protected virtual int CustomQuantumJobsMaxCount => DefaultQuantumJobsMaxCount;

    protected int DefaultQuantumJobsMaxCount => JobsUtility.JobWorkerCount - 1;

    [StaticField(StaticFieldResetMode.None)]
    static Action[] _delegates;

    private void Awake() {
      if (dontDestroyOnLoad) {
        DontDestroyOnLoad(this);
      }
    }

    public static QuantumTaskRunnerJobs GetInstance() {
      var instance = FindFirstObjectByType<QuantumTaskRunnerJobs>();
      if (instance) {
        return instance;
      }

      var go = new GameObject(nameof(QuantumTaskRunnerJobs));
      return go.AddComponent<QuantumTaskRunnerJobs>();
    }


    NativeArray<JobHandle> _handles;
    ActionJob[]            _jobs;

    public virtual void Schedule(Action[] delegates) {
      int maxScheduleLength;

      if (quantumJobsMaxCountMode == QuantumJobsMaxCountMode.AutoDetect) {
        maxScheduleLength = Math.Min(delegates.Length, DefaultQuantumJobsMaxCount);
      } else if (quantumJobsMaxCountMode == QuantumJobsMaxCountMode.Custom) {
        maxScheduleLength = CustomQuantumJobsMaxCount;
      } else {
        throw new ArgumentOutOfRangeException(nameof(quantumJobsMaxCountMode), quantumJobsMaxCountMode, $"{nameof(QuantumJobsMaxCountMode)} not supported.");
      }

      Schedule(delegates, maxScheduleLength);
    }

    private void Schedule(Action[] delegates, int jobsMaxCount) {
      var jobsCount = Math.Max(0, Math.Min(jobsMaxCount, delegates.Length));
      if (jobsCount <= 0) {
        return;
      }

      Profiler.BeginSample("Schedule");
      _delegates = delegates;


      Profiler.BeginSample("Array Creation");
      if (_jobs == null || _jobs.Length != jobsCount) {
        _jobs = new ActionJob[jobsCount];

        if (_handles.IsCreated) {
          _handles.Dispose();
          _handles = default;
        }

        _handles = new NativeArray<JobHandle>(jobsCount, global::Unity.Collections.Allocator.Persistent);
      }

      Profiler.EndSample();

      Profiler.BeginSample("Job Scheduling");
      for (int i = 0; i < jobsCount; ++i) {
        // create job
        _jobs[i] = new ActionJob {
          Index = i,
        };

        // schedule it
        _handles[i] = _jobs[i].Schedule();
      }

      Profiler.EndSample();

      Profiler.BeginSample("JobHandle.ScheduleBatchedJobs");
      JobHandle.ScheduleBatchedJobs();
      Profiler.EndSample();

      Profiler.EndSample();
    }

    public bool PollForComplete() {
      if (_handles.IsCreated) {
        for (int i = 0; i < _handles.Length; ++i) {
          if (_handles[i].IsCompleted == false) {
            return false;
          }
        }
      }

      return true;
    }

    public void WaitForComplete() {
      JobHandle.CompleteAll(_handles);
    }

    void OnDestroy() {
      Dispose();
    }

    public void Dispose() {
      if (_handles.IsCreated) {
        _handles.Dispose();
        _handles = default;
      }

      _jobs      = null;
      _delegates = null;
    }
  }
}