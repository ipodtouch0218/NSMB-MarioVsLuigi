// #define FUSION_NETWORK_SCENE_MANAGER_TRACE

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Fusion {
 

  public abstract class NetworkSceneManagerBase : Fusion.Behaviour, INetworkSceneManager {

    private static WeakReference<NetworkSceneManagerBase> s_currentlyLoading = new WeakReference<NetworkSceneManagerBase>(null);

    /// <summary>
    /// When enabled, a small info button overlays will be added to the Hierarchy Window 
    /// for each active <see cref="NetworkRunner"/> and for its associated scene.
    /// </summary>
    [InlineHelp]
    [ToggleLeft]
    [MultiPropertyDrawersFix]
    public bool ShowHierarchyWindowOverlay = true;

    private IEnumerator _runningCoroutine;
    private bool _currentSceneOutdated = false;
    private SceneRef _currentScene;

    public NetworkRunner Runner { get; private set; }


    protected virtual void OnEnable() {
#if UNITY_EDITOR
      if (ShowHierarchyWindowOverlay) {
        UnityEditor.EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowOverlay;
      }
#endif
    }

    protected virtual void OnDisable() {
#if UNITY_EDITOR
      UnityEditor.EditorApplication.hierarchyWindowItemOnGUI -= HierarchyWindowOverlay;
#endif
    }

    protected virtual void LateUpdate() {
      if (!Runner) {
        return;
      }

      // store the flag in case scene changes during the load; this supports scene toggling as well
      if (Runner.CurrentScene != _currentScene) {
        _currentSceneOutdated = true;
      }

      if (!_currentSceneOutdated || _runningCoroutine != null) {
        // busy or up to date
        return;
      }

      if (s_currentlyLoading.TryGetTarget(out var target)) {
        Assert.Check(target != this);
        if (!target) {
          // orphaned loader?
          s_currentlyLoading.SetTarget(null);
        } else {
          LogTrace($"Waiting for {target} to finish loading");
          return;
        }
      }

      var prevScene = _currentScene;
      _currentScene = Runner.CurrentScene;
      _currentSceneOutdated = false;

      LogTrace($"Scene transition {prevScene}->{_currentScene}");
      _runningCoroutine = SwitchSceneWrapper(prevScene, _currentScene);
      StartCoroutine(_runningCoroutine);
    }

    public static bool IsScenePathOrNameEqual(Scene scene, string nameOrPath) {
      return scene.path == nameOrPath || scene.name == nameOrPath;
    }

    public static bool TryGetScenePathFromBuildSettings(SceneRef sceneRef, out string path) {
      if (sceneRef.IsValid) {
        path = SceneUtility.GetScenePathByBuildIndex(sceneRef);
        if (!string.IsNullOrEmpty(path)) {
          return true;
        }
      }
      path = string.Empty;
      return false;
    }

    public virtual bool TryGetScenePath(SceneRef sceneRef, out string path) {
      return TryGetScenePathFromBuildSettings(sceneRef, out path);
    }
    
    public virtual bool TryGetSceneRef(string nameOrPath, out SceneRef sceneRef) {
      var buildIndex = FusionUnitySceneManagerUtils.GetSceneBuildIndex(nameOrPath);
      if (buildIndex >= 0) {
        sceneRef = buildIndex;
        return true;
      }
      sceneRef = default;
      return false;
    }
    
    public bool IsScenePathOrNameEqual(Scene scene, SceneRef sceneRef) {
      if (TryGetScenePath(sceneRef, out var path)) {
        return IsScenePathOrNameEqual(scene, path);
      } else {
        return false;
      }
    }

    public List<NetworkObject> FindNetworkObjects(Scene scene, bool disable = true, bool addVisibilityNodes = false) {

      var networkObjects = new List<NetworkObject>();
      var gameObjects = scene.GetRootGameObjects();
      var result = new List<NetworkObject>();

      // get all root gameobjects and move them to this runners scene
      foreach (var go in gameObjects) {
        networkObjects.Clear();
        go.GetComponentsInChildren(true, networkObjects);

        foreach (var sceneObject in networkObjects) {
          if (sceneObject.Flags.IsSceneObject()) {
            if (sceneObject.gameObject.activeInHierarchy || sceneObject.Flags.IsActivatedByUser()) {
              Assert.Check(sceneObject.NetworkGuid.IsValid);
              result.Add(sceneObject);
            }
          }
        }

        if (addVisibilityNodes) {
          // register all render related components on this gameobject with the runner, for use with IsVisible
          RunnerVisibilityNode.AddVisibilityNodes(go, Runner);
        }
      }

      if (disable) {
        // disable objects; each will be activated if there's a matching state object
        foreach (var sceneObject in result) {
          sceneObject.gameObject.SetActive(false);
        }
      }

      return result;
    }


    #region INetworkSceneManager

    void INetworkSceneManager.Initialize(NetworkRunner runner) {
      Initialize(runner);
    }

    void INetworkSceneManager.Shutdown(NetworkRunner runner) {
      Shutdown(runner);
    }

    bool INetworkSceneManager.IsReady(NetworkRunner runner) {
      Assert.Check(Runner == runner);
      if (_runningCoroutine != null) {
        return false;
      }
      if (_currentSceneOutdated) {
        return false;
      }
      if (runner.CurrentScene != _currentScene) {
        return false;
      }
      return true;
    }

    #endregion

    protected virtual void Initialize(NetworkRunner runner) {
      Assert.Check(!Runner);
      Runner = runner;
    }

    protected virtual void Shutdown(NetworkRunner runner) {
      Assert.Check(Runner == runner);

      try {
        // ongoing loading, dispose
        if (_runningCoroutine != null) {
          LogWarn($"There is an ongoing scene load ({_currentScene}), stopping and disposing coroutine.");
          StopCoroutine(_runningCoroutine);
          (_runningCoroutine as IDisposable)?.Dispose();
        }
      } finally {
        Runner = null;
        _runningCoroutine = null;
        _currentScene = SceneRef.None;
        _currentSceneOutdated = false;
      }
    }

    protected delegate void FinishedLoadingDelegate(IEnumerable<NetworkObject> sceneObjects);

    protected abstract IEnumerator SwitchScene(SceneRef prevScene, SceneRef newScene, FinishedLoadingDelegate finished);

    [System.Diagnostics.Conditional("FUSION_NETWORK_SCENE_MANAGER_TRACE")]
    protected void LogTrace(string msg) {
      Log.Debug($"[NetworkSceneManager] {(this != null ? this.name : "<destroyed>")}: {msg}");
    }

    protected void LogError(string msg) {
      Log.Error($"[NetworkSceneManager] {(this != null ? this.name : "<destroyed>")}: {msg}");
    }

    protected void LogWarn(string msg) {
      Log.Warn($"[NetworkSceneManager] {(this != null ? this.name : "<destroyed>")}: {msg}");
    }


    private IEnumerator SwitchSceneWrapper(SceneRef prevScene, SceneRef newScene) {
      bool finishCalled = false;
      Dictionary<Guid, NetworkObject> sceneObjects = new Dictionary<Guid, NetworkObject>();
      Exception error = null;
      FinishedLoadingDelegate callback = (objects) => {
        finishCalled = true;
        foreach (var obj in objects) {
          sceneObjects.Add(obj.NetworkGuid, obj);
        }
      };

      try {
        Assert.Check(!s_currentlyLoading.TryGetTarget(out _));
        s_currentlyLoading.SetTarget(this);
        Runner.InvokeSceneLoadStart();
        var coro = SwitchScene(prevScene, newScene, callback);

        for (bool next = true; next;) {
          try {
            next = coro.MoveNext();
          } catch (Exception ex) {
            error = ex;
            break;
          }

          if (next) {
            yield return coro.Current;
          }
        }
      } finally {
        Assert.Check(s_currentlyLoading.TryGetTarget(out var target) && target == this);
        s_currentlyLoading.SetTarget(null);

        LogTrace($"Coroutine finished for {newScene}");
        _runningCoroutine = null;
      }

      if (error != null) {
        LogError($"Failed to switch scenes: {error}");
      } else if (!finishCalled) {
        LogError($"Failed to switch scenes: SwitchScene implementation did not invoke finished delegate");
      } else {
        Runner.RegisterSceneObjects(sceneObjects.Values);
        Runner.InvokeSceneLoadDone();
      }
    }

#if UNITY_EDITOR
    private static Lazy<GUIStyle> s_hierarchyOverlayLabelStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(UnityEditor.EditorStyles.miniButton);
      result.alignment = TextAnchor.MiddleCenter;
      result.fontSize = 9;
      result.padding = new RectOffset(4, 4, 0, 0);
      result.fixedHeight = 13f;
      return result;
    });

    private void HierarchyWindowOverlay(int instanceId, Rect position) {
      if (!Runner) {
        return;
      }

      if (!Runner.MultiplePeerUnityScene.IsValid()) {
        return;
      }

      if (Runner.MultiplePeerUnityScene.GetHashCode() == instanceId) {

        var rect = new Rect(position) {
          xMin = position.xMax - 56,
          xMax = position.xMax - 2,
          yMin = position.yMin + 1,
        };

        if (GUI.Button(rect, $"{Runner.Mode} {(Runner.LocalPlayer.IsValid ? "P" + Runner.LocalPlayer.PlayerId.ToString() : "")}", s_hierarchyOverlayLabelStyle.Value)) {
          UnityEditor.EditorGUIUtility.PingObject(Runner);
          UnityEditor.Selection.activeGameObject = Runner.gameObject;
        }
      }
    }

#endif
  }
}
