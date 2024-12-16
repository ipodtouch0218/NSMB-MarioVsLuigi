#if !QUANTUM_DEV

#region Assets/Photon/Quantum/Runtime/GraphProfilers/QuantumGraphPlayerLoopUtility.cs

namespace Quantum.Profiling {
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.LowLevel;

  /// <summary>
  /// A system to add profiler callbacks into the Unity player loop.
  /// </summary>
  public static partial class QuantumGraphPlayerLoopUtility {
    /// <summary>
    /// Resets the player loop.
    /// </summary>
    public static void SetDefaultPlayerLoopSystem() {
      PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
    }

    /// <summary>
    /// Checks if certain player loop system is already added.
    /// </summary>
    /// <param name="playerLoopSystemType">Player loop system type</param>
    /// <returns>True if the system type was found</returns>
    public static bool HasPlayerLoopSystem(Type playerLoopSystemType) {
      if (playerLoopSystemType == null)
        return false;

      PlayerLoopSystem loopSystem = PlayerLoop.GetCurrentPlayerLoop();
      for (int i = 0, subSystemCount = loopSystem.subSystemList.Length; i < subSystemCount; ++i) {

        var subSubSystems = loopSystem.subSystemList[i].subSystemList;
        if (subSubSystems == null) {
          continue;
        }

        for (int j = 0; j < subSubSystems.Length; ++j) {
          if (subSubSystems[j].type == playerLoopSystemType)
            return true;
        }
      }

      return false;
    }

    /// <summary>
    /// Adds a player loop system to the Unity player loop.
    /// </summary>
    /// <param name="playerLoopSystemType">Player loos system type</param>
    /// <param name="targetLoopSystemType">Target loop type</param>
    /// <param name="updateFunction">Update function</param>
    /// <param name="position">The position of the sub system</param>
    /// <returns>True is system was added</returns>
    /// <exception cref="ArgumentOutOfRangeException">Is raised when the position is invalid</exception>
    public static bool AddPlayerLoopSystem(Type playerLoopSystemType, Type targetLoopSystemType, PlayerLoopSystem.UpdateFunction updateFunction, int position = -1) {
      if (playerLoopSystemType == null || targetLoopSystemType == null || updateFunction == null)
        return false;

      PlayerLoopSystem loopSystem = PlayerLoop.GetCurrentPlayerLoop();
      for (int i = 0, subSystemCount = loopSystem.subSystemList.Length; i < subSystemCount; ++i) {
        PlayerLoopSystem subSystem = loopSystem.subSystemList[i];
        if (subSystem.type == targetLoopSystemType) {
          PlayerLoopSystem targetSystem = new PlayerLoopSystem();
          targetSystem.type = playerLoopSystemType;
          targetSystem.updateDelegate = updateFunction;

          List<PlayerLoopSystem> subSubSystems = new List<PlayerLoopSystem>(subSystem.subSystemList);
          if (position >= 0) {
            if (position > subSubSystems.Count)
              throw new ArgumentOutOfRangeException(nameof(position));

            subSubSystems.Insert(position, targetSystem);
            // Debug.LogWarningFormat("Added Player Loop System: {0} to: {1} position: {2}/{3}", playerLoopSystemType.FullName, subSystem.type.FullName, position, subSubSystems.Count - 1);
          } else {
            subSubSystems.Add(targetSystem);
            // Debug.LogWarningFormat("Added Player Loop System: {0} to: {1} position: {2}/{2}", playerLoopSystemType.FullName, subSystem.type.FullName, subSubSystems.Count - 1);
          }

          subSystem.subSystemList = subSubSystems.ToArray();
          loopSystem.subSystemList[i] = subSystem;

          PlayerLoop.SetPlayerLoop(loopSystem);

          return true;
        }
      }

      Debug.LogErrorFormat("Failed to add Player Loop System: {0} to: {1}", playerLoopSystemType.FullName, targetLoopSystemType.FullName);

      return false;
    }
    /// <summary>
    /// Adds a player loop system to the Unity player loop.
    /// </summary>
    /// <param name="playerLoopSystemType">Player loos system type</param>
    /// <param name="targetSubSystemType">Target sub system type</param>
    /// <param name="updateFunctionBefore">Update before function</param>
    /// <param name="updateFunctionAfter">Update after function</param>
    /// <returns>True if the system was added</returns>
    public static bool AddPlayerLoopSystem(Type playerLoopSystemType, Type targetSubSystemType, PlayerLoopSystem.UpdateFunction updateFunctionBefore, PlayerLoopSystem.UpdateFunction updateFunctionAfter) {
      if (playerLoopSystemType == null || targetSubSystemType == null || (updateFunctionBefore == null && updateFunctionAfter == null))
        return false;

      PlayerLoopSystem loopSystem = PlayerLoop.GetCurrentPlayerLoop();
      for (int i = 0, subSystemCount = loopSystem.subSystemList.Length; i < subSystemCount; ++i) {
        PlayerLoopSystem subSystem = loopSystem.subSystemList[i];
        for (int j = 0, subSubSystemCount = subSystem.subSystemList.Length; j < subSubSystemCount; ++j) {
          PlayerLoopSystem subSubSystem = subSystem.subSystemList[j];
          if (subSubSystem.type == targetSubSystemType) {
            List<PlayerLoopSystem> subSubSystems = new List<PlayerLoopSystem>(subSystem.subSystemList);
            int currentPosition = j;

            if (updateFunctionBefore != null) {
              PlayerLoopSystem playerLoopSystem = new PlayerLoopSystem();
              playerLoopSystem.type = playerLoopSystemType;
              playerLoopSystem.updateDelegate = updateFunctionBefore;

              subSubSystems.Insert(currentPosition, playerLoopSystem);

              // Debug.LogWarningFormat("Added Player Loop System: {0} to: {1} before: {2}", playerLoopSystemType.FullName, subSystem.type.FullName, subSubSystem.type.FullName);

              ++currentPosition;
            }

            if (updateFunctionAfter != null) {
              ++currentPosition;

              PlayerLoopSystem playerLoopSystem = new PlayerLoopSystem();
              playerLoopSystem.type = playerLoopSystemType;
              playerLoopSystem.updateDelegate = updateFunctionAfter;

              subSubSystems.Insert(currentPosition, playerLoopSystem);

              // Debug.LogWarningFormat("Added Player Loop System: {0} to: {1} after: {2}", playerLoopSystemType.FullName, subSystem.type.FullName, subSubSystem.type.FullName);
            }

            subSystem.subSystemList = subSubSystems.ToArray();
            loopSystem.subSystemList[i] = subSystem;

            PlayerLoop.SetPlayerLoop(loopSystem);

            return true;
          }
        }
      }

      Debug.LogErrorFormat("Failed to add Player Loop System: {0}", playerLoopSystemType.FullName);

      return false;
    }

    /// <summary>
    /// Remove a player loop system.
    /// </summary>
    /// <param name="playerLoopSystemType">System type to remove</param>
    /// <returns>True if the system was found and removed</returns>
    public static bool RemovePlayerLoopSystems(Type playerLoopSystemType) {
      if (playerLoopSystemType == null)
        return false;

      bool setPlayerLoop = false;

      PlayerLoopSystem loopSystem = PlayerLoop.GetCurrentPlayerLoop();
      for (int i = 0, subSystemCount = loopSystem.subSystemList.Length; i < subSystemCount; ++i) {
        PlayerLoopSystem subSystem = loopSystem.subSystemList[i];
        if (subSystem.subSystemList == null)
          continue;

        bool removedFromSubSystem = false;

        List<PlayerLoopSystem> subSubSystems = new List<PlayerLoopSystem>(subSystem.subSystemList);
        for (int j = subSubSystems.Count - 1; j >= 0; --j) {
          if (subSubSystems[j].type == playerLoopSystemType) {
            subSubSystems.RemoveAt(j);
            removedFromSubSystem = true;
            // Debug.LogWarningFormat("Removed Loop System: {0} from: {1}", playerLoopSystemType.FullName, subSystem.type.FullName);
          }
        }

        if (removedFromSubSystem == true) {
          setPlayerLoop = true;

          subSystem.subSystemList = subSubSystems.ToArray();
          loopSystem.subSystemList[i] = subSystem;
        }
      }

      if (setPlayerLoop == true) {
        PlayerLoop.SetPlayerLoop(loopSystem);
      }

      return setPlayerLoop;
    }

    /// <summary>
    /// Debug output of the player loop systems.
    /// </summary>
    public static void DumpPlayerLoopSystems() {
      Debug.LogError("====================================================================================================");

      PlayerLoopSystem loopSystem = PlayerLoop.GetCurrentPlayerLoop();
      for (int i = 0, subSystemCount = loopSystem.subSystemList.Length; i < subSystemCount; ++i) {
        PlayerLoopSystem subSystem = loopSystem.subSystemList[i];

        Debug.LogWarning(subSystem.type.FullName);

        List<PlayerLoopSystem> subSubSystems = new List<PlayerLoopSystem>(subSystem.subSystemList);
        for (int j = 0; j < subSubSystems.Count; ++j) {
          Debug.Log("    " + subSubSystems[j].type.FullName);
        }
      }

      Debug.LogError("====================================================================================================");
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/GraphProfilers/QuantumGraphPool.cs

namespace Quantum.Profiling {
  using System.Collections.Generic;
  using System.Runtime.CompilerServices;

  /// <summary>
  /// A pool for <see cref="QuantumGraphTimer"/> objects. 
  /// </summary>
  /// <typeparam name="T">Type </typeparam>
  public static class QuantumGraphPool<T> where T : new() {
    private const int POOL_CAPACITY = 4;
    private static List<T> _pool = new List<T>(POOL_CAPACITY);

    /// <summary>
    /// Get an item from the pool or create a new one if the pool is empty.
    /// </summary>
    /// <returns>Pooled item</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Get() {
      bool found = false;
      T item = default;

      lock (_pool) {
        int index = _pool.Count - 1;
        if (index >= 0) {
          found = true;
          item = _pool[index];

          _pool[index] = _pool[_pool.Count - 1];
          _pool.RemoveAt(_pool.Count - 1);
        }
      }

      if (found == false) {
        item = new T();
      }

      return item;
    }

    /// <summary>
    /// Return an item to the pool.
    /// </summary>
    /// <param name="item">Item to return to the pool</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(T item) {
      if (item == null)
        return;

      lock (_pool) {
        _pool.Add(item);
      }
    }
  }
}



#endregion


#region Assets/Photon/Quantum/Runtime/GraphProfilers/QuantumGraphProfiler.cs

namespace Quantum.Profiling {
  using UnityEngine;

  /// <summary>
  /// The base class for all Quantum graph profilers.
  /// </summary>
  /// <typeparam name="TGraph">Type of graph <see cref="QuantumGraphSeries"/></typeparam>
  public abstract class QuantumGraphProfiler<TGraph> : MonoBehaviour where TGraph : QuantumGraphSeries {
    /// <summary>
    /// The graph object.
    /// </summary>
    protected TGraph Graph { get; private set; }
    /// <summary>
    /// Recorded values.
    /// </summary>
    protected float[] Values { get; private set; }
    /// <summary>
    /// Is the profiler active and visible.
    /// </summary>
    protected bool IsActive { get; private set; }

    [SerializeField]
    private bool _enableOnAwake;
    [SerializeField]
    private GameObject _renderObject;

    private int _targetFPS;

    /// <summary>
    /// Toggle the visibility of the profiler.
    /// </summary>
    public void ToggleVisibility() {
      SetState(!IsActive);
    }

    /// <summary>
    /// Called during Unity <see cref="Awake"/>.
    /// </summary>
    protected virtual void OnInitialize() { }
    /// <summary>
    /// Called during Unity <see cref="OnDestroy"/>
    /// </summary>
    protected virtual void OnDeinitialize() { }
    /// <summary>
    /// Called when enabled <see cref="SetState(bool)"/>.
    /// </summary>
    protected virtual void OnActivated() { }
    /// <summary>
    /// Called when disabled <see cref="SetState(bool)"/>
    /// </summary>
    protected virtual void OnDeactivated() { }
    /// <summary>
    /// Called during Unity <see cref="Update"/>
    /// </summary>
    protected virtual void OnUpdate() { }
    /// <summary>
    /// Called when the profiler is enabled or disabled.
    /// </summary>
    protected virtual void OnRestore() { }
    /// <summary>
    /// Called when <see cref="Application.targetFrameRate"/> changed during <see cref="Update"/>.
    /// </summary>
    /// <param name="fps"></param>
    protected virtual void OnTargetFPSChanged(int fps) { }

    private void Awake() {
      Graph = GetComponentInChildren<TGraph>(true);
      Values = new float[Graph.Samples];

      Graph.Initialize();

      OnInitialize();

      SetState(_enableOnAwake);
    }

    private void Update() {
      if (_targetFPS != Application.targetFrameRate) {
        _targetFPS = Application.targetFrameRate;

        OnTargetFPSChanged(_targetFPS > 0 ? _targetFPS : 60);
      }

      OnUpdate();
    }

    private void OnDestroy() {
      OnDeinitialize();
    }

    private void OnApplicationFocus(bool focus) {
      Graph.Restore();
    }

    private void SetState(bool isActive) {
      IsActive = isActive;

      _renderObject.SetActive(isActive);

      if (isActive == true) {
        Restore();
        OnActivated();
      } else {
        OnDeactivated();
        Restore();
      }
    }

    private void Restore() {
      if (Values != null) {
        System.Array.Clear(Values, 0, Values.Length);
      }

      if (Graph != null) {
        Graph.Restore();
      }

      OnRestore();
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/GraphProfilers/QuantumGraphProfilerMarkerSeries.cs

namespace Quantum.Profiling {
  /// <summary>
  /// A graph profiler that records a series of markers.
  /// </summary>
  public abstract class QuantumGraphProfilerMarkerSeries : QuantumGraphProfiler<QuantumGraphSeriesMarker> {
    private int _offset;
    private int _samples;

    /// <inheritdoc/>
    protected override void OnActivated() {
      _offset = 0;
      _samples = 0;
    }

    /// <inheritdoc/>
    protected void SetMarkers(params bool[] markers) {
      if (IsActive == false)
        return;

      int value = 0;

      for (int i = 0; i < markers.Length; ++i) {
        if (markers[i] == true) {
          value |= 1 << i;
        }
      }

      float[] values = Values;
      values[_offset] = value;

      _offset = (_offset + 1) % values.Length;
      ++_samples;

      Graph.SetValues(values, _offset, _samples);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/GraphProfilers/QuantumGraphProfilers.cs

namespace Quantum.Profiling {
  using UnityEngine;
  using UnityEngine.PlayerLoop;

  /// <summary>
  /// The main management class in the Quantum graph profilers.
  /// </summary>
  public static class QuantumGraphProfilers {
    /// <summary>
    /// List of frame graph timers running.
    /// </summary>
    public static readonly QuantumGraphTimer FrameTimer = new QuantumGraphTimer("Frame");
    /// <summary>
    /// List of scripts timers running.
    /// </summary>
    public static readonly QuantumGraphTimer ScriptsTimer = new QuantumGraphTimer("Scripts");
    /// <summary>
    /// List of render timers running.
    /// </summary>
    public static readonly QuantumGraphTimer RenderTimer = new QuantumGraphTimer("Render");

    /// <summary>
    /// Register to unity callbacks and start the graph profilers using the Unity player loop.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeSubSystem() {
#if UNITY_EDITOR
      if (Application.isPlaying == false)
        return;
#endif
      if (QuantumGraphPlayerLoopUtility.HasPlayerLoopSystem(typeof(QuantumGraphProfilers)) == false) {
        QuantumGraphPlayerLoopUtility.AddPlayerLoopSystem(typeof(QuantumGraphProfilers), typeof(EarlyUpdate), EarlyUpdate, 0);
        QuantumGraphPlayerLoopUtility.AddPlayerLoopSystem(typeof(QuantumGraphProfilers), typeof(FixedUpdate.ScriptRunBehaviourFixedUpdate), BeforeFixedUpdate, AfterFixedUpdate);
        QuantumGraphPlayerLoopUtility.AddPlayerLoopSystem(typeof(QuantumGraphProfilers), typeof(Update.ScriptRunBehaviourUpdate), BeforeUpdate, AfterUpdate);
        QuantumGraphPlayerLoopUtility.AddPlayerLoopSystem(typeof(QuantumGraphProfilers), typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate), BeforeLateUpdate, AfterLateUpdate);
        QuantumGraphPlayerLoopUtility.AddPlayerLoopSystem(typeof(QuantumGraphProfilers), typeof(PostLateUpdate), PostLateUpdateFirst, 0);
        QuantumGraphPlayerLoopUtility.AddPlayerLoopSystem(typeof(QuantumGraphProfilers), typeof(PostLateUpdate), PostLateUpdateLast);
      }

      Application.quitting -= OnApplicationQuit;
      Application.quitting += OnApplicationQuit;
    }

    private static void EarlyUpdate() {
#if UNITY_EDITOR
      if (Application.isPlaying == false) {
        QuantumGraphPlayerLoopUtility.RemovePlayerLoopSystems(typeof(QuantumGraphProfilers));
        return;
      }
#endif

      FrameTimer.Reset();
      ScriptsTimer.Reset();
      RenderTimer.Reset();
      FrameTimer.Start();
    }

    private static void BeforeFixedUpdate() { ScriptsTimer.Start(); }
    private static void AfterFixedUpdate() { ScriptsTimer.Pause(); }
    private static void BeforeUpdate() { ScriptsTimer.Start(); }
    private static void AfterUpdate() { ScriptsTimer.Pause(); }
    private static void BeforeLateUpdate() { ScriptsTimer.Start(); }
    private static void AfterLateUpdate() { ScriptsTimer.Stop(); }

    private static void PostLateUpdateFirst() {
      RenderTimer.Start();
    }

    private static void PostLateUpdateLast() {
      RenderTimer.Stop();
      FrameTimer.Stop();
    }

    private static void OnApplicationQuit() {
      QuantumGraphPlayerLoopUtility.RemovePlayerLoopSystems(typeof(QuantumGraphProfilers));
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/GraphProfilers/QuantumGraphProfilersUtility.cs

namespace Quantum.Profiling {
  using Photon.Client;

  /// <summary>
  /// A utility class for Quantum graph profilers.
  /// </summary>
  public static class QuantumGraphProfilersUtility {
    /// <summary>
    /// Tries to find the network peer by the default runner.
    /// </summary>
    /// <returns>Photon Peer or null</returns>
    public static PhotonPeer GetNetworkPeer() {
      QuantumRunner quantumRunner = QuantumRunner.Default;
      if (quantumRunner != null && quantumRunner.NetworkClient != null) {
        return quantumRunner.NetworkClient.RealtimePeer;
      }

      return null;
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Runtime/GraphProfilers/QuantumGraphProfilerValueSeries.cs

namespace Quantum.Profiling {

  /// <summary>
  /// Records a series of values for a graph profiler.
  /// </summary>
  public abstract class QuantumGraphProfilerValueSeries : QuantumGraphProfiler<QuantumGraphSeriesValue> {
    private int _offset;
    private int _samples;

    /// <inheritdoc/>
    protected override void OnActivated() {
      _offset = 0;
      _samples = 0;
    }

    /// <summary>
    /// Add a value to the series.
    /// </summary>
    /// <param name="value">Value to record</param>
    protected void AddValue(float value) {
      if (IsActive == false)
        return;

      float[] values = Values;
      values[_offset] = value;

      _offset = (_offset + 1) % values.Length;
      ++_samples;

      Graph.SetValues(values, _offset, _samples);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/GraphProfilers/QuantumGraphSeries.cs

namespace Quantum.Profiling {
  using UnityEngine;
  using UnityEngine.UI;

  /// <summary>
  /// A graph series is the base class to render graphs in Unity.
  /// </summary>
  public abstract class QuantumGraphSeries : MonoBehaviour {
    private const string SHADER_PROPERTY_VALUES = "_Values";
    private const string SHADER_PROPERTY_SAMPLES = "_Samples";

    /// <summary>
    /// Returns the number of samples that can be displayed.
    /// </summary>
    public int Samples =>  _samples;

    /// <summary>
    /// The target image to render the graphs in.
    /// </summary>
    [SerializeField]
    protected Image _targetImage;
    /// <summary>
    /// The number of samples that are displayed.
    /// </summary>
    [SerializeField]
    [Range(60, 540)]
    protected int _samples = 300;
    /// <summary>
    /// The values to render.
    /// </summary>
    protected float[] _values;
    /// <summary>
    /// The material to render the graph.
    /// </summary>
    protected Material _material;
    /// <summary>
    /// Cache shared property.
    /// </summary>
    protected int _valuesShaderPropertyID;

    /// <inheritdoc/>
    protected virtual void OnInitialize() { }
    /// <inheritdoc/>
    protected virtual void OnRestore() { }
    /// <inheritdoc/>
    public void Initialize() {
      _valuesShaderPropertyID = Shader.PropertyToID(SHADER_PROPERTY_VALUES);

      _values = new float[_samples];

      _material = new Material(_targetImage.material);
      _targetImage.material = _material;

      Restore();

      OnInitialize();
    }

    /// <summary>
    /// Set the values to render
    /// </summary>
    /// <param name="values">Values array</param>
    /// <param name="offset">Offset to start reading values from the array</param>
    /// <param name="samples">Number of samples to display</param>
    public virtual void SetValues(float[] values, int offset, int samples) {
      if (_values == null || values == null || _values.Length != values.Length)
        return;

      for (int i = 0; i < _samples; ++i, ++offset) {
        offset %= _samples;

        _values[i] = values[offset];
      }

      _material.SetFloatArray(_valuesShaderPropertyID, _values);
    }

    /// <inheritdoc/>
    public void Restore() {
      if (_material != null) {
        _material.SetInt(SHADER_PROPERTY_SAMPLES, _samples);
        _material.SetFloatArray(_valuesShaderPropertyID, _values);
      }

      OnRestore();
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Runtime/GraphProfilers/QuantumGraphTimer.cs

namespace Quantum.Profiling {
  using System;
  using System.Diagnostics;
  using System.Runtime.CompilerServices;

  /// <summary>
  /// The Quantum graph timer to measure time with a stopwatch.
  /// </summary>
  public sealed partial class QuantumGraphTimer {
    /// <summary>
    /// The state of the timer.
    /// </summary>
    public enum EState {
      /// <summary>
      /// Timer stopped
      /// </summary>
      Stopped = 0,
      /// <summary>
      /// Timer running
      /// </summary>
      Running = 1,
      /// <summary>
      /// Timer paused
      /// </summary>
      Paused = 2,
    }

    // PUBLIC MEMBERS

    /// <summary>
    /// The time id
    /// </summary>
    public readonly int ID;
    /// <summary>
    /// The time name
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// Returns the timer state.
    /// </summary>
    public EState State { get { return _state; } }
    /// <summary>
    /// Counts how many time the timer has been updated.
    /// </summary>
    public int Counter { get { return _counter; } }
    /// <summary>
    /// The total accumulated time.
    /// </summary>
    public TimeSpan TotalTime { get { if (_state == EState.Running) { Update(); } return new TimeSpan(_totalTicks); } }
    /// <summary>
    /// The time after the last start.
    /// </summary>
    public TimeSpan RecentTime { get { if (_state == EState.Running) { Update(); } return new TimeSpan(_recentTicks); } }
    /// <summary>
    /// The peak time measured.
    /// </summary>
    public TimeSpan PeakTime { get { if (_state == EState.Running) { Update(); } return new TimeSpan(_peakTicks); } }
    /// <summary>
    /// The time in the last update.
    /// </summary>
    public TimeSpan LastTime { get { if (_state == EState.Running) { Update(); } return new TimeSpan(_lastTicks); } }

    // PRIVATE MEMBERS

    private EState _state;
    private int _counter;
    private long _baseTicks;
    private long _totalTicks;
    private long _recentTicks;
    private long _peakTicks;
    private long _lastTicks;

    // CONSTRUCTORS

    /// <summary>
    /// Create a new timer.
    /// </summary>
    public QuantumGraphTimer() : this(null) {
    }

    /// <summary>
    /// Create a new timer with a name.
    /// </summary>
    /// <param name="name">Name</param>
    public QuantumGraphTimer(string name) : this(-1, name) {
    }

    /// <summary>
    /// Create a new timer with an id and a name.
    /// </summary>
    /// <param name="id">Timer id</param>
    /// <param name="name">Timer name</param>
    public QuantumGraphTimer(int id, string name) {
      ID = id;
      Name = name;
    }

    // PUBLIC METHODS

    /// <summary>
    /// Start the timer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Start() {
      if (_state == EState.Running)
        return;

      if (_state != EState.Paused) {
        if (_recentTicks != 0) {
          _lastTicks = _recentTicks;
          _recentTicks = 0;
        }
      }

      _baseTicks = Stopwatch.GetTimestamp();
      _state = EState.Running;
    }

    /// <summary>
    /// Pause the timer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Pause() {
      if (_state != EState.Running)
        return;

      Update();

      _state = EState.Paused;
    }

    /// <summary>
    /// Stop the timer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Stop() {
      if (_state == EState.Running) {
        Update();
      }

      _state = EState.Stopped;
    }

    /// <summary>
    /// Restart the timer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Restart() {
      if (_recentTicks != 0) {
        _lastTicks = _recentTicks;
      }

      _state = EState.Running;
      _counter = 1;
      _baseTicks = Stopwatch.GetTimestamp();
      _recentTicks = 0;
      _totalTicks = 0;
      _peakTicks = 0;
    }

    /// <summary>
    /// Reset the timer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() {
      if (_recentTicks != 0) {
        _lastTicks = _recentTicks;
      }

      _state = EState.Stopped;
      _counter = 0;
      _baseTicks = 0;
      _recentTicks = 0;
      _totalTicks = 0;
      _peakTicks = 0;
    }

    /// <summary>
    /// Return the timer to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return() {
      Return(this);
    }

    /// <summary>
    /// Combine the timer with another timer.
    /// </summary>
    /// <param name="other">Other timer</param>
    public void Combine(QuantumGraphTimer other) {
      if (other._state == EState.Running) {
        other.Update();
      }

      _totalTicks += other._totalTicks;

      if (_state == EState.Stopped) {
        _recentTicks = other._recentTicks;
        if (_recentTicks > _peakTicks) {
          _peakTicks = _recentTicks;
        }
      }

      if (other._peakTicks > _peakTicks) {
        _peakTicks = other._peakTicks;
      }

      _counter += other._counter;
    }

    /// <summary>
    /// Get the total time in seconds.
    /// </summary>
    /// <returns>Time in seconds</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetTotalSeconds() {
      return (float)TotalTime.TotalSeconds;
    }

    /// <summary>
    /// Get the total time in milliseconds.
    /// </summary>
    /// <returns>Time in ms</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetTotalMilliseconds() {
      return (float)TotalTime.TotalMilliseconds;
    }

    /// <summary>
    /// Get the recent time in seconds since the last start.
    /// </summary>
    /// <returns>TIme in seconds</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetRecentSeconds() {
      return (float)RecentTime.TotalSeconds;
    }

    /// <summary>
    /// Get the recent time in milliseconds since the last start.
    /// </summary>
    /// <returns>Time in ms</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetRecentMilliseconds() {
      return (float)RecentTime.TotalMilliseconds;
    }

    /// <summary>
    /// Get the peak seconds measured.
    /// </summary>
    /// <returns>Peak seconds</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetPeakSeconds() {
      return (float)PeakTime.TotalSeconds;
    }

    /// <summary>
    /// Get the peak milliseconds measured.
    /// </summary>
    /// <returns>Peak ms</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetPeakMilliseconds() {
      return (float)PeakTime.TotalMilliseconds;
    }

    /// <summary>
    /// Get the seconds measured of the last update.
    /// </summary>
    /// <returns>Time in seconds</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetLastSeconds() {
      return (float)LastTime.TotalSeconds;
    }

    /// <summary>
    /// Get the milliseconds measured of the last update.
    /// </summary>
    /// <returns>Time in ms</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetLastMilliseconds() {
      return (float)LastTime.TotalMilliseconds;
    }

    /// <summary>
    /// Create a new timer or get one from the pool.
    /// </summary>
    /// <param name="start">Set true to automatically start the timer.</param>
    /// <returns>A timer object</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuantumGraphTimer Get(bool start = false) {
      QuantumGraphTimer timer = QuantumGraphPool<QuantumGraphTimer>.Get();
      if (start == true) {
        timer.Restart();
      }
      return timer;
    }

    /// <summary>
    /// Return the timer object to the pool.
    /// </summary>
    /// <param name="timer">Timer</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(QuantumGraphTimer timer) {
      timer.Reset();
      QuantumGraphPool<QuantumGraphTimer>.Return(timer);
    }

    // PRIVATE METHODS

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Update() {
      long ticks = Stopwatch.GetTimestamp();

      _totalTicks += ticks - _baseTicks;
      _recentTicks += ticks - _baseTicks;

      _baseTicks = ticks;

      if (_recentTicks > _peakTicks) {
        _peakTicks = _recentTicks;
      }

      if (_totalTicks < 0L) {
        _totalTicks = 0L;
      }

      ++_counter;
    }
  }
}

#endregion

#endif
