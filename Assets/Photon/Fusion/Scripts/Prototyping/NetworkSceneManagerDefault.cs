// #define FUSION_NETWORK_SCENE_MANAGER_TRACE
// #define FUSION_USE_ADDRESSABLES

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

#if FUSION_USE_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace Fusion {
  
  public class NetworkSceneManagerDefault : NetworkSceneManagerBase {

    [Header("Single Peer Options")]
    public int PostLoadDelayFrames = 1;
    
    protected virtual YieldInstruction LoadSceneAsync(SceneRef sceneRef, LoadSceneParameters parameters, Action<Scene> loaded) {

      if (!TryGetScenePath(sceneRef, out var scenePath)) {
        throw new InvalidOperationException($"Not going to load {sceneRef}: unable to find the scene name");
      }

#if FUSION_USE_ADDRESSABLES
      if (TryFindAddress(sceneRef, out var address)) {
        Debug.Assert(address == scenePath);
        var op = Addressables.LoadSceneAsync(address, parameters);

        // to get the scene a callback is used, as it fires immediately when loading finished,
        // compared to waiting for the coroutine to resume
        op.Completed += op => {
          if (op.Status == AsyncOperationStatus.Succeeded) {
            loaded(op.Result.Scene);
          }
        };
        
        return StartCoroutine(op);
      } else 
#endif
      {

        var op = SceneManager.LoadSceneAsync(scenePath, parameters);
        Assert.Check(op);

        bool alreadyHandled = false;

        // if there's a better way to get scene struct more reliably I'm dying to know
        UnityAction<Scene, LoadSceneMode> sceneLoadedHandler = (scene, _) => {
          if (IsScenePathOrNameEqual(scene, scenePath)) {
            Assert.Check(!alreadyHandled);
            alreadyHandled = true;
            loaded(scene);
          }
        };
        SceneManager.sceneLoaded += sceneLoadedHandler;
        op.completed += _ => {
          SceneManager.sceneLoaded -= sceneLoadedHandler;
        };

        return op;
      }
    }

    protected virtual YieldInstruction UnloadSceneAsync(Scene scene) {
      return SceneManager.UnloadSceneAsync(scene);
    }

    protected override IEnumerator SwitchScene(SceneRef prevScene, SceneRef newScene, FinishedLoadingDelegate finished) {
      if (Runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Single) {
        return SwitchSceneSinglePeer(prevScene, newScene, finished);
      } else {
        return SwitchSceneMultiplePeer(prevScene, newScene, finished);
      }
    }

    protected virtual IEnumerator SwitchSceneMultiplePeer(SceneRef prevScene, SceneRef newScene, FinishedLoadingDelegate finished) {

      Scene activeScene = SceneManager.GetActiveScene();

      bool canTakeOverActiveScene = prevScene == default && IsScenePathOrNameEqual(activeScene, newScene);

      LogTrace($"Start loading scene {newScene} in multi peer mode");
      var loadSceneParameters = new LoadSceneParameters(LoadSceneMode.Additive, NetworkProjectConfig.ConvertPhysicsMode(Runner.Config.PhysicsEngine));

      var sceneToUnload = Runner.MultiplePeerUnityScene;
      var tempSceneSpawnedPrefabs = Runner.IsMultiplePeerSceneTemp ? sceneToUnload.GetRootGameObjects() : Array.Empty<GameObject>();

      if (canTakeOverActiveScene && NetworkRunner.GetRunnerForScene(activeScene) == null && SceneManager.sceneCount > 1) {
        LogTrace("Going to attempt to unload the initial scene as it needs a separate Physics stage");
        yield return UnloadSceneAsync(activeScene);
      }

      if (SceneManager.sceneCount == 1 && tempSceneSpawnedPrefabs.Length == 0) {
        // can load non-additively, stuff will simply get unloaded
        LogTrace($"Only one scene remained, going to load non-additively");
        loadSceneParameters.loadSceneMode = LoadSceneMode.Single;
      } else if (sceneToUnload.IsValid()) {
        // need a new temp scene here; otherwise calls to PhysicsStage will fail
        if (Runner.TryMultiplePeerAssignTempScene()) {
          LogTrace($"Unloading previous scene: {sceneToUnload}, temp scene created");
          yield return UnloadSceneAsync(sceneToUnload);
        }
      }

      LogTrace($"Loading scene {newScene} with parameters: {JsonUtility.ToJson(loadSceneParameters)}");

      Scene loadedScene = default;
      yield return LoadSceneAsync(newScene, loadSceneParameters, scene => loadedScene = scene);

      LogTrace($"Loaded scene {newScene} with parameters: {JsonUtility.ToJson(loadSceneParameters)}: {loadedScene}");

      if (!loadedScene.IsValid()) {
        throw new InvalidOperationException($"Failed to load scene {newScene}: async op failed");
      }

      var sceneObjects = FindNetworkObjects(loadedScene, disable: true, addVisibilityNodes: true);

      // unload temp scene
      var tempScene = Runner.MultiplePeerUnityScene;
      Runner.MultiplePeerUnityScene = loadedScene;
      if (tempScene.IsValid()) {
        if (tempSceneSpawnedPrefabs.Length > 0) {
          LogTrace($"Temp scene has {tempSceneSpawnedPrefabs.Length} spawned prefabs, need to move them to the loaded scene.");
          foreach (var go in tempSceneSpawnedPrefabs) {
            Assert.Check(go.GetComponent<NetworkObject>(), $"Expected {nameof(NetworkObject)} on a GameObject spawned on the temp scene {tempScene.name}");
            SceneManager.MoveGameObjectToScene(go, loadedScene);
          }
        }
        LogTrace($"Unloading temp scene {tempScene}");
        yield return UnloadSceneAsync(tempScene);
      }

      finished(sceneObjects);
    }

    protected virtual IEnumerator SwitchSceneSinglePeer(SceneRef prevScene, SceneRef newScene, FinishedLoadingDelegate finished) {

      Scene loadedScene;
      Scene activeScene = SceneManager.GetActiveScene();

      bool canTakeOverActiveScene = prevScene == default && IsScenePathOrNameEqual(activeScene, newScene);

      if (canTakeOverActiveScene) {
        LogTrace($"Not going to load initial scene {newScene} as this is the currently active scene");
        loadedScene = activeScene;
      } else {

        LogTrace($"Start loading scene {newScene} in single peer mode");
        LoadSceneParameters loadSceneParameters = new LoadSceneParameters(LoadSceneMode.Single);

        loadedScene = default;
        LogTrace($"Loading scene {newScene} with parameters: {JsonUtility.ToJson(loadSceneParameters)}");

        yield return LoadSceneAsync(newScene, loadSceneParameters, scene => loadedScene = scene);

        LogTrace($"Loaded scene {newScene} with parameters: {JsonUtility.ToJson(loadSceneParameters)}: {loadedScene}");

        if (!loadedScene.IsValid()) {
          throw new InvalidOperationException($"Failed to load scene {newScene}: async op failed");
        }
      }

      for (int i = PostLoadDelayFrames; i > 0; --i) {
        yield return null;
      }

      var sceneObjects = FindNetworkObjects(loadedScene, disable: true);
      finished(sceneObjects);
    }
    
#if FUSION_USE_ADDRESSABLES
    
    /// <summary>
    /// SceneRefs for scenes in <see cref="AddressableScenes"/> are equal to their index in the array plus this offset.
    /// </summary>
    [Header("Addressables")]
    [InlineHelp]
    public int AddressableSceneRefOffset = 1000;
    [ScenePath]
    public string[] AddressableScenes;
    
    public override bool TryGetScenePath(SceneRef sceneRef, out string path) {
      if (base.TryGetScenePath(sceneRef, out path)) {
        return true;
      }

      if (TryFindAddress(sceneRef, out path)) {
        return true;
      }

      return false;
    }

    public override bool TryGetSceneRef(string nameOrPath, out SceneRef sceneRef) {
      if (base.TryGetSceneRef(nameOrPath, out sceneRef)) {
        return true;
      }
      
      var index = FusionUnitySceneManagerUtils.GetSceneIndex(AddressableScenes, nameOrPath);
      if (index >= 0) {
        sceneRef = AddressableSceneRefOffset + index;
        return true;
      }

      return false;
    }
    
    private bool TryFindAddress(SceneRef sceneRef, out string address) {
      var index = (int)sceneRef;
      if (index < AddressableSceneRefOffset || index >= AddressableSceneRefOffset + AddressableScenes.Length) {
        address = null;
        return false;
      }

      address = AddressableScenes[index - AddressableSceneRefOffset];
      return true;
    }
#endif
  }
}
