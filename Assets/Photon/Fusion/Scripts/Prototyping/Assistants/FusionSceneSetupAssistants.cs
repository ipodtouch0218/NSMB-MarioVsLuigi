#if UNITY_EDITOR
using UnityEditor;

using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion.Editor;

namespace Fusion.Assistants {

  public static class FusionSceneSetupAssistants {


    [MenuItem("Fusion/GameObject/Setup Basic Fusion Scene", false, FusionAssistants.PRIORITY_LOW)]
    [MenuItem("GameObject/Fusion/Setup Basic Fusion Scene", false, FusionAssistants.PRIORITY)]
    public static void SetupBasicFusionScene() {

      // Create floor and Spawn Points
      var floor = FusionAssistants.CreatePrimitive(PrimitiveType.Cube, "Prototype Floor", new Vector3(0, -1, 0), null, new Vector3(20, 2, 20), null, FusionPrototypeMaterials.Floor).transform;

      // Delete any existing spawn points
      var found = UnityEngine.Object.FindObjectsOfType<PlayerSpawnPointPrototype>();
      foreach (var spawn in found) {
        GameObject.DestroyImmediate(spawn.gameObject);
      }

      // Add 5 spawn points
      FusionAssistants.CreatePrimitive(null, "Player SpawnPoint 0", new Vector3(-2, 2, 4), Quaternion.Euler(0, 160, 0), null, floor, null, typeof(PlayerSpawnPointPrototype));
      FusionAssistants.CreatePrimitive(null, "Player SpawnPoint 1", new Vector3(2, 2, 4),  Quaternion.Euler(0, 200, 0), null, floor, null, typeof(PlayerSpawnPointPrototype));
      FusionAssistants.CreatePrimitive(null, "Player SpawnPoint 2", new Vector3(4, 2, 0),  Quaternion.Euler(0, -90, 0), null, floor, null, typeof(PlayerSpawnPointPrototype));
      FusionAssistants.CreatePrimitive(null, "Player SpawnPoint 3", new Vector3(0, 2, -4), Quaternion.Euler(0, 0, 0),   null, floor, null, typeof(PlayerSpawnPointPrototype));
      FusionAssistants.CreatePrimitive(null, "Player SpawnPoint 4", new Vector3(-4, 2, 0), Quaternion.Euler(0, 90, 0),  null, floor, null, typeof(PlayerSpawnPointPrototype));

      // Add NetworkDebugRunner if missing
      var n = AddNetworkStartup();

      var nds = n.Item1;
      var nr = n.Item2;

      nr.gameObject.EnsureComponentExists<InputBehaviourPrototype>();
      AddPlayerSpawner(nr.gameObject);
      nr.gameObject.EnsureComponentExists<NetworkEvents>();

      // Set our physics to 2D
      NetworkProjectConfig.Global.PhysicsEngine = NetworkProjectConfig.PhysicsEngines.Physics3D;
      NetworkProjectConfigUtilities.SaveGlobalConfig();

      // Get scene and mark scene as dirty.
      DirtyAndSaveScene(nds.gameObject.scene);
    }

    [MenuItem("Fusion/GameObject/Setup Basic Fusion Scene 2D", false, FusionAssistants.PRIORITY_LOW)]
    [MenuItem("GameObject/Fusion/Setup Basic Fusion Scene 2D", false, FusionAssistants.PRIORITY)]
    public static void SetupBasic2DFusionScene() {

      // Create floor and Spawn Points
      var floor = GameObject.Instantiate(FusionPrototypingPrefabs.Ground2D, null).transform;

      // Delete any existing spawn points
      var found = UnityEngine.Object.FindObjectsOfType<PlayerSpawnPointPrototype>();
      foreach(var spawn in found) {
        GameObject.DestroyImmediate(spawn.gameObject);
      }

      // Add 5 spawn points
      FusionAssistants.CreatePrimitive(null, "Player SpawnPoint 0", new Vector3(-4, 2, 0), default, null, floor, null, typeof(PlayerSpawnPointPrototype));
      FusionAssistants.CreatePrimitive(null, "Player SpawnPoint 1", new Vector3(-2, 2, 0), default, null, floor, null, typeof(PlayerSpawnPointPrototype));
      FusionAssistants.CreatePrimitive(null, "Player SpawnPoint 2", new Vector3(-0, 2, 0), default, null, floor, null, typeof(PlayerSpawnPointPrototype));
      FusionAssistants.CreatePrimitive(null, "Player SpawnPoint 3", new Vector3(2, 2, 0), default, null, floor, null, typeof(PlayerSpawnPointPrototype));
      FusionAssistants.CreatePrimitive(null, "Player SpawnPoint 4", new Vector3(4, 2, 0), default, null, floor, null, typeof(PlayerSpawnPointPrototype));

      // Add NetworkDebugRunner if missing
      var n = AddNetworkStartup();

      var nds = n.Item1;
      var nr = n.Item2;
      
      nr.gameObject.EnsureComponentExists<InputBehaviourPrototype>();
      var spawner = AddPlayerSpawner(nr.gameObject);
      spawner.PlayerPrefab = FusionPrototypingPrefabs.BasicPlayerRB2D.GetComponent<NetworkObject>();
      nr.gameObject.EnsureComponentExists<NetworkEvents>();
     
      // Set our physics to 2D
      NetworkProjectConfig.Global.PhysicsEngine = NetworkProjectConfig.PhysicsEngines.Physics2D;
      NetworkProjectConfigUtilities.SaveGlobalConfig();

      // Get scene and mark scene as dirty.
      DirtyAndSaveScene(nds.gameObject.scene);
    }

    [MenuItem("Fusion/GameObject/Setup/Add Networking To Scene", false, FusionAssistants.PRIORITY_LOW)]
    [MenuItem("GameObject/Fusion/Setup/Add Networking To Scene", false, FusionAssistants.PRIORITY)]
    public static void AddNetworkingToScene() {
      (NetworkDebugStart nds, NetworkRunner nr) n = AddNetworkStartup();
      n.nr.gameObject.EnsureComponentExists<NetworkEvents>();

      // Get scene and mark scene as dirty.
      DirtyAndSaveScene(n.nds.gameObject.scene);
    }

    public static (NetworkDebugStart, NetworkRunner) AddNetworkStartup() {
      // Add Visibility node to AudioListeners to disallow multiple active in shared instance mode (preventing log spam)
      HandleAudioListeners();

      // Add NetworkDebugRunner if missing
      var nds = FusionAssistants.EnsureExistsInScene<NetworkDebugStart>("Prototype Network Start");

      NetworkRunner nr = nds.RunnerPrefab == null ? null : nds.RunnerPrefab.TryGetComponent<NetworkRunner>(out var found) ? found : null;
      // Add NetworkRunner to scene if the DebugStart doesn't have one as a prefab set already.
      if (nr == null) {

        // Add NetworkRunner to scene if NetworkDebugStart doesn't have one set as a prefab already.
        nr = FusionAssistants.EnsureExistsInScene<NetworkRunner>("Prototype Runner");

        nds.RunnerPrefab = nr;
        // The runner go is also our fallback spawn point... so raise it into the air a bit
        nr.transform.position = new Vector3(0, 3, 0);
      }

      return (nds, nr);
    }

    //[MenuItem("GameObject/Fusion/Setup/Add Player Spawner", false, FusionAssistants.PRIORITY)]
    public static void AddPlayerSpawner() { AddPlayerSpawner(null); }
    public static PlayerSpawnerPrototype AddPlayerSpawner(GameObject addTo) {
      if (addTo == null) {
        if (Selection.activeGameObject != null && Selection.activeGameObject.scene == SceneManager.GetActiveScene()) {
          addTo = Selection.activeGameObject;
        } else {
          addTo = new GameObject("Prototype Player Spawner");
          Selection.activeGameObject = addTo;
        }
      }

      var spawner = addTo.EnsureComponentExists<PlayerSpawnerPrototype>();
      addTo.EnsureComponentExists<PlayerSpawnPointManagerPrototype>();
      return spawner;
    }

    //[MenuItem("GameObject/Fusion/Setup/Add Player Spawn Point", false, FusionAssistants.PRIORITY)]
    public static void AddPlayerSpawnPoint() {
      var parent = Selection.activeGameObject ? Selection.activeGameObject.transform : null;
      var point = FusionAssistants.CreatePrimitive(null, "Player SpawnPoint", null, null, null, parent, null, typeof(PlayerSpawnPointPrototype));
      Selection.activeGameObject = point;
    }


    [MenuItem("Fusion/GameObject/Setup/Add Current Scene To Build Settings", false, FusionAssistants.PRIORITY_LOW)]
    [MenuItem("GameObject/Fusion/Setup/Add Current Scene To Build Settings", false, FusionAssistants.PRIORITY)]
    public static void AddCurrentSceneToSettings() { DirtyAndSaveScene(SceneManager.GetActiveScene()); }
    public static void DirtyAndSaveScene(Scene scene) {

      UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
      var scenename = scene.path;

      // Give chance to save - required in order to build out. If users cancel will only be able to run in the editor.
      if (scenename == "") {
        UnityEditor.SceneManagement.EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new Scene[] { scene });
        scenename = scene.path;
      }
      
      // Add scene to Build and Fusion settings
      if (scenename != "")
        scene.AddSceneToBuildSettings();
    }

    [MenuItem("Fusion/GameObject/Setup/Add AudioListener Handling", false, FusionAssistants.PRIORITY_LOW)]
    [MenuItem("GameObject/Fusion/Setup/Add AudioListener Handling", false, FusionAssistants.PRIORITY)]
    public static void HandleAudioListeners() {
      int count = 0;
      foreach (var listener in Object.FindObjectsOfType<AudioListener>()) {
        count++;
        listener.EnsureComponentHasVisibilityNode();
      }
      Debug.Log($"{count} {nameof(AudioListener)}(s) found and given a {nameof(RunnerVisibilityNode)} component.");
    }

  }
}

#endif
