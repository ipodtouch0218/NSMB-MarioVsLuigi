#if !FUSION_DEV

#region Assets/Photon/Fusion/Runtime/FusionAssetSource.Common.cs

// merged AssetSource

#region NetworkAssetSourceAddressable.cs

#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
namespace Fusion {
  using System;
#if UNITY_EDITOR
  using UnityEditor;
#endif
  using UnityEngine;
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;
  using Object = UnityEngine.Object;

  [Serializable]
  public partial class NetworkAssetSourceAddressable<T> where T : UnityEngine.Object {
    public AssetReference Address;
    
    [NonSerialized]
    private int _acquireCount;

    public void Acquire(bool synchronous) {
      if (_acquireCount == 0) {
        LoadInternal(synchronous);
      }
      _acquireCount++;
    }

    public void Release() {
      if (_acquireCount <= 0) {
        throw new Exception("Asset is not loaded");
      }
      if (--_acquireCount == 0) {
        UnloadInternal();
      }
    }

    public bool IsCompleted => Address.IsDone;

    public T WaitForResult() {
      Debug.Assert(Address.IsValid());
      var op = Address.OperationHandle;
      if (!op.IsDone) {
        try {
          op.WaitForCompletion();
        } catch (Exception e) when (!Application.isPlaying && typeof(Exception) == e.GetType()) {
          Debug.LogError($"An exception was thrown when loading asset: {Address}; since this method " +
            $"was called from the editor, it may be due to the fact that Addressables don't have edit-time load support. Please use EditorInstance instead.");
          throw;
        }
      }
      
      if (op.OperationException != null) {
        throw new InvalidOperationException($"Failed to load asset: {Address}", op.OperationException);
      }
      
      Debug.AssertFormat(op.Result != null, "op.Result != null");
      return ValidateResult(op.Result);
    }
    
    private void LoadInternal(bool synchronous) {
      Debug.Assert(!Address.IsValid());

      var op = Address.LoadAssetAsync<UnityEngine.Object>();
      if (!op.IsValid()) {
        throw new Exception($"Failed to load asset: {Address}");
      }
      if (op.Status == AsyncOperationStatus.Failed) {
        throw new Exception($"Failed to load asset: {Address}", op.OperationException);
      }
      
      if (synchronous) {
        op.WaitForCompletion();
      }
    }

    private void UnloadInternal() {
      Debug.Assert(Address.IsValid());
      Address.ReleaseAsset();
    }

    private T ValidateResult(object result) {
      if (result == null) {
        throw new InvalidOperationException($"Failed to load asset: {Address}; asset is null");
      }
      if (typeof(T).IsSubclassOf(typeof(Component))) {
        if (result is GameObject gameObject == false) {
          throw new InvalidOperationException($"Failed to load asset: {Address}; asset is not a GameObject, but a {result.GetType()}");
        }
        
        var component = ((GameObject)result).GetComponent<T>();
        if (!component) {
          throw new InvalidOperationException($"Failed to load asset: {Address}; asset does not contain component {typeof(T)}");
        }

        return component;
      }

      if (result is T asset) {
        return asset;
      }
      
      throw new InvalidOperationException($"Failed to load asset: {Address}; asset is not of type {typeof(T)}, but {result.GetType()}");
    }
    
    public string Description => "Address: " + Address.RuntimeKey;
    
#if UNITY_EDITOR
    public T EditorInstance {
      get {
        var editorAsset = Address.editorAsset;
        if (string.IsNullOrEmpty(Address.SubObjectName)) {
          return ValidateResult(editorAsset);
        } else {
          var assetPath = AssetDatabase.GUIDToAssetPath(Address.AssetGUID);
          var assets    = AssetDatabase.LoadAllAssetsAtPath(assetPath);
          foreach (var asset in assets) {
            if (asset.name == Address.SubObjectName) {
              return ValidateResult(asset);
            }
          }

          return null;
        }
      }
    }
#endif
  }
}
#endif

#endregion


#region NetworkAssetSourceResource.cs

namespace Fusion {
  using System;
  using System.Runtime.ExceptionServices;
  using UnityEngine;
  using Object = UnityEngine.Object;
  using UnityResources = UnityEngine.Resources;

  [Serializable]
  public partial class NetworkAssetSourceResource<T> where T : UnityEngine.Object {
    [UnityResourcePath(typeof(Object))]
    public string ResourcePath;
    public string SubObjectName;

    [NonSerialized]
    private object _state;
    [NonSerialized]
    private int    _acquireCount;

    public void Acquire(bool synchronous) {
      if (_acquireCount == 0) {
        LoadInternal(synchronous);
      }
      _acquireCount++;
    }

    public void Release() {
      if (_acquireCount <= 0) {
        throw new Exception("Asset is not loaded");
      }
      if (--_acquireCount == 0) {
        UnloadInternal();
      }
    }

    public bool IsCompleted {
      get {
        if (_state == null) {
          // hasn't started
          return false;
        }
        
        if (_state is ResourceRequest asyncOp && !asyncOp.isDone) {
          // still loading, wait
          return false;
        }

        return true;
      }
    }

    public T WaitForResult() {
      Debug.Assert(_state != null);
      if (_state is ResourceRequest asyncOp) {
        if (asyncOp.isDone) {
          FinishAsyncOp(asyncOp);
        } else {
          // just load synchronously, then pass through
          _state = null;
          LoadInternal(synchronous: true);
        }
      }
      
      if (_state == null) {
        throw new InvalidOperationException($"Failed to load asset {typeof(T)}: {ResourcePath}[{SubObjectName}]. Asset is null.");  
      }

      if (_state is T asset) {
        return asset;
      }

      if (_state is ExceptionDispatchInfo exception) {
        exception.Throw();
        throw new NotSupportedException();
      }

      throw new InvalidOperationException($"Failed to load asset {typeof(T)}: {ResourcePath}, SubObjectName: {SubObjectName}");
    }

    private void FinishAsyncOp(ResourceRequest asyncOp) {
      try {
        var asset = string.IsNullOrEmpty(SubObjectName) ? asyncOp.asset : LoadNamedResource(ResourcePath, SubObjectName);
        if (asset) {
          _state = asset;
        } else {
          throw new InvalidOperationException($"Missing Resource: {ResourcePath}, SubObjectName: {SubObjectName}");
        }
      } catch (Exception ex) {
        _state = ExceptionDispatchInfo.Capture(ex);
      }
    }
    
    private static T LoadNamedResource(string resoucePath, string subObjectName) {
      var assets = UnityResources.LoadAll<T>(resoucePath);

      for (var i = 0; i < assets.Length; ++i) {
        var asset = assets[i];
        if (string.Equals(asset.name, subObjectName, StringComparison.Ordinal)) {
          return asset;
        }
      }

      return null;
    }
    
    private void LoadInternal(bool synchronous) {
      Debug.Assert(_state == null);
      try {
        if (synchronous) {
          _state = string.IsNullOrEmpty(SubObjectName) ? UnityResources.Load<T>(ResourcePath) : LoadNamedResource(ResourcePath, SubObjectName);
        } else {
          _state = UnityResources.LoadAsync<T>(ResourcePath);
        }

        if (_state == null) {
          _state = new InvalidOperationException($"Missing Resource: {ResourcePath}, SubObjectName: {SubObjectName}");
        }
      } catch (Exception ex) {
        _state = ExceptionDispatchInfo.Capture(ex);
      }
    }

    private void UnloadInternal() {
      if (_state is ResourceRequest asyncOp) {
        asyncOp.completed += op => {
          // unload stuff
        };
      } else if (_state is Object) {
        // unload stuff
      }

      _state = null;
    }
    
    public string Description => $"Resource: {ResourcePath}{(!string.IsNullOrEmpty(SubObjectName) ? $"[{SubObjectName}]" : "")}";
    
#if UNITY_EDITOR
    public T EditorInstance => string.IsNullOrEmpty(SubObjectName) ? UnityResources.Load<T>(ResourcePath) : LoadNamedResource(ResourcePath, SubObjectName);
#endif
  }
}

#endregion


#region NetworkAssetSourceStatic.cs

namespace Fusion {
  using System;
#if UNITY_EDITOR
  using UnityEditor;
#endif
  using UnityEngine;
  using UnityEngine.Serialization;
  using Object = UnityEngine.Object;

  [Serializable]
  public partial class NetworkAssetSourceStatic<T> where T : UnityEngine.Object {
    public T Prefab;
    
    public bool IsCompleted => true;

    public void Acquire(bool synchronous) {
      // do nothing
    }

    public void Release() {
      // do nothing
    }

    public T WaitForResult() {
      if (Prefab == null) {
        throw new InvalidOperationException("Missing static reference");
      }

      return Prefab;
    }
    
    public string Description {
      get {
        if (Prefab) {
#if UNITY_EDITOR
          if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Prefab, out var guid, out long fileID)) {
            return $"Static: {guid}, fileID: {fileID}";
          }
#endif
          return "Static: " + Prefab;
        } else {
          return "Static: (null)";
        }
      }
    }
    
#if UNITY_EDITOR
    public T EditorInstance => Prefab;
#endif
  }
}

#endregion


#region NetworkAssetSourceStaticLazy.cs

namespace Fusion {
  using System;
#if UNITY_EDITOR
  using UnityEditor;
#endif
  using UnityEngine;
  using UnityEngine.Serialization;
  using Object = UnityEngine.Object;

  [Serializable]
  public partial class NetworkAssetSourceStaticLazy<T> where T : UnityEngine.Object {
    public LazyLoadReference<T> Prefab;
    
    public bool IsCompleted => true;

    public void Acquire(bool synchronous) {
      // do nothing
    }

    public void Release() {
      // do nothing
    }

    public T WaitForResult() {
      if (Prefab.asset == null) {
        throw new InvalidOperationException("Missing static reference");
      }

      return Prefab.asset;
    }
    
    public string Description {
      get {
        if (Prefab.isBroken) {
          return "Static: (broken)";
        } else if (Prefab.isSet) {
#if UNITY_EDITOR
          if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Prefab.instanceID, out var guid, out long fileID)) {
            return $"Static: {guid}, fileID: {fileID}";
          }
#endif
          return "Static: " + Prefab.asset;
        } else {
          return "Static: (null)";
        }
      }
    }
    
#if UNITY_EDITOR
    public T EditorInstance => Prefab.asset;
#endif
  }
}

#endregion



#endregion


#region Assets/Photon/Fusion/Runtime/FusionBurstIntegration.cs

// deleted

#endregion


#region Assets/Photon/Fusion/Runtime/FusionCoroutine.cs

﻿
namespace Fusion {
  using UnityEngine;
  using System;
  using System.Collections;
  using System.Runtime.ExceptionServices;

  public sealed class FusionCoroutine : ICoroutine, IDisposable  {
    private readonly IEnumerator             _inner;
    private          Action<IAsyncOperation> _completed;
    private          float                   _progress;
    private          Action                  _activateAsync;

    public FusionCoroutine(IEnumerator inner) {
      _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }
      
    public event Action<IAsyncOperation> Completed
    {
      add {
        _completed += value;
        if (IsDone) {
          value(this);
        }
      }
      remove => _completed -= value;
    }

    public bool                  IsDone { get; private set; }
    public ExceptionDispatchInfo Error  { get; private set; }

    bool IEnumerator.MoveNext() {
      try {
        if (_inner.MoveNext()) {
          return true;
        } else {
          IsDone = true;
          _completed?.Invoke(this);
          return false;
        }
      } catch (Exception e) {
        IsDone = true;
        Error  = ExceptionDispatchInfo.Capture(e);
        _completed?.Invoke(this);
        return false;
      }
    }

    void IEnumerator.Reset() {
      _inner.Reset();
      IsDone = false;
      Error  = null;
    }

    object IEnumerator.Current => _inner.Current;
      
    public void Dispose() {
      if (_inner is IDisposable disposable) {
        disposable.Dispose();
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Runtime/FusionProfiler.cs

namespace Fusion {
#if FUSION_PROFILER_INTEGRATION
  using Unity.Profiling;
  using UnityEngine;

  public static class FusionProfiler {
    [RuntimeInitializeOnLoadMethod]
    static void Init() {
      Fusion.EngineProfiler.InterpolationOffsetCallback = f => InterpolationOffset.Sample(f);
      Fusion.EngineProfiler.InterpolationTimeScaleCallback = f => InterpolationTimeScale.Sample(f);
      Fusion.EngineProfiler.InterpolationMultiplierCallback = f => InterpolationMultiplier.Sample(f);
      Fusion.EngineProfiler.InterpolationUncertaintyCallback = f => InterpolationUncertainty.Sample(f);

      Fusion.EngineProfiler.ResimulationsCallback = i => Resimulations.Sample(i);
      Fusion.EngineProfiler.WorldSnapshotSizeCallback = i => WorldSnapshotSize.Sample(i);

      Fusion.EngineProfiler.RoundTripTimeCallback = f => RoundTripTime.Sample(f);

      Fusion.EngineProfiler.InputSizeCallback = i => InputSize.Sample(i);
      Fusion.EngineProfiler.InputQueueCallback = i => InputQueue.Sample(i);

      Fusion.EngineProfiler.RpcInCallback = i => RpcIn.Value += i;
      Fusion.EngineProfiler.RpcOutCallback = i => RpcOut.Value += i;

      Fusion.EngineProfiler.SimualtionTimeScaleCallback = f => SimulationTimeScale.Sample(f);

      Fusion.EngineProfiler.InputOffsetCallback = f => InputOffset.Sample(f);
      Fusion.EngineProfiler.InputOffsetDeviationCallback = f => InputOffsetDeviation.Sample(f);

      Fusion.EngineProfiler.InputRecvDeltaCallback = f => InputRecvDelta.Sample(f);
      Fusion.EngineProfiler.InputRecvDeltaDeviationCallback = f => InputRecvDeltaDeviation.Sample(f);
    }

    public static readonly ProfilerCategory Category = ProfilerCategory.Scripts;

    public static readonly ProfilerCounter<float> InterpolationOffset = new ProfilerCounter<float>(Category, "Interp Offset", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> InterpolationTimeScale = new ProfilerCounter<float>(Category, "Interp Time Scale", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> InterpolationMultiplier = new ProfilerCounter<float>(Category, "Interp Multiplier", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> InterpolationUncertainty = new ProfilerCounter<float>(Category, "Interp Uncertainty", ProfilerMarkerDataUnit.Undefined);

    public static readonly ProfilerCounter<int> InputSize = new ProfilerCounter<int>(Category, "Client Input Size", ProfilerMarkerDataUnit.Bytes);
    public static readonly ProfilerCounter<int> InputQueue = new ProfilerCounter<int>(Category, "Client Input Queue", ProfilerMarkerDataUnit.Count);

    public static readonly ProfilerCounter<int> WorldSnapshotSize = new ProfilerCounter<int>(Category, "Client Snapshot Size", ProfilerMarkerDataUnit.Bytes);
    public static readonly ProfilerCounter<int> Resimulations = new ProfilerCounter<int>(Category, "Client Resims", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> RoundTripTime = new ProfilerCounter<float>(Category, "Client RTT", ProfilerMarkerDataUnit.Count);

    public static readonly ProfilerCounterValue<int> RpcIn = new ProfilerCounterValue<int>(Category, "RPCs In", ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);
    public static readonly ProfilerCounterValue<int> RpcOut = new ProfilerCounterValue<int>(Category, "RPCs Out", ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

    public static readonly ProfilerCounter<float> SimulationTimeScale = new ProfilerCounter<float>(Category, "Simulation Time Scale", ProfilerMarkerDataUnit.Count);

    public static readonly ProfilerCounter<float> InputOffset = new ProfilerCounter<float>(Category, "Input Offset", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> InputOffsetDeviation = new ProfilerCounter<float>(Category, "Input Offset Dev", ProfilerMarkerDataUnit.Count);

    public static readonly ProfilerCounter<float> InputRecvDelta = new ProfilerCounter<float>(Category, "Input Recv Delta", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> InputRecvDeltaDeviation = new ProfilerCounter<float>(Category, "Input Recv Delta Dev", ProfilerMarkerDataUnit.Count);
  }
#endif
}

#endregion


#region Assets/Photon/Fusion/Runtime/FusionRuntimeCheck.cs

namespace Fusion {
  using UnityEngine;

  static class FusionRuntimeCheck {

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RuntimeCheck() {
      RuntimeUnityFlagsSetup.Check_ENABLE_IL2CPP();
      RuntimeUnityFlagsSetup.Check_ENABLE_MONO();

      RuntimeUnityFlagsSetup.Check_UNITY_EDITOR();
      RuntimeUnityFlagsSetup.Check_UNITY_GAMECORE();
      RuntimeUnityFlagsSetup.Check_UNITY_SWITCH();
      RuntimeUnityFlagsSetup.Check_UNITY_WEBGL();
      RuntimeUnityFlagsSetup.Check_UNITY_XBOXONE();

      RuntimeUnityFlagsSetup.Check_NETFX_CORE();
      RuntimeUnityFlagsSetup.Check_NET_4_6();
      RuntimeUnityFlagsSetup.Check_NET_STANDARD_2_0();

      RuntimeUnityFlagsSetup.Check_UNITY_2019_4_OR_NEWER();
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Runtime/FusionStats.Controls.cs

namespace Fusion
{
  using StatsInternal;
  using UnityEditor;

  public partial class FusionStats
  {

    const int   SCREEN_SCALE_W = 1080;
    const int   SCREEN_SCALE_H = 1080;
    const float TEXT_MARGIN    = 0.25f;
    const float TITLE_HEIGHT   = 20f;
    const int   MARGIN         = FusionStatsUtilities.MARGIN;
    const int   PAD            = FusionStatsUtilities.PAD;

    const string PLAY_TEXT = "PLAY";
    const string PAUS_TEXT = "PAUSE";
    const string SHOW_TEXT = "SHOW";
    const string HIDE_TEXT = "HIDE";
    const string CLER_TEXT = "CLEAR";
    const string CNVS_TEXT = "CANVAS";
    const string CLSE_TEXT = "CLOSE";

    const string PLAY_ICON = "\u25ba";
    const string PAUS_ICON = "\u05f0";
    const string HIDE_ICON = "\u25bc";
    const string SHOW_ICON = "\u25b2";
    const string CLER_ICON = "\u1d13";
    const string CNVS_ICON = "\ufb26"; //"\u2261";
    const string CLSE_ICON = "x";

    void InitializeControls() {
      // Listener connections are not retained with serialization and always need to be connected at startup.
      // Remove listeners in case this is a copy of a runtime generated graph.
      _togglButton?.onClick.RemoveListener(Toggle);
      _canvsButton?.onClick.RemoveListener(ToggleCanvasType);
      _clearButton?.onClick.RemoveListener(Clear);
      _pauseButton?.onClick.RemoveListener(Pause);
      _closeButton?.onClick.RemoveListener(Close);
      _titleButton?.onClick.RemoveListener(PingSelectFusionStats);
      _objctButton?.onClick.RemoveListener(PingSelectObject);

      _togglButton?.onClick.AddListener(Toggle);
      _canvsButton?.onClick.AddListener(ToggleCanvasType);
      _clearButton?.onClick.AddListener(Clear);
      _pauseButton?.onClick.AddListener(Pause);
      _closeButton?.onClick.AddListener(Close);
      _titleButton?.onClick.AddListener(PingSelectFusionStats);
      _objctButton?.onClick.AddListener(PingSelectObject);
    }
    
        void Pause() {
      if (_runner && _runner.IsRunning) {
        _paused = !_paused;

        var icon = _paused ? PLAY_ICON : PAUS_ICON;
        var label = _paused ? PLAY_TEXT : PAUS_TEXT;
        _pauseIcon.text = icon;
        _pauseLabel.text = label;

        // Pause for all SimStats tied to this runner if all related FusionStats are paused.
        if (_statsForRunnerLookup.TryGetValue(_runner, out var stats)) {

          // bool statsAreBeingUsed = false;
          foreach (var stat in stats) {
            if (stat._paused == false) {
              // statsAreBeingUsed = true;
              break;
            }
          }
          // Pause in 2.0 should probably be removed
          // _runner.Simulation.Stats.Pause(statsAreBeingUsed == false);
        }
      }
    }

    void Toggle() {
      _hidden = !_hidden;

      _togglIcon.text = _hidden ? SHOW_ICON : HIDE_ICON;
      _togglLabel.text = _hidden ? SHOW_TEXT : HIDE_TEXT;

      _statsPanelRT.gameObject.SetActive(!_hidden);

      for (int i = 0; i < _simGraphs.Length; ++i) {
        var graph = _simGraphs[i];
        if (graph) {
          _simGraphs[i].gameObject.SetActive(!_hidden && ((long)1 << i & _includedSimStats.Mask) != 0);
        }
      }
      for (int i = 0; i < _objGraphs.Length; ++i) {
        var graph = _objGraphs[i];
        if (graph) {
          _objGraphs[i].gameObject.SetActive(!_hidden && ((long)1 << i & _includedObjStats.Mask) != 0);
        }
      }
      for (int i = 0; i < _netGraphs.Length; ++i) {
        var graph = _netGraphs[i];
        if (graph) {
          _netGraphs[i].gameObject.SetActive(!_hidden && ((long)1 << i & _includedNetStats.Mask) != 0);
        }
      }
    }

    void Clear() {
      if (_runner && _runner.IsRunning) {
        // Clear in 2.0 likely needs to be removed
        // _runner.Simulation.Stats.Clear();
      }

      for (int i = 0; i < _simGraphs.Length; ++i) {
        var graph = _simGraphs[i];
        if (graph) {
          _simGraphs[i].Clear();
        }
      }
      for (int i = 0; i < _objGraphs.Length; ++i) {
        var graph = _objGraphs[i];
        if (graph) {
          _objGraphs[i].Clear();
        }
      }
      for (int i = 0; i < _netGraphs.Length; ++i) {
        var graph = _netGraphs[i];
        if (graph) {
          _netGraphs[i].Clear();
        }
      }
    }

    void ToggleCanvasType() {
#if UNITY_EDITOR
      UnityEditor.EditorGUIUtility.PingObject(gameObject);
      if (Selection.activeGameObject == null) {
        Selection.activeGameObject = gameObject;
      }
#endif
      _canvasType = (_canvasType == StatCanvasTypes.GameObject) ? StatCanvasTypes.Overlay : StatCanvasTypes.GameObject;
      //_canvas.enabled = false;
      _layoutDirty = 3;
      CalculateLayout();
    }

    void Close() {
      Destroy(this.gameObject);
    }

    void PingSelectObject() {

#if UNITY_EDITOR
      var obj = Object;
      if (obj) {
        EditorGUIUtility.PingObject(Object.gameObject);
        Selection.activeGameObject = Object.gameObject;
      }
#endif
    }

    void PingSelectFusionStats() {

#if UNITY_EDITOR
      EditorGUIUtility.PingObject(gameObject);
      Selection.activeGameObject = gameObject;
#endif
    }

  }
}

#endregion


#region Assets/Photon/Fusion/Runtime/FusionStats.Create.cs

namespace Fusion
{
  using System;
  using System.Collections.Generic;
  using StatsInternal;
  using UnityEngine;

#if UNITY_EDITOR
  using UnityEditor;
#endif
  
  public partial class FusionStats {
    
#if UNITY_EDITOR

    [MenuItem("Tools/Fusion/Scene/Add Fusion Stats", false, 1000)]
    [MenuItem("GameObject/Fusion/Scene/Add Fusion Stats", false, 0)]
    public static void AddFusionStatsToScene() {

      var selected = Selection.activeGameObject;

      if (selected && PrefabUtility.IsPartOfPrefabAsset(selected)) {
        Debug.LogWarning("Open prefabs before running 'Add Fusion Stats' on them.");
        return;
      }

      var fs = new GameObject("FusionStats");

      if (selected) {
        fs.transform.SetParent(Selection.activeGameObject.transform);
      }

      fs.transform.localPosition = default;
      fs.transform.localRotation = default;
      fs.transform.localScale    = Vector3.one;

      fs.AddComponent<FusionStatsBillboard>();
      fs.AddComponent<FusionStats>();
      EditorGUIUtility.PingObject(fs.gameObject);
      Selection.activeGameObject = fs.gameObject;
    }
#endif
    
    [SerializeField][HideInInspector] FusionStatsGraph[]     _simGraphs;
    [SerializeField][HideInInspector] FusionStatsGraph[]     _objGraphs;
    [SerializeField][HideInInspector] FusionStatsGraph[]     _netGraphs;
    [SerializeField][HideInInspector] FusionStatsObjectIds   _objIds;
    [NonSerialized]                   List<IFusionStatsView> _foundViews;
    [NonSerialized]                   List<FusionStatsGraph> _foundGraphs;

    [SerializeField][HideInInspector] UnityEngine.UI.Text _titleText;

    [SerializeField][HideInInspector] UnityEngine.UI.Text _clearIcon;
    [SerializeField][HideInInspector] UnityEngine.UI.Text _pauseIcon;
    [SerializeField][HideInInspector] UnityEngine.UI.Text _togglIcon;
    [SerializeField][HideInInspector] UnityEngine.UI.Text _closeIcon;
    [SerializeField][HideInInspector] UnityEngine.UI.Text _canvsIcon;

    [SerializeField][HideInInspector] UnityEngine.UI.Text _clearLabel;
    [SerializeField][HideInInspector] UnityEngine.UI.Text _pauseLabel;
    [SerializeField][HideInInspector] UnityEngine.UI.Text _togglLabel;
    [SerializeField][HideInInspector] UnityEngine.UI.Text _closeLabel;
    [SerializeField][HideInInspector] UnityEngine.UI.Text _canvsLabel;
    [SerializeField][HideInInspector] UnityEngine.UI.Text _objectNameText;

    [SerializeField][HideInInspector] UnityEngine.UI.GridLayoutGroup _graphGridLayoutGroup;

    [SerializeField][HideInInspector] Canvas _canvas;
    [SerializeField][HideInInspector] RectTransform _canvasRT;
    [SerializeField][HideInInspector] RectTransform _rootPanelRT;
    [SerializeField][HideInInspector] RectTransform _headerRT;
    [SerializeField][HideInInspector] RectTransform _statsPanelRT;
    [SerializeField][HideInInspector] RectTransform _graphsLayoutRT;
    [SerializeField][HideInInspector] RectTransform _titleRT;
    [SerializeField][HideInInspector] RectTransform _buttonsRT;
    [SerializeField][HideInInspector] RectTransform _objectTitlePanelRT;
    [SerializeField][HideInInspector] RectTransform _objectIdsGroupRT;
    [SerializeField][HideInInspector] RectTransform _objectMetersPanelRT;

    [SerializeField][HideInInspector] UnityEngine.UI.Button _titleButton;
    [SerializeField][HideInInspector] UnityEngine.UI.Button _objctButton;
    [SerializeField][HideInInspector] UnityEngine.UI.Button _clearButton;
    [SerializeField][HideInInspector] UnityEngine.UI.Button _togglButton;
    [SerializeField][HideInInspector] UnityEngine.UI.Button _pauseButton;
    [SerializeField][HideInInspector] UnityEngine.UI.Button _closeButton;
    [SerializeField][HideInInspector] UnityEngine.UI.Button _canvsButton;
    
    /// <summary>
    /// Creates a new GameObject with a <see cref="FusionStats"/> component, attaches it to any supplied parent, and generates Canvas/Graphs.
    /// </summary>
    /// <param name="runner"></param>
    /// <param name="parent">Generated FusionStats component and GameObject will be added as a child of this transform.</param>
    /// <param name="objectLayout">Uses a predefined position.</param>
    /// <param name="netStatsMask">The network stats to be enabled. If left null, default statistics will be used.</param>
    /// <param name="simStatsMask">The simulation stats to be enabled. If left null, default statistics will be used.</param>
    public static FusionStats Create(Transform parent = null, NetworkRunner runner = null, DefaultLayouts? screenLayout = null, DefaultLayouts? objectLayout = null/*, Stats.NetStatFlags? netStatsMask = null, Stats.SimStatFlags? simStatsMask = null*/) {

      var go = new GameObject($"{nameof(FusionStats)} {(runner ? runner.name : "null")}");
      FusionStats stats;
      if (parent) {
        go.transform.SetParent(parent);
      }

      stats = go.AddComponent<FusionStats>();

      stats.ResetInternal(null, null, screenLayout);

      stats.SetRunner(runner);

      if (runner != null) {
        stats.AutoDestroy = true;
      }
      return stats;
    }

    bool _graphsAreMissing => _canvasRT == null;
    
    [EditorButton(EditorButtonVisibility.EditMode)]
    [DrawIf(nameof(_graphsAreMissing), Hide = true)]
    void GenerateGraphs() {
      var rootRectTr = gameObject.GetComponent<Transform>();
      _canvasRT            = rootRectTr.CreateRectTransform("Stats Canvas");
      _canvas              = _canvasRT.gameObject.AddComponent<Canvas>();
      _canvas.pixelPerfect = true;
      _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;

      // If the runner has already started, the root FusionStats has been added to the VisNodes registration for the runner,
      // But any generated children GOs here will not. Add the generated components to the visibility system.
      if (Runner && Runner.IsRunning) {
        Runner.AddVisibilityNodes(_canvasRT.gameObject);
      }
      var scaler = _canvasRT.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
      scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(SCREEN_SCALE_W, SCREEN_SCALE_H);
      scaler.matchWidthOrHeight = .4f;

      _canvasRT.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

      _rootPanelRT = _canvasRT
        .CreateRectTransform("Root Panel");

      _headerRT = _rootPanelRT
        .CreateRectTransform("Header Panel")
        .AddCircleSprite(PanelColor);

      _titleRT = _headerRT
        .CreateRectTransform("Runner Title")
        .SetAnchors(0.0f, 1.0f, 0.75f, 1.0f)
        .SetOffsets(MARGIN, -MARGIN, 0.0f, -MARGIN);

      _titleButton = _titleRT.gameObject.AddComponent<UnityEngine.UI.Button>();
      _titleText = _titleRT.AddText(_runner ? _runner.name : "Disconnected", TextAnchor.UpperCenter, _fontColor, LabelFont);
      _titleText.raycastTarget = true;

      // Buttons
      _buttonsRT = _headerRT
        .CreateRectTransform("Buttons")
        .SetAnchors(0.0f, 1.0f, 0.0f, 0.75f)
        .SetOffsets(MARGIN, -MARGIN, MARGIN, 0);

      var buttonsGrid = _buttonsRT.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
      buttonsGrid.childControlHeight = true;
      buttonsGrid.childControlWidth = true;
      buttonsGrid.spacing = MARGIN;
      _buttonsRT.MakeButton(ref _togglButton, HIDE_ICON, HIDE_TEXT, LabelFont, out _togglIcon, out _togglLabel, Toggle);
      _buttonsRT.MakeButton(ref _canvsButton, CNVS_ICON, CNVS_TEXT, LabelFont, out _canvsIcon, out _canvsLabel, ToggleCanvasType);
      _buttonsRT.MakeButton(ref _pauseButton, PAUS_ICON, PAUS_TEXT, LabelFont, out _pauseIcon, out _pauseLabel, Pause);
      _buttonsRT.MakeButton(ref _clearButton, CLER_ICON, CLER_TEXT, LabelFont, out _clearIcon, out _clearLabel, Clear);
      _buttonsRT.MakeButton(ref _closeButton, CLSE_ICON, CLSE_TEXT, LabelFont, out _closeIcon, out _closeLabel, Close);

      // Minor tweak to foldout arrow icon, since its too tall.
      _togglIcon.rectTransform.anchorMax = new Vector2(1, 0.85f);

      // Stats stack

      _statsPanelRT = _rootPanelRT
        .CreateRectTransform("Stats Panel")
        .AddCircleSprite(PanelColor);

      // Object Name, IDs and Meters

      _objectTitlePanelRT = _statsPanelRT
        .CreateRectTransform("Object Name Panel")
        .ExpandTopAnchor(MARGIN)
        .AddCircleSprite(_objDataBackColor);

      _objctButton = _objectTitlePanelRT.gameObject.AddComponent<UnityEngine.UI.Button>();

      var objectTitleRT = _objectTitlePanelRT
        .CreateRectTransform("Object Name")
        .SetAnchors(0.0f, 1.0f, 0.15f, 0.85f)
        .SetOffsets(PAD, -PAD, 0, 0);

      _objectNameText = objectTitleRT.AddText("Object Name", TextAnchor.MiddleCenter, _fontColor, LabelFont);
      _objectNameText.alignByGeometry = false;
      _objectNameText.raycastTarget = false;

      _objIds = FusionStatsObjectIds.Create(_statsPanelRT, this, out _objectIdsGroupRT);

      _objectMetersPanelRT = _statsPanelRT
        .CreateRectTransform("Object Meters Layout")
        .ExpandTopAnchor(MARGIN)
        .AddVerticalLayoutGroup(MARGIN);

      // These are placeholders to connect stats 2.0 to old 1.0 enums
      const int INDEX_OF_OBJ_BANDWIDTH = 0;
      const int INDEX_OF_OBJ_RPC_COUNT = 1;

      FusionStatsMeterBar.Create(_objectMetersPanelRT, this, StatSourceTypes.NetworkObject, INDEX_OF_OBJ_BANDWIDTH, 15, 30);
      FusionStatsMeterBar.Create(_objectMetersPanelRT, this, StatSourceTypes.NetworkObject, INDEX_OF_OBJ_RPC_COUNT, 3,  6);

      // Graphs
      _graphsLayoutRT = _statsPanelRT
        .CreateRectTransform("Graphs Layout")
        .ExpandAnchor()
        .SetOffsets(MARGIN, 0, 0, 0);

      //.AddGridlLayoutGroup(MRGN);
      _graphGridLayoutGroup = _graphsLayoutRT.AddGridlLayoutGroup(MARGIN);

      int objTypeCount = StatSourceTypes.NetworkObject.Lookup().LongNames.Length;
      _objGraphs = new FusionStatsGraph[objTypeCount];
      for (int i = 0; i < objTypeCount; ++i) {
        if (InitializeAllGraphs == false) {
          var statFlag = (long)1 << i;
          if ((statFlag & _includedObjStats.Mask) == 0) {
            continue;
          }
        }
        CreateGraph(StatSourceTypes.NetworkObject, i, _graphsLayoutRT);
      }

      int netConnTypeCount = StatSourceTypes.NetConnection.Lookup().LongNames.Length;
      _netGraphs = new FusionStatsGraph[netConnTypeCount];
      for (int i = 0; i < netConnTypeCount; ++i) {
        if (InitializeAllGraphs == false) {
          var statFlag = (long)1 << i;
          if ((statFlag & _includedNetStats.Mask) == 0) {
            continue;
          }
        }
        CreateGraph(StatSourceTypes.NetConnection, i, _graphsLayoutRT);
      }

      int simTypeCount = StatSourceTypes.Simulation.Lookup().LongNames.Length;
      _simGraphs = new FusionStatsGraph[simTypeCount];
      for (int i = 0; i < simTypeCount; ++i) {
        if (InitializeAllGraphs == false) {
          var statFlag = ((long)1 << i);
          if ((statFlag & _includedSimStats.Mask) == 0) {
            continue;
          }
        }
        CreateGraph(StatSourceTypes.Simulation, i, _graphsLayoutRT);
      }

      _activeDirty = true;
      _layoutDirty = 2;
    }

    
      public FusionStatsGraph CreateGraph(StatSourceTypes type, int statId, RectTransform parentRT) {

        var fg = FusionStatsGraph.Create(this, type, statId, parentRT);

        if (type == StatSourceTypes.Simulation) {
          _simGraphs[statId] = fg;
          if ((_includedSimStats.Mask & ((long)1 << statId)) == 0) {
            fg.gameObject.SetActive(false);
          }
        } else if (type == StatSourceTypes.NetworkObject) {
          _objGraphs[statId] = fg;
          if ((_includedObjStats.Mask & ((long)1 << statId)) == 0) {
            fg.gameObject.SetActive(false);
          }
        } else {
          _netGraphs[statId] = fg;
          if ((_includedNetStats.Mask & ((long)1 << statId)) == 0) {
            fg.gameObject.SetActive(false);
          }
        }

        return fg;
      }

    }
}


#endregion


#region Assets/Photon/Fusion/Runtime/FusionStats.Layout.cs

namespace Fusion
{
  using System;
  using System.Collections.Generic;
  using StatsInternal;
  using UnityEngine;

  public partial class FusionStats
  {

    void UpdateTitle() {
      var runnername = _runner ? _runner.name : "Disconnected";
      if (_titleText) {
        _titleText.text = runnername;
      }
    }
    
    void DirtyLayout(int minimumRefreshes = 1) {
      if (_layoutDirty < minimumRefreshes) {
        _layoutDirty = minimumRefreshes;
      }
    }
    
    float _lastLayoutUpdate;

    void CalculateLayout() {

      if (_rootPanelRT == null || _graphsLayoutRT == null) {
        return;
      }

      if (_foundGraphs == null) {
        _foundGraphs = new List<FusionStatsGraph>(_graphsLayoutRT.GetComponentsInChildren<FusionStatsGraph>(false));
      } else {
        GetComponentsInChildren(false, _foundGraphs);
      }

      // Don't count multiple executions of CalculateLayout in the same Update as reducing the dirty count.
      // _layoutDirty can be set to values greater than 1 to force a recalculate for several consecutive Updates.
      var time = Time.time;

      if (_lastLayoutUpdate < time) {
        _layoutDirty--;
        _lastLayoutUpdate = time;

      }

#if UNITY_EDITOR
      if (Application.isPlaying == false && _layoutDirty > 0) {
        UnityEditor.EditorApplication.delayCall -= CalculateLayout;
        UnityEditor.EditorApplication.delayCall += CalculateLayout;
      }
#endif

      if (_layoutDirty <= 0 && _canvas.enabled == false) {
        //_canvas.enabled = true;
      }

      if (_rootPanelRT) {

        var maxHeaderHeight = Math.Min(_maxHeaderHeight, _rootPanelRT.rect.width / 4);

        if (_canvasType == StatCanvasTypes.GameObject) {
          _canvas.renderMode = RenderMode.WorldSpace;
          var scale = CanvasScale / SCREEN_SCALE_H; //  (1f / SCREEN_SCALE_H) * Scale;
          _canvasRT.localScale = new Vector3(scale, scale, scale);
          _canvasRT.sizeDelta = new Vector2(1024, 1024);
          _canvasRT.localPosition = new Vector3(0, 0, CanvasDistance);

          // TODO: Cache this
          if (_canvasRT.GetComponent<FusionStatsBillboard>() == false) {
            _canvasRT.localRotation = default;
          }
        } else {
          _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        _objectTitlePanelRT.gameObject.SetActive(_enableObjectStats);
        _objectIdsGroupRT.gameObject.SetActive(_enableObjectStats);
        _objectMetersPanelRT.gameObject.SetActive(_enableObjectStats);

        Vector2 icoMinAnchor;

        if (_showButtonLabels) {
          icoMinAnchor = new Vector2(0.0f, FusionStatsUtilities.BTTN_LBL_NORM_HGHT * .5f);
        } else {
          icoMinAnchor = new Vector2(0.0f, 0.0f);
        }

        _togglIcon.rectTransform.anchorMin = icoMinAnchor + new Vector2(0, .15f);
        _canvsIcon.rectTransform.anchorMin = icoMinAnchor;
        _clearIcon.rectTransform.anchorMin = icoMinAnchor;
        _pauseIcon.rectTransform.anchorMin = icoMinAnchor;
        _closeIcon.rectTransform.anchorMin = icoMinAnchor;

        _togglLabel.gameObject.SetActive(_showButtonLabels);
        _canvsLabel.gameObject.SetActive(_showButtonLabels);
        _clearLabel.gameObject.SetActive(_showButtonLabels);
        _pauseLabel.gameObject.SetActive(_showButtonLabels);
        _closeLabel.gameObject.SetActive(_showButtonLabels);

        var rect = CurrentRect;

        _rootPanelRT.anchorMax = new Vector2(rect.xMax, rect.yMax);
        _rootPanelRT.anchorMin = new Vector2(rect.xMin, rect.yMin);
        _rootPanelRT.sizeDelta = new Vector2(0.0f, 0.0f);
        _rootPanelRT.pivot = new Vector2(0.5f, 0.5f);
        _rootPanelRT.anchoredPosition3D = default;

        _headerRT.anchorMin = new Vector2(0.0f, 1);
        _headerRT.anchorMax = new Vector2(1.0f, 1);
        _headerRT.pivot = new Vector2(0.5f, 1);
        _headerRT.anchoredPosition3D = default;
        _headerRT.sizeDelta = new Vector2(0, /*TITLE_HEIGHT +*/ maxHeaderHeight);

        _objectTitlePanelRT.offsetMax = new Vector2(-MARGIN, -MARGIN);
        _objectTitlePanelRT.offsetMin = new Vector2(MARGIN, -(ObjectTitleHeight));
        _objectIdsGroupRT.offsetMax = new Vector2(-MARGIN, -(ObjectTitleHeight + MARGIN));
        _objectIdsGroupRT.offsetMin = new Vector2(MARGIN, -(ObjectTitleHeight + ObjectIdsHeight));
        _objectMetersPanelRT.offsetMax = new Vector2(-MARGIN, -(ObjectTitleHeight + ObjectIdsHeight + MARGIN));
        _objectMetersPanelRT.offsetMin = new Vector2(MARGIN, -(ObjectTitleHeight + ObjectIdsHeight + ObjectMetersHeight));

        // Disable object sections that have been minimized to 0
        _objectTitlePanelRT.gameObject.SetActive(EnableObjectStats && ObjectTitleHeight > 0);
        _objectIdsGroupRT.gameObject.SetActive(EnableObjectStats && ObjectIdsHeight > 0);
        _objectMetersPanelRT.gameObject.SetActive(EnableObjectStats && ObjectMetersHeight > 0);

        _statsPanelRT.ExpandAnchor().SetOffsets(0, 0, 0, -(/*TITLE_HEIGHT + */maxHeaderHeight));

        if (_enableObjectStats && _statsPanelRT.rect.height < (ObjectTitleHeight + ObjectIdsHeight + ObjectMetersHeight)) {
          _statsPanelRT.offsetMin = new Vector2(0.0f, _statsPanelRT.rect.height - (ObjectTitleHeight + ObjectIdsHeight + ObjectMetersHeight + MARGIN));
        }

        var graphColCount = GraphColumnCount > 0 ? GraphColumnCount : (int)(_graphsLayoutRT.rect.width / (_graphMaxWidth + MARGIN));
        if (graphColCount < 1) {
          graphColCount = 1;
        }

        var graphRowCount = (int)Math.Ceiling((double)_foundGraphs.Count / graphColCount);
        if (graphRowCount < 1) {
          graphRowCount = 1;
        }

        if (graphRowCount == 1) {
          graphColCount = _foundGraphs.Count;
        }

        _graphGridLayoutGroup.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        _graphGridLayoutGroup.constraintCount = graphColCount;

        var cellwidth = _graphsLayoutRT.rect.width / graphColCount - MARGIN;
        var cellheight = _graphsLayoutRT.rect.height / graphRowCount - (/*(graphRowCount - 1) **/ MARGIN);

        _graphGridLayoutGroup.cellSize = new Vector2(cellwidth, cellheight);
        _graphsLayoutRT.offsetMax = new Vector2(0, _enableObjectStats ? -(ObjectTitleHeight + ObjectIdsHeight + ObjectMetersHeight + MARGIN) : -MARGIN);


        if (_foundViews == null) {
          _foundViews = new List<IFusionStatsView>(GetComponentsInChildren<IFusionStatsView>(false));
        } else {
          GetComponentsInChildren(false, _foundViews);
        }

        if (_objGraphs != null) {
          // enabled/disable any object graphs based on _enabledObjectStats setting
          foreach (var objGraph in _objGraphs) {
            if (objGraph) {
              objGraph.gameObject.SetActive((_includedObjStats.Mask & ((long)1 << objGraph.StatId)) != 0 && _enableObjectStats);
            }
          }
        }

        for (int i = 0; i < _foundViews.Count; ++i) {
          var graph = _foundViews[i];
          if (graph == null || graph.isActiveAndEnabled == false) {
            continue;
          }
          graph.CalculateLayout();
          graph.transform.localRotation = default;
          graph.transform.localScale = new Vector3(1, 1, 1);
        }
      }
    }

    void ApplyDefaultLayout(DefaultLayouts defaults, StatCanvasTypes? applyForCanvasType = null) {
      bool applyToGO = applyForCanvasType.HasValue == false || applyForCanvasType.Value == StatCanvasTypes.GameObject;
      bool applyToOL = applyForCanvasType.HasValue == false || applyForCanvasType.Value == StatCanvasTypes.Overlay;

      if (defaults == DefaultLayouts.Custom) {
        return;
      }

      Rect screenrect;
      Rect objectrect;
      bool isTall;
#if UNITY_EDITOR
      var currentRes = UnityEditor.Handles.GetMainGameViewSize();
      isTall = (currentRes.y > currentRes.x);
#else
    isTall = Screen.height > Screen.width;
#endif

      switch (defaults) {
        case DefaultLayouts.Left: {
            objectrect = Rect.MinMaxRect(0.0f, 0.0f, 0.3f, 1.0f);
            screenrect = objectrect;
            break;
          }
        case DefaultLayouts.Right: {
            objectrect = Rect.MinMaxRect(0.7f, 0.0f, 1.0f, 1.0f);
            screenrect = objectrect;
            break;
          }
        case DefaultLayouts.UpperLeft: {
            objectrect = Rect.MinMaxRect(0.0f, 0.5f, 0.3f, 1.0f);
            screenrect = isTall ? Rect.MinMaxRect(0.0f, 0.7f, 0.3f, 1.0f) : objectrect;
            break;
          }
        case DefaultLayouts.UpperRight: {
            objectrect = Rect.MinMaxRect(0.7f, 0.5f, 1.0f, 1.0f);
            screenrect = isTall ? Rect.MinMaxRect(0.7f, 0.7f, 1.0f, 1.0f) : objectrect;
            break;
          }
        case DefaultLayouts.Full: {
            objectrect = Rect.MinMaxRect(0.0f, 0.0f, 1.0f, 1.0f);
            screenrect = objectrect;
            break;
          }
        default: {
            objectrect = Rect.MinMaxRect(0.0f, 0.5f, 0.3f, 1.0f);
            screenrect = objectrect;
            break;
          }
      }

      if (applyToGO) {
        GameObjectRect = objectrect;
      }
      if (applyToOL) {
        OverlayRect = screenrect;
      }

      _layoutDirty += 1;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Runtime/FusionStats.Statics.cs


namespace Fusion
{
  using System.Collections.Generic;
  using UnityEngine;



  public partial class FusionStats
    {



      [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
      static void ResetStatics() {
        _statsForRunnerLookup.Clear();
        _activeGuids.Clear();
        _newInputSystemFound = null;
      }
      
      // Lookup for all FusionStats associated with active runners.
      static Dictionary<NetworkRunner, List<FusionStats>> _statsForRunnerLookup = new Dictionary<NetworkRunner, List<FusionStats>>();

      // Record of active SimStats, used to prevent more than one _guid version from existing (in the case of SimStats existing in a scene that gets cloned in Multi-Peer).
      static Dictionary<string, FusionStats> _activeGuids = new Dictionary<string, FusionStats>();

      public static NetworkId MonitoredNetworkObjectId { get; set; }

      /// <summary>
      /// Any FusionStats instance with NetworkObject stats enabled, will use this Object when that FusionStats.Object is null.
      /// </summary>
      /// <param name="no"></param>
      public static void SetMonitoredNetworkObject(NetworkObject no) {
        if (no) {
          MonitoredNetworkObjectId = no.Id;
        } else {
          MonitoredNetworkObjectId = new NetworkId();
        }
      }
    }
}


#endregion


#region Assets/Photon/Fusion/Runtime/FusionStatsExtensions.cs


namespace Fusion.StatsInternal
{
  using System.Collections.Generic;
  using UnityEngine;
  using System;
  using System.ComponentModel;
  using System.Reflection;
  
  // [Flags]
  // public enum FusionGraphVisualization {
  //   [Description("Auto")]
  //   Auto,
  //   [Description("Continuous Tick")]
  //   ContinuousTick = 1,
  //   [Description("Intermittent Tick")]
  //   IntermittentTick = 2,
  //   [Description("Intermittent Time")]
  //   IntermittentTime = 4,
  //   [Description("Value Histogram")]
  //   ValueHistogram = 8,
  //   [Description("Count Histogram")]
  //   CountHistogram = 16,
  // }
  //
  /// <summary>
  /// Engine sources for Samples.
  /// </summary>
  public enum StatSourceTypes {
    Simulation,
    NetworkObject,
    NetConnection,
    Behaviour,
  }
  
  // [Flags]
  // public enum StatsPer {
  //   Individual = 1,
  //   Tick       = 2,
  //   Second     = 4,
  // }

  [Flags]
  public enum StatFlags {
    ValidOnServer         = 1 << 0,
    ValidOnClient         = 1 << 1,
    ValidInShared         = 1 << 2,
    ValidOnStateAuthority = 1 << 5,
    ValidForBuildType     = 1 << 6,
  }
    public static class FusionStatsExtensions
    {
      public struct FieldMaskData {
        public GUIContent[]         LongNames;
        public GUIContent[]         ShortNames;
        public StatsMetaAttribute[] Metas;
        public FieldInfo[]          FieldInfos;
        public Mask256              DefaultMask;
      }

      private static Dictionary<StatSourceTypes, FieldMaskData> s_lookup = new ();

      public static FieldMaskData Lookup(this Type type) {
        if (type == typeof(SimulationStats)) return Lookup(StatSourceTypes.Simulation);
        if (type == typeof(BehaviourStats)) return Lookup(StatSourceTypes.Behaviour);
        if (type == typeof(SimulationConnectionStats)) return Lookup(StatSourceTypes.NetConnection);
        if (type == typeof(NetworkObjectStats)) return Lookup(StatSourceTypes.NetworkObject);
        return default;
      }

      public static Mask256 GetDefaults(this Type type) {
        return type.Lookup().DefaultMask;
      }

      /// <summary>
      /// Get the cached Long Name for the stat source and type.
      /// </summary>
      public static string GetLongName(this StatSourceTypes type, int statId) {
        var data = Lookup(type);
        return data.LongNames[statId].text;
      }
      
      private static string GetRuntimeNicifiedName(this StatsMetaAttribute meta, FieldInfo fieldInfo) {
        if (meta.Name != null) {
          return meta.Name;
        }
        // TODO: Make this return formatting closer to Unity's Object.Nicify
        return System.Text.RegularExpressions.Regex.Replace(fieldInfo.Name, "(?<=[a-z])([A-Z])", " $1").Trim();
      }
      
      public static FieldMaskData Lookup(this StatSourceTypes type) {
        if (s_lookup.TryGetValue(type, out var fieldMaskData)) {
          return fieldMaskData;
        }
        var fields     = type.GetStateSourceType().GetFields();
        var gcLong     = new GUIContent[fields.Length];
        var gcShort    = new GUIContent[fields.Length];
        var metas      = new StatsMetaAttribute[fields.Length];
        var defaults   = new Mask256();

        for (int i = 0; i < fields.Length; ++i) {
          metas[i]          = fields[i].GetCustomAttribute<StatsMetaAttribute>();
          var longName = metas[i].GetRuntimeNicifiedName(fields[i]);
          gcLong[i]  = new GUIContent(longName);
          gcShort[i] = new GUIContent(metas[i].ShortName);
          if (metas[i].DefaultEnabled) {
            defaults.SetBit(i, true);
          }
        }
        var data = new FieldMaskData() { LongNames = gcLong, ShortNames = gcShort, Metas = metas, FieldInfos = fields, DefaultMask = defaults};

        s_lookup.Add(type, data);
        return data;
      }

      private static Type GetStateSourceType(this StatSourceTypes statSourceType) {
        switch (statSourceType) {
          case StatSourceTypes.Behaviour: return typeof(BehaviourStats);
          case StatSourceTypes.NetConnection: return typeof(SimulationConnectionStats);
          case StatSourceTypes.Simulation: return typeof(SimulationStats);
          case StatSourceTypes.NetworkObject: return typeof(NetworkObjectStats);
        }
        Debug.LogError($"Stat Type not found.");
        return default;
      }
      
      public static (StatsMetaAttribute meta, FieldInfo fieldInfo) GetDescription(this StatSourceTypes statSource, int statId) {
        var datas = Lookup(statSource);
        return (datas.Metas[statId], datas.FieldInfos[statId]);
      }
    }
}


#endregion


#region Assets/Photon/Fusion/Runtime/FusionStatsGraphBase.cs

namespace Fusion {
  using System.Reflection;
  using UnityEngine;
  using UI = UnityEngine.UI;
  using StatsInternal;
  using UnityEngine.Serialization;

  [ScriptHelp(BackColor = ScriptHeaderBackColor.Olive)]
  public abstract class FusionStatsGraphBase : Fusion.Behaviour, IFusionStatsView {
    
    protected const int PAD = FusionStatsUtilities.PAD;
    protected const int MRGN = FusionStatsUtilities.MARGIN;
    // protected const int MAX_FONT_SIZE_WITH_GRAPH = 24;

    [SerializeField] [HideInInspector] protected UI.Text LabelTitle;
    [SerializeField] [HideInInspector] protected UI.Image BackImage;

    /// <summary>
    /// Which section of the Fusion engine is being monitored. In combination with StatId, this selects the stat being monitored.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    protected StatSourceTypes _statSourceType;
    public StatSourceTypes StateAuthorityType {
      get => _statSourceType;
      set {
        _statSourceType = value;
        TryConnect();
      }
    }

    /// <summary>
    /// The specific stat being monitored.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    //[DisplayAsEnum(nameof(CastToStatType))]
    protected int _statId = -1;
    public int StatId {
      get => _statId;
      set {
        _statId = value;
        TryConnect();
      }
    }

    // [FormerlySerializedAs("StatsPerDefault")] [InlineHelp]
    // public StatAveraging StatsAvergingDefault;
  
    [InlineHelp]
    public float WarnThreshold;

    [InlineHelp]
    public float ErrorThreshold;

    // protected IStatsBuffer _statsBuffer;
    // public IStatsBuffer StatsBuffer {
    //   get {
    //     if (_statsBuffer == null) {
    //       TryConnect();
    //     }
    //     return _statsBuffer;
    //   }
    // }

    protected bool _isOverlay;
    public bool IsOverlay {
      set {
        if (_isOverlay != value) {
          _isOverlay = value;
          CalculateLayout();
          _layoutDirty = _layoutDirty < 1 ? 1 : _layoutDirty;
        }
      }
      get {
        return _isOverlay;
      }
    }

    // Needed for multi-peer, cloned graphs otherwise have default layout.
    private void Start() {
      _layoutDirty = 4;
    }

    protected virtual Color BackColor {
      get {
        if (_statSourceType == StatSourceTypes.Simulation) {
          return FusionStats.SimDataBackColor;
        }
        if (_statSourceType == StatSourceTypes.NetConnection) {
          return FusionStats.NetDataBackColor;
        }
        return FusionStats.ObjDataBackColor;
      }
    }

    // protected Type CastToStatType =>
    //   (_statSourceType == StatSourceTypes.Simulation) ? typeof(Stats.SimStats) :
    //   (_statSourceType == StatSourceTypes.NetConnection) ? typeof(Stats.NetStats) :
    //                                                              typeof(Stats.ObjStats);

    [SerializeField]
    protected FusionStats _fusionStats;

    protected FusionStats FusionStats {
      get {
        if (_fusionStats) 
          return _fusionStats;
        return _fusionStats = GetComponentInParent<FusionStats>();
      }
    }
    
    // protected FusionStats LocateParentFusionStats() {
    //   if (_fusionStats == null) {
    //     _fusionStats = GetComponentInParent<FusionStats>();
    //   }
    //   return _fusionStats;
    // }

    protected int _layoutDirty = 2;

    [SerializeField]
    protected StatAveraging CurrentAveraging;

//    public StatSourceInfo StatSourceInfo;
    protected StatsMetaAttribute _statSourceInfo;

    public StatsMetaAttribute StatSourceInfo {
      get {
        if (_statSourceInfo == null) {
          TryConnect();
        }
        return _statSourceInfo;
      }
    }
    
    private FieldInfo _fieldInfo;

    public FieldInfo FieldInfo {
      get {
        if (_fieldInfo == null) {
          TryConnect();
        }
        return _fieldInfo;
      }
    }
    private object        _statsObject;
    private NetworkObject _previousNetworkObject;

    public object StatsObject {
      get {
         
        if (_statsObject != null && _previousNetworkObject == _fusionStats.Object) {
          return _statsObject;
        }
        
        var runner = FusionStats.Runner;
        if (runner.IsRunning) {
          switch (_statSourceType) {
            case StatSourceTypes.Simulation: {
              runner.TryGetSimulationStats(out var stats);
              return _statsObject = stats;
            }
            case StatSourceTypes.NetworkObject: {
              var no = _fusionStats.Object; // GetComponentInParent<NetworkObject>();
              if (no != _previousNetworkObject) {
                if (no) {
                  _previousNetworkObject = no;
                  runner.TryGetObjectStats(no.Id, out var stats);
                  return _statsObject = stats;
                }
                _previousNetworkObject = null;
                return _statsObject = default;
              }

              return _statsObject;
            }
            case StatSourceTypes.NetConnection: {
              runner.TryGetPlayerStats(FusionStats.PlayerRef, out var stats);
              // if (stats == null)
              //   Debug.LogError($"Failed to get Stats Object for netConnection {_fusionStats.Runner.Mode} {_fusionStats.PlayerRef}");
              return _statsObject = stats;
            }

            // Need to write handling for all other stat types
          }
        }
        Debug.LogError($"Can't get stats object {_statSourceType}");
        return default;

      }
    }

    // Track source values to detect changes in OnValidate.
    [SerializeField]
    [HideInInspector]
    StatSourceTypes _prevStatSourceType;

    [SerializeField]
    [HideInInspector]
    int _prevStatId;

#if UNITY_EDITOR

    protected virtual void OnValidate() {
      if (_statSourceType != _prevStatSourceType || _statId != _prevStatId) {
        WarnThreshold = 0;
        ErrorThreshold = 0;
        _prevStatSourceType = _statSourceType;
        _prevStatId = _statId;
      }
    }
#endif

    public virtual void Initialize() {

    }

    public virtual void CyclePer() {

      switch (CurrentAveraging) {
        case StatAveraging.PerSample:
          // Only include PerSecond if that was the original default handling. Otherwise we assume per second is not a useful graph.
          if (StatSourceInfo.Averaging == StatAveraging.PerSecond) {
            CurrentAveraging = StatAveraging.PerSecond;
          } else {
            CurrentAveraging = StatAveraging.Latest;
          }
          return;
        
        case StatAveraging.PerSecond:
          CurrentAveraging = StatAveraging.Latest;
          return;
        
        case StatAveraging.Latest:
          CurrentAveraging = StatAveraging.RecentPeak;
          return;
        
        case StatAveraging.RecentPeak:
          CurrentAveraging = StatAveraging.Peak;
          return;
        
        case StatAveraging.Peak:
          CurrentAveraging = StatAveraging.PerSample;
          return;
      }
    }

    public abstract void CalculateLayout();

    public abstract void Refresh();

    public void Disconnect() {
      _statsObject = null;
    }
    
    protected virtual bool TryConnect() {

      // Don't try to connect if values are not initialized. (was just added as a component and OnValidate is calling this method).
      if (_statId == -1) {
        return false;
      }
      
      var info = FusionStatsExtensions.GetDescription(_statSourceType, _statId);
      _statSourceInfo = info.meta;
      _fieldInfo      = info.fieldInfo;

      if (WarnThreshold == 0 && ErrorThreshold == 0) {
        WarnThreshold = StatSourceInfo.WarnThreshold;
        ErrorThreshold = StatSourceInfo.ErrorThreshold;
      }

      if (gameObject.activeInHierarchy == false) {
        return false;
      }

      // // Any data connection requires a runner for the statistics source.
      // // TODO: Is this needed still?
      // var runner = FusionStats?.Runner;

      if (BackImage) {
        BackImage.color = BackColor;
      }

      // Update the labels, regardless if a connection can be made.
      if (LabelTitle) {
        ApplyTitleText();
      }

      // If averaging setting is not set yet, get the default.
      if (CurrentAveraging == 0) {
        CurrentAveraging = _statSourceInfo.Averaging;
      }

      return true;
    }

    protected void ApplyTitleText() {
      var info = StatSourceInfo;

      if (info == null) {
        return;
      }

      string longName = _statSourceType.GetLongName(_statId);

      if (!LabelTitle) {
        return;
      }
      
      var titleRT = LabelTitle.rectTransform;
      if (titleRT.rect.width < 100) {
        LabelTitle.text = info.ShortName ?? longName;
      } else {
        LabelTitle.text = longName;
      }
      BackImage.gameObject.SetActive(true);
    }

  }
}


#endregion


#region Assets/Photon/Fusion/Runtime/FusionStatsUtilities.cs

namespace Fusion.StatsInternal {
  using UnityEngine;
  using UnityEngine.Events;
  using UI = UnityEngine.UI;

  
  public interface IFusionStatsView {
    void Initialize();
    void CalculateLayout();
    void Refresh();
    bool isActiveAndEnabled { get; }
    Transform transform { get; }
  }

  public static class FusionStatsUtilities {

    public const int PAD           = 10;
    public const int MARGIN        = 6;
    public const int FONT_SIZE     = 12;
    public const int FONT_SIZE_MIN = 4;
    public const int FONT_SIZE_MAX = 200;

    const int METER_TEXTURE_WIDTH = 512;
    static Texture2D _meterTexture;
    static Texture2D MeterTexture {
      get {
        if (_meterTexture == null) {
          var tex = new Texture2D(METER_TEXTURE_WIDTH, 2);
          for (int x = 0; x < METER_TEXTURE_WIDTH; ++x) {
            for (int y = 0; y < 2; ++y) {
              var color = (x != 0 && x % 16 == 0) ? new Color(1f, 1f, 1f, 0.75f) : new Color(1f, 1f, 1f, 1f);
              tex.SetPixel(x, y, color);
            }
          }
          tex.Apply();
          return _meterTexture = tex;

        }
        return _meterTexture;
      }
    }

    static Sprite _meterSprite;
    public static Sprite MeterSprite {
      get {
        if (_meterSprite == null) {
          _meterSprite = Sprite.Create(MeterTexture, new Rect(0, 0, METER_TEXTURE_WIDTH, 2), new Vector2());
        }
        return _meterSprite;
      }
    }

    const int R = 64;

    static Texture2D _circle32Texture;
    static Texture2D Circle32Texture {
      get {
        if (_circle32Texture == null) {
          var tex = new Texture2D(R * 2, R * 2);
          for (int x = 0; x < R; ++x) {
            for (int y = 0; y < R; ++y) {
              double h = System.Math.Abs( System.Math.Sqrt(x * x + y * y));
              float a = h > R ? 0.0f : h < (R - 1) ? 1.0f :(float) (R - h);
              var c = new Color(1.0f, 1.0f, 1.0f, a);
              tex.SetPixel(R + 0 + x, R + 0 + y, c);
              tex.SetPixel(R - 1 - x, R + 0 + y, c);
              tex.SetPixel(R + 0 + x, R - 1 - y, c);
              tex.SetPixel(R - 1 - x, R - 1 - y, c);

            }
          }
          tex.Apply();
          return _circle32Texture = tex;
        }
        return _circle32Texture;
      }
    }

    static Sprite _circle32Sprite;
    public static Sprite CircleSprite {
      get {
        if (_circle32Sprite == null) {
          _circle32Sprite = Sprite.Create(Circle32Texture, new Rect(0, 0, R * 2, R * 2), new Vector2(R , R), 10f, 0, SpriteMeshType.Tight, new Vector4(R-1, R-1, R-1, R-1));
        }
        return _circle32Sprite;
      }
    }

    public static Color DARK_GREEN = new Color(0.0f, 0.5f, 0.0f, 1.0f);
    public static Color DARK_BLUE  = new Color(0.0f, 0.0f, 0.5f, 1.0f);
    public static Color DARK_RED   = new Color(0.5f, 0.0f, 0.0f, 1.0f);

    
    public static void ValidateRunner(this FusionStats fusionStats, NetworkRunner currentRunner) {
      
      bool runnerFromSelected   = fusionStats.RunnerFromSelected;
      bool currentRunnerIsValid = currentRunner && currentRunner.IsRunning;

      // Logic:
      // If EnableObjectStats is set, then always prioritize Object's runner.
      // next if RunnerFromSelected == true, try to get runner from selected object
      // next Find first active runner that matches ConnectTo - If not enforce single, bias toward runner associated with the FusionStats itself
      
      // First check to see if the current runner is perfectly valid so we can skip any searching expenses
      if (currentRunnerIsValid && runnerFromSelected == false && fusionStats.EnableObjectStats == false) {
        if ((fusionStats.ConnectTo & currentRunner.Mode) != 0) {
          return;
        }
      }

      // Prioritize selected NetworkObjects if EnableObjectStats
      if (fusionStats.EnableObjectStats) {
        var obj = fusionStats.Object;
        if (obj) {
          fusionStats.SetRunner(obj.Runner);
          return;
        }
      }
      
      // If in the editor and using Selected, 
#if UNITY_EDITOR
      if (runnerFromSelected) {
        var selected = UnityEditor.Selection.activeObject as GameObject;
        if (selected) {
          var found = NetworkRunner.GetRunnerForGameObject(selected);
          if (found && found.IsRunning) {
            fusionStats.SetRunner(found);
            return;
          }
        }
      }
#endif

      var gameObject = fusionStats.gameObject;
      var connectTo  = fusionStats.ConnectTo;
      
      // If we are no enforcing single, bias toward the runner associated with this actual FusionStats GameObject.
      if (fusionStats.EnforceSingle == false) {
        var sceneRunner = NetworkRunner.GetRunnerForGameObject(gameObject);
        if (sceneRunner && sceneRunner.IsRunning && (sceneRunner.Mode & connectTo) != 0) {
          fusionStats.SetRunner(sceneRunner);
          return;
        }
      }
      
      // Finally Loop all runners, looking for one that matches connectTo
      var enumerator = NetworkRunner.GetInstancesEnumerator();
      while (enumerator.MoveNext()) {
        var found = enumerator.Current;
        
        // Ignore non-running 
        if (found == null || found.IsRunning == false) {
          continue;
        }
        
        // May as well stop if we find Single Player, there is only one.
        if (found.IsSinglePlayer) {
          fusionStats.SetRunner(found);
          return;
        }
        
        // If this runner matches our preferred runner (ConnectTo), use it.
        if ((connectTo & found.Mode) != 0) {
          fusionStats.SetRunner(found);
          return;
        }
      }
    }

    public static RectTransform CreateRectTransform(this Transform parent, string name, bool expand = false) {
      var go = new GameObject(name);
      var rt = go.AddComponent<RectTransform>();
      rt.SetParent(parent);
      rt.localPosition = default;
      rt.localScale = default;
      rt.localScale = new Vector3(1, 1, 1);

      if (expand) {
        ExpandAnchor(rt);
      }
      return rt;
    }

    public static UI.Text AddText(this RectTransform rt, string label, TextAnchor anchor, Color FontColor, Font font, int maxFontSize = 200) {
      var text = rt.gameObject.AddComponent<UI.Text>();
      if (font != null) {
        text.font = font;
      }
      text.text                 = label;
      text.color                = FontColor;
      text.alignment            = anchor;
      text.fontSize             = FONT_SIZE;
      text.raycastTarget        = false;
      text.resizeTextMinSize    = FONT_SIZE_MIN;
      text.resizeTextMaxSize    = maxFontSize;
      text.resizeTextForBestFit = true;
      return text;
    }

    public  const float BTTN_LBL_NORM_HGHT = .175f;
    private const int   BTTN_FONT_SIZE_MAX = 100;
    private const float BTTN_ALPHA         = 0.925f;

    internal static void MakeButton(this RectTransform parent, ref UI.Button button, string iconText, string labelText, Font font, out UI.Text icon, out UI.Text text, UnityAction action) {
      var rt = parent.CreateRectTransform(labelText);
      button = rt.gameObject.AddComponent<UI.Button>();

      var iconRt = rt.CreateRectTransform("Icon", true);
      iconRt.anchorMin = new Vector2(0, BTTN_LBL_NORM_HGHT);
      iconRt.anchorMax = new Vector2(1, 1.0f);
      iconRt.offsetMin = new Vector2(0, 0);
      iconRt.offsetMax = new Vector2(0, 0);

      icon = iconRt.gameObject.AddComponent<UI.Text>();
      button.targetGraphic = icon;
      if (font != null) {
        icon.font = font;
      }
      icon.text = iconText;
      icon.alignment = TextAnchor.MiddleCenter;
      icon.fontStyle = FontStyle.Bold;
      icon.fontSize = BTTN_FONT_SIZE_MAX;
      icon.resizeTextMinSize = 0;
      icon.resizeTextMaxSize = BTTN_FONT_SIZE_MAX;
      icon.alignByGeometry = true;
      icon.resizeTextForBestFit = true;

      var textRt = rt.CreateRectTransform("Label", true);
      textRt.anchorMin = new Vector2(0, 0);
      textRt.anchorMax = new Vector2(1, BTTN_LBL_NORM_HGHT);
      textRt.pivot = new Vector2(.5f, BTTN_LBL_NORM_HGHT * .5f);
      textRt.offsetMin = new Vector2(0, 0);
      textRt.offsetMax = new Vector2(0, 0);

      text                      = textRt.gameObject.AddComponent<UI.Text>();
      text.color                = Color.black;
      if (font != null) {
        text.font = font;
      }
      text.text                 = labelText;
      text.alignment            = TextAnchor.MiddleCenter;
      text.fontStyle            = FontStyle.Bold;
      text.fontSize             = 0;
      text.resizeTextMinSize    = 0;
      text.resizeTextMaxSize    = BTTN_FONT_SIZE_MAX;
      text.resizeTextForBestFit = true;
      text.horizontalOverflow   = HorizontalWrapMode.Overflow;

      UI.ColorBlock colors = button.colors;
      colors.normalColor = new Color(.0f, .0f, .0f, BTTN_ALPHA);
      colors.pressedColor = new Color(.5f, .5f, .5f, BTTN_ALPHA);
      colors.highlightedColor = new Color(.3f, .3f, .3f, BTTN_ALPHA);
      colors.selectedColor = new Color(.0f, .0f, .0f, BTTN_ALPHA);
      button.colors = colors;

      button.onClick.AddListener(action);
    }

    public static RectTransform AddVerticalLayoutGroup(this RectTransform rt, float spacing, int? rgtPad = null, int? lftPad = null, int? topPad = null, int? botPad = null) {
      var group = rt.gameObject.AddComponent<UI.VerticalLayoutGroup>();
      group.childControlHeight = true;
      group.childControlWidth = true;
      group.spacing = spacing;
      return rt;
    }

    public static UI.GridLayoutGroup AddGridlLayoutGroup(this RectTransform rt, float spacing, int? rgtPad = null, int? lftPad = null, int? topPad = null, int? botPad = null) {
      var group = rt.gameObject.AddComponent<UI.GridLayoutGroup>();
      group.spacing = new Vector2( spacing, spacing);
      return group;
    }

    public static RectTransform AddImage(this RectTransform rt, Color color) {
      var image = rt.gameObject.AddComponent<UI.Image>();
      image.color = color;
      image.raycastTarget = false;
      return rt;
    }

    public static RectTransform AddCircleSprite(this RectTransform rt, Color color) {
      rt.AddCircleSprite(color, out var _);
      return rt;
    }

    public static RectTransform AddCircleSprite(this RectTransform rt, Color color, out UI.Image image) {
      image = rt.gameObject.AddComponent<UI.Image>();
      image.sprite = CircleSprite;
      image.type = UI.Image.Type.Sliced;
      image.pixelsPerUnitMultiplier = 100f;
      image.color = color;
      image.raycastTarget = false;
      return rt;

    }

    public static RectTransform ExpandAnchor(this RectTransform rt, float? padding = null) {
      rt.anchorMax = new Vector2(1, 1);
      rt.anchorMin = new Vector2(0, 0);
      rt.pivot = new Vector2(0.5f, 0.5f);
      if (padding.HasValue) {
        rt.offsetMin = new Vector2(padding.Value, padding.Value);
        rt.offsetMax = new Vector2(-padding.Value, -padding.Value);
      } else {
        rt.sizeDelta = default;
        rt.anchoredPosition = default;
      }
      return rt;
    }

    public static RectTransform ExpandTopAnchor(this RectTransform rt, float? padding = null) {
      rt.anchorMax = new Vector2(1, 1);
      rt.anchorMin = new Vector2(0, 1);
      rt.pivot = new Vector2(0.5f, 1f);
      if (padding.HasValue) {
        rt.offsetMin = new Vector2(padding.Value, padding.Value);
        rt.offsetMax = new Vector2(-padding.Value, -padding.Value);
      } else {
        rt.sizeDelta = default;
        rt.anchoredPosition = default;
      }
      return rt;
    }

    public static RectTransform SetSizeDelta(this RectTransform rt, float offsetX, float offsetY) {
      rt.sizeDelta = new Vector2(offsetX, offsetY);
      return rt;    
    }


    public static RectTransform SetOffsets(this RectTransform rt, float minX, float maxX, float minY, float maxY) {
      rt.offsetMin = new Vector2(minX, minY);
      rt.offsetMax = new Vector2(maxX, maxY);
      return rt;
    }

    public static RectTransform SetPivot(this RectTransform rt, float pivotX, float pivotY) {
      rt.pivot = new Vector2(pivotX, pivotY);
      return rt;
    }

    public static RectTransform SetAnchors(this RectTransform rt, float minX, float maxX, float minY, float maxY) {
      rt.anchorMin = new Vector2(minX, minY);
      rt.anchorMax = new Vector2(maxX, maxY);
      return rt;
    }
  }
}



#endregion


#region Assets/Photon/Fusion/Runtime/FusionUnityLogger.cs

namespace Fusion {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Runtime.CompilerServices;
  using System.Runtime.ExceptionServices;
  using System.Text;
  using System.Threading;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.Serialization;
  using Object = UnityEngine.Object;

  [Serializable]
  public partial class FusionUnityLogger : Fusion.ILogger {

    /// <summary>
    /// Implement this to modify values of this logger.
    /// </summary>
    /// <param name="logger"></param>
    static partial void InitializePartial(ref FusionUnityLogger logger);

    StringBuilder  _builder    = new StringBuilder();
    Thread _mainThread;

    public string NameUnavailableObjectDestroyedLabel = "(destroyed)";
    public string NameUnavailableInWorkerThreadLabel = "";

    /// <summary>
    /// If true, all messages will be prefixed with [Fusion] tag
    /// </summary>
    public bool UseGlobalPrefix;

    /// <summary>
    /// If true, some parts of messages will be enclosed with &lt;color&gt; tags.
    /// </summary>
    public bool UseColorTags;

    /// <summary>
    /// If true, each log message that has a source parameter will be prefixed with a hash code of the source object. 
    /// </summary>
    public bool AddHashCodePrefix;
    
    /// <summary>
    /// Color of the global prefix (see <see cref="UseGlobalPrefix"/>).
    /// </summary>
    public string GlobalPrefixColor;

    /// <summary>
    /// Min Random Color
    /// </summary>
    public Color32 MinRandomColor;
    
    /// <summary>
    /// Max Random Color
    /// </summary>
    public Color32 MaxRandomColor;

    /// <summary>
    /// Server Color
    /// </summary>
    public Color ServerColor;

    public FusionUnityLogger(Thread mainThread) {

      _mainThread = mainThread;
      
      bool isDarkMode = false;
#if UNITY_EDITOR
      isDarkMode = UnityEditor.EditorGUIUtility.isProSkin;
#endif

      MinRandomColor = isDarkMode ? new Color32(158, 158, 158, 255) : new Color32(30, 30, 30, 255);
      MaxRandomColor = isDarkMode ? new Color32(255, 255, 255, 255) : new Color32(90, 90, 90, 255);
      ServerColor    = isDarkMode ? new Color32(255, 255, 158, 255) : new Color32(30, 90, 200, 255);

      UseColorTags = true;
      UseGlobalPrefix = true;
      GlobalPrefixColor = Color32ToRGBString(isDarkMode ? new Color32(115, 172, 229, 255) : new Color32(20, 64, 120, 255));
    }

    public void Log(LogType logType, object message, in LogContext logContext) {

      Debug.Assert(_builder.Length == 0);
      string fullMessage;

      var obj = logContext.Source as UnityEngine.Object;

      try {
        if (logType == LogType.Debug) {
          _builder.Append("[DEBUG] ");
        } else if (logType == LogType.Trace) {
          _builder.Append("[TRACE] ");
        }

        if (UseGlobalPrefix) {
          if (UseColorTags) {
            _builder.Append("<color=");
            _builder.Append(GlobalPrefixColor);
            _builder.Append(">");
          }
          _builder.Append("[Fusion");

          if (!string.IsNullOrEmpty(logContext.Prefix)) {
            _builder.Append("/");
            _builder.Append(logContext.Prefix);
          }

          _builder.Append("]");

          if (UseColorTags) {
            _builder.Append("</color>");
          }
          _builder.Append(" ");
        } else {
          if (!string.IsNullOrEmpty(logContext.Prefix)) {
            _builder.Append(logContext.Prefix);
            _builder.Append(": ");
          }
        }

        if (obj) {
          var pos = _builder.Length;
          if (obj is NetworkRunner runner) {
            TryAppendRunnerPrefix(_builder, runner);
          } else if (obj is NetworkObject networkObject) {
            TryAppendNetworkObjectPrefix(_builder, networkObject);
          } else if (obj is SimulationBehaviour simulationBehaviour) {
            TryAppendSimulationBehaviourPrefix(_builder, simulationBehaviour);
          } else {
            AppendNameThreadSafe(_builder, obj); 
          }
          if (_builder.Length > pos) {
            _builder.Append(": ");
          }
        }
        _builder.Append(message);
        
        fullMessage = _builder.ToString();
      } finally {
        _builder.Clear();
      }

      switch (logType) {
        case LogType.Error:
          Debug.LogError(fullMessage, IsInMainThread ? obj : null);
          break;
        case LogType.Warn:
          Debug.LogWarning(fullMessage, IsInMainThread ? obj : null);
          break;
        default:
          Debug.Log(fullMessage, IsInMainThread ? obj : null);
          break;
      }
    }

    public void LogException(Exception ex, in LogContext logContext) {
      Log(LogType.Error, $"{ex.GetType()} <i>See next error log entry for details.</i>", in logContext);
      
#if UNITY_EDITOR
      // this is to force console window double click to take you where the exception
      // has been thrown, not where it has been logged
      var edi = ExceptionDispatchInfo.Capture(ex);
      var thread = new Thread(() => {
        edi.Throw();
      });
      thread.Start();
      thread.Join();
#else
      if (logContext.Source is UnityEngine.Object obj) {
        Debug.LogException(ex, obj);
      } else {
        Debug.LogException(ex);
      }
#endif
    }

    int GetRandomColor(int seed) => GetRandomColor(seed, MinRandomColor, MaxRandomColor, ServerColor);

    int GetColorSeed(string name) {
      int hash = 0;
      for (var i = 0; i < name.Length; ++i) {
        hash = hash * 31 + name[i];
      }

      return hash;
    }

    static int GetRandomColor(int seed, Color32 min, Color32 max, Color32 svr) {
      var random = new NetworkRNG(seed);
      int r, g, b;
      // -1 indicates host/client - give it a more pronounced color.
      if (seed == -1) {
        r = svr.r;
        g = svr.g;
        b = svr.b;
      } else {
        r = random.RangeInclusive(min.r, max.r);
        g = random.RangeInclusive(min.g, max.g);
        b = random.RangeInclusive(min.b, max.b);
      }

      r = Mathf.Clamp(r, 0, 255);
      g = Mathf.Clamp(g, 0, 255);
      b = Mathf.Clamp(b, 0, 255);

      int rgb = (r << 16) | (g << 8) | b;
      return rgb;
    }

    static int Color32ToRGB24(Color32 c) {
      return (c.r << 16) | (c.g << 8) | c.b;
    }

    static string Color32ToRGBString(Color32 c) {
      return string.Format("#{0:X6}", Color32ToRGB24(c));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize() {
      if (Fusion.Log.Initialized) {
        return;
      }

      var logger = new FusionUnityLogger(Thread.CurrentThread);

      // Optional override of default values
      InitializePartial(ref logger);

      if (logger != null) {
        Fusion.Log.Init(logger);
      }
    }
    
    private void AppendNameThreadSafe(StringBuilder builder, UnityEngine.Object obj) {
      
      if  ((object)obj == null) throw new ArgumentNullException(nameof(obj));
      
      string name;
      bool isDestroyed = obj == null;
      
      if (isDestroyed) {
        name = NameUnavailableObjectDestroyedLabel;
      } else if (!IsInMainThread) {
        name = NameUnavailableInWorkerThreadLabel;
      } else {
        name = obj.name;
      }
      
      if (UseColorTags) {
        int colorSeed = GetColorSeed(name);
        builder.AppendFormat("<color=#{0:X6}>", GetRandomColor(colorSeed));
      }

      if (AddHashCodePrefix) {
        builder.AppendFormat("{0:X8}", obj.GetHashCode());
      }

      if (name?.Length > 0) {
        if (AddHashCodePrefix) {
          builder.Append(" ");
        }
        builder.Append(name);  
      }

      if (UseColorTags) {
        builder.Append("</color>");
      }
    }

    private bool IsInMainThread => _mainThread == Thread.CurrentThread;

    bool TryAppendRunnerPrefix(StringBuilder builder, NetworkRunner runner) {
      if ((object)runner == null) {
        return false;
      }
      if (runner.Config?.PeerMode != NetworkProjectConfig.PeerModes.Multiple) {
        return false;
      }

      AppendNameThreadSafe(builder, runner);

      var localPlayer = runner.LocalPlayer;
      if (localPlayer.IsRealPlayer) {
        builder.Append("[P").Append(localPlayer.PlayerId).Append("]");
      } else {
        builder.Append("[P-]");
      }
      
      return true;
    }
    
    bool TryAppendNetworkObjectPrefix(StringBuilder builder, NetworkObject networkObject) {
      if ((object)networkObject == null) {
        return false;
      }

      AppendNameThreadSafe(builder, networkObject);
      
      if (networkObject.Id.IsValid) {
        builder.Append(" ");
        builder.Append(networkObject.Id.ToString());
      }
      
      int pos = builder.Length;
      if (TryAppendRunnerPrefix(builder, networkObject.Runner)) {
        builder.Insert(pos, '@');
      }

      return true;
    }
    
    bool TryAppendSimulationBehaviourPrefix(StringBuilder builder, SimulationBehaviour simulationBehaviour) {
      if ((object)simulationBehaviour == null) {
        return false;
      }

      AppendNameThreadSafe(builder, simulationBehaviour);
      
      if (simulationBehaviour is NetworkBehaviour nb && nb.Id.IsValid) {
        builder.Append(" ");
        builder.Append(nb.Id.ToString());
      }
      
      int pos = builder.Length;
      if (TryAppendRunnerPrefix(builder, simulationBehaviour.Runner)) {
        builder.Insert(pos, '@');
      }

      return true;
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Runtime/NetworkObjectBaker.cs

﻿//#undef UNITY_EDITOR
namespace Fusion {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEngine;

#if UNITY_EDITOR
  using UnityEditor;
#endif

  public class NetworkObjectBaker {

    private List<NetworkObject> _allNetworkObjects             = new List<NetworkObject>();
    private List<TransformPath> _networkObjectsPaths           = new List<TransformPath>();
    private List<SimulationBehaviour> _allSimulationBehaviours = new List<SimulationBehaviour>();
    private TransformPathCache _pathCache                      = new TransformPathCache();
    private List<NetworkBehaviour> _arrayBufferNB    = new List<NetworkBehaviour>();
    private List<NetworkObject> _arrayBufferNO       = new List<NetworkObject>();

    public struct Result {
      public bool HadChanges { get; }
      public int ObjectCount { get; }
      public int BehaviourCount { get; }

      public Result(bool dirty, int objectCount, int behaviourCount) {
        HadChanges = dirty;
        ObjectCount = objectCount;
        BehaviourCount = behaviourCount;
      }
    }

    protected virtual void SetDirty(MonoBehaviour obj) {
      // do nothing
    }

    protected virtual bool TryGetExecutionOrder(MonoBehaviour obj, out int order) {
      order = default;
      return false;
    }
    
    protected virtual uint GetSortKey(NetworkObject obj) {
      return 0;
    }

    [System.Diagnostics.Conditional("FUSION_EDITOR_TRACE")]
    protected static void Trace(string msg) {
      Debug.Log($"[Fusion/NetworkObjectBaker] {msg}");
    }

    protected static void Warn(string msg, UnityEngine.Object context = null) {
      Debug.LogWarning($"[Fusion/NetworkObjectBaker] {msg}", context);
    }

    public Result Bake(GameObject root) {

      if (root == null) {
        throw new ArgumentNullException(nameof(root));
      }
      
      root.GetComponentsInChildren(true, _allNetworkObjects);
      
      // remove null ones (missing scripts may cause that)
      _allNetworkObjects.RemoveAll(x => x == null);
      
      if (_allNetworkObjects.Count == 0) {
        return new Result(false, 0, 0);
      }

      try {
        foreach (var obj in _allNetworkObjects) {
          _networkObjectsPaths.Add(_pathCache.Create(obj.transform));
        }

        bool dirty = false;
        
        _allNetworkObjects.Reverse();
        _networkObjectsPaths.Reverse();

        root.GetComponentsInChildren(true, _allSimulationBehaviours);
        _allSimulationBehaviours.RemoveAll(x => x == null);
        
        int countNO = _allNetworkObjects.Count;
        int countSB = _allSimulationBehaviours.Count;

        // start from the leaves
        for (int i = 0; i < _allNetworkObjects.Count; ++i) {
          var obj = _allNetworkObjects[i];

          var objDirty = false;
          var objActive = obj.gameObject.activeInHierarchy;
          int? objExecutionOrder = null;
          if (!objActive) {
            if (TryGetExecutionOrder(obj, out var order)) {
              objExecutionOrder = order;
            } else {
              Warn($"Unable to get execution order for {obj}. " +
                $"Because the object is initially inactive, Fusion is unable to guarantee " +
                $"the script's Awake will be invoked before Spawned. Please implement {nameof(TryGetExecutionOrder)}.");
            }
          }

          // find nested behaviours
          _arrayBufferNB.Clear();
          
          var path = _networkObjectsPaths[i];
          
          string entryPath = path.ToString();
          for (int scriptIndex = _allSimulationBehaviours.Count - 1; scriptIndex >= 0; --scriptIndex) {
            var script = _allSimulationBehaviours[scriptIndex];
            var scriptPath = _pathCache.Create(script.transform);

            if (_pathCache.IsEqualOrAncestorOf(path, scriptPath)) {
              if (script is NetworkBehaviour nb) {
                _arrayBufferNB.Add(nb);
              }
              
              _allSimulationBehaviours.RemoveAt(scriptIndex);

              if (objExecutionOrder != null) {
                // check if execution order is ok
                if (TryGetExecutionOrder(script, out var scriptOrder)) {
                  if (objExecutionOrder <= scriptOrder) {
                    Warn($"{obj} execution order is less or equal than of the script {script}. " +
                      $"Because the object is initially inactive, Spawned callback will be invoked before the script's Awake on activation.", script);
                  }
                } else {
                  Warn($"Unable to get execution order for {script}. " +
                    $"Because the object is initially inactive, Fusion is unable to guarantee " +
                    $"the script's Awake will be invoked before Spawned. Please implement {nameof(TryGetExecutionOrder)}.");
                }
              }

            } else if (_pathCache.Compare(path, scriptPath) < 0) {
              // can't discard it yet
            } else {
              Debug.Assert(_pathCache.Compare(path, scriptPath) > 0);
              break;
            }
          }

          _arrayBufferNB.Reverse();
          objDirty |= Set(obj, ref obj.NetworkedBehaviours, _arrayBufferNB);

          // handle flags

          var flags = obj.Flags;

          if (!flags.IsVersionCurrent()) {
            flags = flags.SetCurrentVersion();
          }

          objDirty |= Set(obj, ref obj.Flags, flags);

          // what's left is nested network objects resolution
          {
            _arrayBufferNO.Clear();

            // collect descendants; descendants should be continous without gaps here
            int j = i - 1;
            for (; j >= 0 && _pathCache.IsAncestorOf(path, _networkObjectsPaths[j]); --j) {
              _arrayBufferNO.Add(_allNetworkObjects[j]);
            }

            int descendantsBegin = j + 1;
            Debug.Assert(_arrayBufferNO.Count == i - descendantsBegin);

            objDirty |= Set(obj, ref obj.NestedObjects, _arrayBufferNO);
          }

          objDirty |= Set(obj, ref obj.SortKey, GetSortKey(obj));
          
          if (objDirty) {
            SetDirty(obj);
            dirty = true;
          }
        }

        return new Result(dirty, countNO, countSB);
      } finally {
        _pathCache.Clear();
        _allNetworkObjects.Clear();
        _allSimulationBehaviours.Clear();

        _networkObjectsPaths.Clear();

        _arrayBufferNB.Clear();
        _arrayBufferNO.Clear();
      }
    }

    private bool Set<T>(MonoBehaviour host, ref T field, T value) {
      if (!EqualityComparer<T>.Default.Equals(field, value)) {
        Trace($"Object dirty: {host} ({field} vs {value})");
        field = value;
        return true;
      } else {
        return false;
      }
    }

    private bool Set<T>(MonoBehaviour host, ref T[] field, List<T> value) {
      var comparer = EqualityComparer<T>.Default;
      if (field == null || field.Length != value.Count || !field.SequenceEqual(value, comparer)) {
        Trace($"Object dirty: {host} ({field} vs {value})");
        field = value.ToArray();
        return true;
      } else {
        return false;
      }
    }

    public unsafe readonly struct TransformPath {
      public const int MaxDepth = 10;

      public struct _Indices {
        public fixed ushort Value[MaxDepth];
      }

      public readonly _Indices Indices;
      public readonly ushort Depth;
      public readonly ushort Next;

      internal TransformPath(ushort depth, ushort next, List<ushort> indices, int offset, int count) {
        Depth = depth;
        Next = next;

        for (int i = 0; i < count; ++i) {
          Indices.Value[i] = indices[i + offset];
        }
      }

      public override string ToString() {
        var builder = new StringBuilder();
        for (int i = 0; i < Depth && i < MaxDepth; ++i) {
          if (i > 0) {
            builder.Append("/");
          }
          builder.Append(Indices.Value[i]);
        }

        if (Depth > MaxDepth) {
          Debug.Assert(Next > 0);
          builder.Append($"/...[{Depth - MaxDepth}]");
        }

        return builder.ToString();
      }
    }

    public sealed unsafe class TransformPathCache : IComparer<TransformPath>, IEqualityComparer<TransformPath> {

      private Dictionary<Transform, TransformPath> _cache = new Dictionary<Transform, TransformPath>();
      private List<ushort> _siblingIndexStack             = new List<ushort>();
      private List<TransformPath> _nexts                  = new List<TransformPath>();


      public TransformPath Create(Transform transform) {
        if (_cache.TryGetValue(transform, out var existing)) {
          return existing;
        }

        _siblingIndexStack.Clear();
        for (var tr = transform; tr != null; tr = tr.parent) {
          _siblingIndexStack.Add(checked((ushort)tr.GetSiblingIndex()));
        }
        _siblingIndexStack.Reverse();


        var depth = checked((ushort)_siblingIndexStack.Count);

        ushort nextPlusOne = 0;

        if (depth > TransformPath.MaxDepth) {

          int i;
          if (depth % TransformPath.MaxDepth != 0) {
            // tail is going to be partially full
            i = depth - (depth % TransformPath.MaxDepth);
          } else {
            // tail is going to be full
            i = depth - TransformPath.MaxDepth;
          }

          for (; i > 0; i -= TransformPath.MaxDepth) {
            checked {
              TransformPath path = new TransformPath((ushort)(depth - i), nextPlusOne,
                _siblingIndexStack, i, Mathf.Min(TransformPath.MaxDepth, depth - i));
              _nexts.Add(path);
              nextPlusOne = (ushort)_nexts.Count;
            }
          }
        }

        var result = new TransformPath(depth, nextPlusOne,
          _siblingIndexStack, 0, Mathf.Min(TransformPath.MaxDepth, depth));

        _cache.Add(transform, result);
        return result;
      }

      public void Clear() {
        _nexts.Clear();
        _cache.Clear();
        _siblingIndexStack.Clear();
      }

      public bool Equals(TransformPath x, TransformPath y) {
        if (x.Depth != y.Depth) {
          return false;
        }

        return CompareToDepthUnchecked(x, y, x.Depth) == 0;
      }

      public int GetHashCode(TransformPath obj) {
        int hash = obj.Depth;
        return GetHashCode(obj, hash);
      }

      public int Compare(TransformPath x, TransformPath y) {
        var diff = CompareToDepthUnchecked(x, y, Mathf.Min(x.Depth, y.Depth));
        if (diff != 0) {
          return diff;
        }

        return x.Depth - y.Depth;
      }

      private int CompareToDepthUnchecked(in TransformPath x, in TransformPath y, int depth) {
        for (int i = 0; i < depth && i < TransformPath.MaxDepth; ++i) {
          int diff = x.Indices.Value[i] - y.Indices.Value[i];
          if (diff != 0) {
            return diff;
          }
        }

        if (depth > TransformPath.MaxDepth) {
          Debug.Assert(x.Next > 0);
          Debug.Assert(y.Next > 0);
          return CompareToDepthUnchecked(_nexts[x.Next - 1], _nexts[y.Next - 1], depth - TransformPath.MaxDepth);
        } else {
          return 0;
        }
      }

      private int GetHashCode(in TransformPath path, int hash) {
        for (int i = 0; i < path.Depth && i < TransformPath.MaxDepth; ++i) {
          hash = hash * 31 + path.Indices.Value[i];
        }

        if (path.Depth > TransformPath.MaxDepth) {
          Debug.Assert(path.Next > 0);
          hash = GetHashCode(_nexts[path.Next - 1], hash);
        }

        return hash;
      }

      public bool IsAncestorOf(in TransformPath x, in TransformPath y) {
        if (x.Depth >= y.Depth) {
          return false;
        }

        return CompareToDepthUnchecked(x, y, x.Depth) == 0;
      }

      public bool IsEqualOrAncestorOf(in TransformPath x, in TransformPath y) {
        if (x.Depth > y.Depth) {
          return false;
        }

        return CompareToDepthUnchecked(x, y, x.Depth) == 0;
      }

      public string Dump(in TransformPath x) {
        var builder = new StringBuilder();

        Dump(x, builder);

        return builder.ToString();
      }

      private void Dump(in TransformPath x, StringBuilder builder) {
        for (int i = 0; i < x.Depth && i < TransformPath.MaxDepth; ++i) {
          if (i > 0) {
            builder.Append("/");
          }
          builder.Append(x.Indices.Value[i]);
        }

        if (x.Depth > TransformPath.MaxDepth) {
          Debug.Assert(x.Next > 0);
          builder.Append("/");
          Dump(_nexts[x.Next - 1], builder);
        }
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Runtime/NetworkPrefabSourceUnity.cs

﻿namespace Fusion {
  using System;
  using Object = UnityEngine.Object;

  [Serializable]
  public class NetworkPrefabSourceStatic : NetworkAssetSourceStatic<NetworkObject>, INetworkPrefabSource {
    public NetworkObjectGuid               AssetGuid;
    NetworkObjectGuid INetworkPrefabSource.AssetGuid => AssetGuid;
  }
  
  [Serializable]
  public class NetworkPrefabSourceStaticLazy : NetworkAssetSourceStaticLazy<NetworkObject>, INetworkPrefabSource {
    public NetworkObjectGuid               AssetGuid;
    NetworkObjectGuid INetworkPrefabSource.AssetGuid => AssetGuid;
  }

  [Serializable]
  public class NetworkPrefabSourceResource : NetworkAssetSourceResource<NetworkObject>, INetworkPrefabSource {
    public NetworkObjectGuid               AssetGuid;
    NetworkObjectGuid INetworkPrefabSource.AssetGuid => AssetGuid;
  }
  
#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
  [Serializable]
  public class NetworkPrefabSourceAddressable : NetworkAssetSourceAddressable<NetworkObject>, INetworkPrefabSource {
    public NetworkObjectGuid               AssetGuid;
    NetworkObjectGuid INetworkPrefabSource.AssetGuid => AssetGuid;
  }
#endif
}

#endregion


#region Assets/Photon/Fusion/Runtime/Utilities/FusionScalableIMGUI.cs

namespace Fusion {
  using System.Reflection;
  using UnityEngine;

  /// <summary>
  /// In-Game IMGUI style used for the <see cref="FusionBootstrapDebugGUI"/> interface.
  /// </summary>
  public static class FusionScalableIMGUI {
    private static GUISkin _scalableSkin;

    private static void InitializedGUIStyles(GUISkin baseSkin) {
      _scalableSkin = baseSkin == null ? GUI.skin : baseSkin;

      // If no skin was provided, make the built in GuiSkin more tolerable.
      if (baseSkin == null) {
        _scalableSkin = GUI.skin;
        _scalableSkin.button.alignment = TextAnchor.MiddleCenter;
        _scalableSkin.label.alignment = TextAnchor.MiddleCenter;
        _scalableSkin.textField.alignment = TextAnchor.MiddleCenter;

        _scalableSkin.button.normal.background = _scalableSkin.box.normal.background;
        _scalableSkin.button.hover.background = _scalableSkin.window.normal.background;

        _scalableSkin.button.normal.textColor = new Color(.8f, .8f, .8f);
        _scalableSkin.button.hover.textColor = new Color(1f, 1f, 1f);
        _scalableSkin.button.active.textColor = new Color(1f, 1f, 1f);
        _scalableSkin.button.border = new RectOffset(6, 6, 6, 6);
        _scalableSkin.window.border = new RectOffset(8, 8, 8, 10);
      } else {
        // Use the supplied skin as the base.
        _scalableSkin = baseSkin;
      }
    }

    /// <summary>
    /// Get the custom scalable skin, already resized to the current screen. Provides the height, width, padding and margin used.
    /// </summary>
    /// <returns></returns>
    public static GUISkin GetScaledSkin(GUISkin baseSkin, out float height, out float width, out int padding, out int margin, out float boxLeft) {

      if (_scalableSkin == null) {
        InitializedGUIStyles(baseSkin);
      }

      var dimensions = ScaleGuiSkinToScreenHeight();
      height = dimensions.Item1;
      width = dimensions.Item2;
      padding = dimensions.Item3;
      margin = dimensions.Item4;
      boxLeft = dimensions.Item5;
      return _scalableSkin;
    }

    /// <summary>
    /// Modifies a skin to make it scale with screen height.
    /// </summary>
    /// <param name="skin"></param>
    /// <returns>Returns (height, width, padding, top-margin, left-box-margin) values applied to the GuiSkin</returns>
    public static (float, float, int, int, float) ScaleGuiSkinToScreenHeight() {

      bool isVerticalAspect = Screen.height > Screen.width;
      bool isSuperThin = Screen.height / Screen.width > (17f / 9f);

      float height = Screen.height * .08f;
      float width = System.Math.Min(Screen.width * .9f, Screen.height * .6f);
      int padding = (int)(height / 4);
      int margin = (int)(height / 8);
      float boxLeft = (Screen.width - width) * .5f;

      int fontsize = (int)(isSuperThin ? (width - (padding * 2)) * .07f : height * .4f);
      var margins = new RectOffset(0, 0, margin, margin);

      _scalableSkin.button.fontSize = fontsize;
      _scalableSkin.button.margin = margins;
      _scalableSkin.label.fontSize = fontsize;
      _scalableSkin.label.padding = new RectOffset(padding, padding, padding, padding);
      _scalableSkin.textField.fontSize = fontsize;
      _scalableSkin.window.padding = new RectOffset(padding, padding, padding, padding);
      _scalableSkin.window.margin = new RectOffset(margin, margin, margin, margin);

      return (height, width, padding, margin, boxLeft);
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Runtime/Utilities/FusionUnitySceneManagerUtils.cs

﻿namespace Fusion {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  public static class FusionUnitySceneManagerUtils {

    public class SceneEqualityComparer : IEqualityComparer<Scene> {
      public bool Equals(Scene x, Scene y) {
        return x.handle == y.handle;
      }

      public int GetHashCode(Scene obj) {
        return obj.handle;
      }
    }

    public static bool IsAddedToBuildSettings(this Scene scene) {
      if (scene.buildIndex < 0) {
        return false;
      }
      // yep that's a thing: https://docs.unity3d.com/ScriptReference/SceneManagement.Scene-buildIndex.html
      if (scene.buildIndex >= SceneManager.sceneCountInBuildSettings) {
        return false;
      }
      return true;
    }

#if UNITY_EDITOR
    public static bool AddToBuildSettings(Scene scene) {
      if (IsAddedToBuildSettings(scene)) {
        return false;
      }

      EditorBuildSettings.scenes =
        new[] { new EditorBuildSettingsScene(scene.path, true) }
        .Concat(EditorBuildSettings.scenes)
        .ToArray();

      Debug.Log($"Added '{scene.path}' as first entry in Build Settings.");
      return true;
    }
#endif

    public static LocalPhysicsMode GetLocalPhysicsMode(this Scene scene) {
      LocalPhysicsMode mode = LocalPhysicsMode.None;
      if (scene.GetPhysicsScene() != Physics.defaultPhysicsScene) {
        mode |= LocalPhysicsMode.Physics3D;
      }
      if (scene.GetPhysicsScene2D() != Physics2D.defaultPhysicsScene) {
        mode |= LocalPhysicsMode.Physics2D;
      }
      return mode;
    }

    /// <summary>
    /// Finds all components of type <typeparam name="T"/> in the scene.
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="includeInactive"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T[] GetComponents<T>(this Scene scene, bool includeInactive) where T : Component {
      return GetComponents<T>(scene, includeInactive, out _);
    }
    
    /// <summary>
    /// Finds all components of type <typeparam name="T"/> in the scene.
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="includeInactive"></param>
    /// <param name="rootObjects"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T[] GetComponents<T>(this Scene scene, bool includeInactive, out GameObject[] rootObjects) where T : Component {
      rootObjects = scene.GetRootGameObjects();
      
      var partialResult = new List<T>();
      var result        = new List<T>();

      foreach (var go in rootObjects) {
        // depth-first, according to docs and verified by our tests
        go.GetComponentsInChildren(includeInactive: includeInactive, partialResult);
        // AddRange accepts IEnumerable, so there would be an alloc
        foreach (var comp in partialResult) {
          result.Add(comp);
        }
      }
      return result.ToArray(); 
    }
    
    private static readonly List<GameObject> _reusableGameObjectList = new List<GameObject>();
    
    /// <summary>
    /// Finds all components of type <typeparam name="T"/> in the scene.
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="results"></param>
    /// <param name="includeInactive"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static void GetComponents<T>(this Scene scene, List<T> results, bool includeInactive) where T : Component {
      var rootObjects = _reusableGameObjectList;
      scene.GetRootGameObjects(rootObjects);
      results.Clear();
      
      var partialResult = new List<T>();

      foreach (var go in rootObjects) {
        // depth-first, according to docs and verified by our tests
        go.GetComponentsInChildren(includeInactive: includeInactive, partialResult);
        // AddRange accepts IEnumerable, so there would be an alloc
        foreach (var comp in partialResult) {
          results.Add(comp);
        }
      }
    }
    
    /// <summary>
    /// Finds the first instance of type <typeparam name="T"/> in the scene. Returns null if no instance found.
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="includeInactive"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T FindComponent<T>(this Scene scene, bool includeInactive = false) where T : Component {
      var rootObjects = _reusableGameObjectList;
      scene.GetRootGameObjects(rootObjects);

      foreach (var go in rootObjects) {
        // depth-first, according to docs and verified by our tests
        var found = go.GetComponentInChildren<T>(includeInactive);
        if (found != null) {
          return found;
        }
      }
      return null;
    }

    public static bool CanBeUnloaded(this Scene scene) {
      if (!scene.isLoaded) {
        return false;
      }
      
      for (int i = 0; i < SceneManager.sceneCount; ++i) {
        var s = SceneManager.GetSceneAt(i);
        if (s != scene && s.isLoaded) {
          return true;
        }
      }
      return false;
    }

    public static string Dump(this Scene scene) {
      StringBuilder result = new StringBuilder();

      result.Append("[UnityScene:");
      
      if (scene.IsValid()) {
        result.Append(scene.name);
        result.Append(", isLoaded:").Append(scene.isLoaded);
        result.Append(", buildIndex:").Append(scene.buildIndex);
        result.Append(", isDirty:").Append(scene.isDirty);
        result.Append(", path:").Append(scene.path);
        result.Append(", rootCount:").Append(scene.rootCount);
        result.Append(", isSubScene:").Append(scene.isSubScene);
      } else {
        result.Append("<Invalid>");
      }

      result.Append(", handle:").Append(scene.handle);
      result.Append("]");
      return result.ToString();
    }

    public static string Dump(this LoadSceneParameters loadSceneParameters) {
      return $"[LoadSceneParameters: {loadSceneParameters.loadSceneMode}, localPhysicsMode:{loadSceneParameters.localPhysicsMode}]";
    }
    
    public static int GetSceneBuildIndex(string nameOrPath) {
      if (nameOrPath.IndexOf('/') >= 0) {
        return SceneUtility.GetBuildIndexByScenePath(nameOrPath);
      } else {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; ++i) {
          var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
          GetFileNameWithoutExtensionPosition(scenePath, out var nameIndex, out var nameLength);
          if (nameLength == nameOrPath.Length && string.Compare(scenePath, nameIndex, nameOrPath, 0, nameLength, true) == 0) {
            return i;
          }
        }

        return -1;
      }
    }
    
    public static int GetSceneIndex(IList<string> scenePathsOrNames, string nameOrPath) {
      if (nameOrPath.IndexOf('/') >= 0) {
        return scenePathsOrNames.IndexOf(nameOrPath);
      } else {
        for (int i = 0; i < scenePathsOrNames.Count; ++i) {
          var scenePath = scenePathsOrNames[i];
          GetFileNameWithoutExtensionPosition(scenePath, out var nameIndex, out var nameLength);
          if (nameLength == nameOrPath.Length && string.Compare(scenePath, nameIndex, nameOrPath, 0, nameLength, true) == 0) {
            return i;
          }
        }
        return -1;
      }
    }

    public static void GetFileNameWithoutExtensionPosition(string nameOrPath, out int index, out int length) {
      var lastSlash = nameOrPath.LastIndexOf('/');
      if (lastSlash >= 0) {
        index = lastSlash + 1;
      } else {
        index = 0;
      }

      var lastDot = nameOrPath.LastIndexOf('.');
      if (lastDot > index) {
        length = lastDot - index;
      } else {
        length = nameOrPath.Length - index;
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/NetworkRunnerVisibilityExtensions.cs

namespace Fusion
{
  using System.Collections.Generic;
  using UnityEngine;
  using Analyzer;

    public static class NetworkRunnerVisibilityExtensions {
   
      // TODO: Still needed?
      [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
      private static void ResetAllSimulationStatics() {
        ResetStatics();
      }
      
      /// <summary>
      /// Types that fusion.runtime isn't aware of, which need to be found using names instead.
      /// </summary>
      [StaticField(StaticFieldResetMode.None)]
      private static readonly string[] RecognizedBehaviourNames = 
      {
        "EventSystem"
      };
      
      [StaticField(StaticFieldResetMode.None)]
      private static readonly System.Type[] RecognizedBehaviourTypes = {
        typeof(IRunnerVisibilityRecognizedType),
        typeof(Renderer),
        typeof(AudioListener),
        typeof(Camera),
        typeof(Canvas),
        typeof(Light)
      };

      
      private static readonly Dictionary<NetworkRunner, RunnerVisibility> DictionaryLookup;

      // Constructor
      static NetworkRunnerVisibilityExtensions() {
        DictionaryLookup = new Dictionary<NetworkRunner, RunnerVisibility>();
      }

      private class RunnerVisibility {
        public bool IsVisible { get; set; } = true;

        public LinkedList<RunnerVisibilityLink> Nodes = new LinkedList<RunnerVisibilityLink>();
      }

      public static void EnableVisibilityExtension(this NetworkRunner runner) {
        if (runner && DictionaryLookup.ContainsKey(runner) == false) {
          DictionaryLookup.Add(runner, new RunnerVisibility());
        }
      }

      public static void DisableVisibilityExtension(this NetworkRunner runner) {
        if (runner && DictionaryLookup.ContainsKey(runner)) {
          DictionaryLookup.Remove(runner);
        }
      }
      
      public static bool HasVisibilityEnabled(this NetworkRunner runner) {
        return DictionaryLookup.ContainsKey(runner);
      }
      
      public static bool GetVisible(this NetworkRunner runner) {
        if (runner == null) {
          return false;
        }
        
        if (DictionaryLookup.TryGetValue(runner, out var runnerVisibility) == false) {
          return true;
        }

        return runnerVisibility.IsVisible;
      }

      public static void SetVisible(this NetworkRunner runner, bool isVisibile) {
        runner.GetVisibilityInfo().IsVisible = isVisibile;
        RefreshRunnerVisibility(runner);
      }

      private static LinkedList<RunnerVisibilityLink> GetVisibilityNodes(this NetworkRunner runner) {
        if (runner == false) {
          return null;
        }
        return runner.GetVisibilityInfo()?.Nodes;
      }

      private static RunnerVisibility GetVisibilityInfo(this NetworkRunner runner) {
        if (DictionaryLookup.TryGetValue(runner, out var runnerVisibility) == false) {
          return null;
        }

        return runnerVisibility;
      }
      
      /// <summary>
      /// Find all component types that contribute to a scene rendering, and associate them with a <see cref="RunnerVisibilityLink"/> component, 
      /// and add them to the runner's list of visibility nodes.
      /// </summary>
      /// <param name="go"></param>
      /// <param name="runner"></param>
      public static void AddVisibilityNodes(this NetworkRunner runner, GameObject go) {
        runner.EnableVisibilityExtension();

        // Check for flag component which indicates object has already been cataloged.
        if (go.GetComponent<RunnerVisibilityLinksRoot>()) {return;}
      
        go.AddComponent<RunnerVisibilityLinksRoot>();

        // Have user EnableOnSingleRunner add RunnerVisibilityControl before we process all nodes.
        var existingEnableOnSingles = go.transform.GetComponentsInChildren<EnableOnSingleRunner>(false);
      
        foreach (var enableOnSingleRunner in existingEnableOnSingles) {
          enableOnSingleRunner.AddNodes();
        }

        RunnerVisibilityLink[] existingNodes = go.GetComponentsInChildren<RunnerVisibilityLink>(false);

        CollectBehavioursAndAddNodes(go, runner, existingNodes);

        RefreshRunnerVisibility(runner);
      }

      private static void CollectBehavioursAndAddNodes(GameObject go, NetworkRunner runner, RunnerVisibilityLink[] existingNodes) {

        // If any changes are made to the commons, we need a full refresh.
        var commonsNeedRefresh = false;

        var components = go.transform.GetComponentsInChildren<Component>(true);
        foreach (var comp in components) {
          var nodeAlreadyExists = false;

          // Check for broken/missing components
          if (comp == null) continue;
          // See if devs added a node for this behaviour already
          foreach (var existingNode in existingNodes)
            if (existingNode.Component == comp) {
              nodeAlreadyExists = true;
              AddNodeToCommonLookup(existingNode);
              RegisterNode(existingNode, runner, comp);
              commonsNeedRefresh = true;
              break;
            }

          if (nodeAlreadyExists)
            continue;

          // No existing node was found, create one if this comp is a recognized render type

          var type = comp.GetType();
          // Only add if comp is one of the behaviours considered render related.
          foreach (var recognizedType in RecognizedBehaviourTypes)
            if (IsRecognizedByRunnerVisibility(type)) {
              var node = comp.gameObject.AddComponent<RunnerVisibilityLink>();
              RegisterNode(node, runner, comp);
              break;
            }
        }

        if (commonsNeedRefresh)
          RefreshCommonObjectVisibilities();
      }

      internal static bool IsRecognizedByRunnerVisibility(this System.Type type) {
        // First try the faster type based lookup
        foreach (var recognizedType in RecognizedBehaviourTypes) {
          if (recognizedType.IsAssignableFrom(type))
            return true;
        }

        // The try the slower string based (for namespace references not included in the Fusion core).
        var typename = type.Name;
        foreach (var recognizedNames in RecognizedBehaviourNames) {
          if (typename.Contains(recognizedNames))
            return true;
        }

        return false;
      }
      
      private static void RegisterNode(RunnerVisibilityLink link, NetworkRunner runner, Component comp) {
// #if DEBUG
//         if (runner.GetVisibilityNodes().Contains(node))
//           Log.Warn($"{nameof(RunnerVisibilityNode)} on '{node.name}' already has been registered.");
// #endif

        var listnode = runner.GetVisibilityNodes().AddLast(link);
        link.Initialize(comp, runner, listnode);
      }

      public static void UnregisterNode(this RunnerVisibilityLink link) {

        if (link == null || link._runner == null) {
          return;
        }

        var runner                  = link._runner;
        var runnerIsNullOrDestroyed = !(runner);

        if (!runnerIsNullOrDestroyed) {
          var visNodes = link._runner.GetVisibilityNodes();
          if (visNodes == null) {
            // No VisibilityNodes collection, likely a shutdown condition.
            return;
          } 
        }

        if (runnerIsNullOrDestroyed == false && runner.GetVisibilityNodes().Contains(link)) {
          runner.GetVisibilityNodes().Remove(link);
        }

        // // Remove from the Runner list.
        // if (!ReferenceEquals(node, null) && node._node != null && node._node.List != null) {
        //   node._node.List.Remove(node);
        // }

        if (link.Guid != null) {

          if (CommonObjectLookup.TryGetValue(link.Guid, out var clones)) {
            if (clones.Contains(link)) {
              clones.Remove(link);
            }

            // if this is the last instance of this _guid... remove the entry from the lookup.
            if (clones.Count == 0) {
              CommonObjectLookup.Remove(link.Guid);
            }
          }
        }
      }


      private static void AddNodeToCommonLookup(RunnerVisibilityLink link) {
        var guid = link.Guid;
        if (string.IsNullOrEmpty(guid))
          return;

        if (!CommonObjectLookup.TryGetValue(guid, out var clones)) {
          clones = new List<RunnerVisibilityLink>();
          CommonObjectLookup.Add(guid, clones);
        }
        clones.Add(link);
      }
      
      /// <summary>
      /// Reapplies a runner's IsVisibile setting to all of its registered visibility nodes.
      /// </summary>
      /// <param name="runner"></param>
      /// <param name="refreshCommonObjects"></param>
      private static void RefreshRunnerVisibility(NetworkRunner runner, bool refreshCommonObjects = true) {

        // Trying to refresh before the runner has setup.
        if (runner.GetVisibilityNodes() == null) {
          //Log.Warn($"{nameof(NetworkRunner)} visibility can't be changed. Not ready yet.");
          return;
        }

        bool enable = runner.GetVisible();

        foreach (var node in runner.GetVisibilityNodes()) {

          // This should never be null, but just in case...
          if (node == null) {
            continue;
          }
          node.SetEnabled(enable);
        }
        if (refreshCommonObjects) {
          RefreshCommonObjectVisibilities();
        }
      }
      
      
      /// <summary>
      /// Dictionary lookup for manually added visibility nodes (which indicates only one instance should be visible at a time), 
      /// which returns a list of nodes for a given LocalIdentifierInFile.
      /// </summary>
      [StaticField]
      private readonly static Dictionary<string, List<RunnerVisibilityLink>> CommonObjectLookup = new Dictionary<string, List<RunnerVisibilityLink>>();

      
      internal static void RefreshCommonObjectVisibilities() {
        var runners = NetworkRunner.GetInstancesEnumerator();
        NetworkRunner serverRunner = null;
        NetworkRunner clientRunner = null;
        NetworkRunner inputAuthority = null;

        // First find the runner for each preference.
        while (runners.MoveNext()) {
          var runner = runners.Current;
          // Exclude inactive runners TODO: may not be needed after this list is patched to contain only active
          if (!runner.IsRunning || !runner.GetVisible() || runner.IsShutdown)
            continue;

          if (runner.IsServer)
            serverRunner = runner;
          if (!clientRunner && runner.IsClient) {
            clientRunner = runner;
          }
          if (!inputAuthority && runner.ProvideInput) {
            inputAuthority = runner;
          }
        }

        // If the preferred runner isn't available for some types, pick a runner as a fallback.
        if (!serverRunner)
          serverRunner = inputAuthority ? inputAuthority : clientRunner;

        if (!(clientRunner))
          clientRunner = (serverRunner) ? serverRunner : inputAuthority;

        if (!(inputAuthority))
          inputAuthority = (serverRunner) ? serverRunner : clientRunner;

        // loop all common objects, making sure to activate only one peer instance.
        foreach (var kvp in CommonObjectLookup) {
          var clones = kvp.Value;
          if (clones.Count > 0) {
            NetworkRunner prefRunner;
            switch (clones[0].PreferredRunner) {
              case RunnerVisibilityLink.PreferredRunners.Server:
                prefRunner = serverRunner;
                break;
              case RunnerVisibilityLink.PreferredRunners.Client:
                prefRunner = clientRunner;
                break;
              case RunnerVisibilityLink.PreferredRunners.InputAuthority:
                prefRunner = inputAuthority;
                break;
              default:
                prefRunner = null;
                break;
            }

            foreach (var clone in clones) {
              clone.Enabled = ReferenceEquals(clone._runner, prefRunner);
            }
          }
        }
      }
      
      [StaticFieldResetMethod]
      internal static void ResetStatics() {
        CommonObjectLookup.Clear();
      }
    }
}


#endregion

#endif
