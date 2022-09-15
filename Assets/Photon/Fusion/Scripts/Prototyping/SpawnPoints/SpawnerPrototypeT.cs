
using Fusion;
using System.Collections.Generic;
using UnityEngine;

[ScriptHelp(BackColor = EditorHeaderBackColor.Steel)]
public class SpawnerPrototype<T> : SimulationBehaviour, IPlayerJoined, IPlayerLeft, ISpawned, ISceneLoadDone where T : Component, ISpawnPointPrototype {

  protected Dictionary<PlayerRef, List<NetworkObject>> _spawnedLookup = new Dictionary<PlayerRef, List<NetworkObject>>();

  public enum SpawnMethods { AutoOnNetworkStart, ByScriptOnly }
  public enum AuthorityOptions { Auto, Server, Player }

  /// <summary>
  /// Reference to the Prefab which will be used for spawning.
  /// </summary>
  [InlineHelp]
  //[UnityEngine.Serialization.FormerlySerializedAs("PlayerPrefab")]
  public NetworkObject PlayerPrefab;

  /// <summary>
  /// Selects if spawning will be automatic, or explicitly with user script.
  /// </summary>
  [InlineHelp]
  public SpawnMethods SpawnMethod;

  /// <summary>
  /// This allows players to be spawned with Player StateAuthority. Only applicable to Shared Mode.
  /// </summary>
  [InlineHelp]
  [DrawIf(nameof(_AllowClientObjects), true, DrawIfHideType.Hide)]
  [MultiPropertyDrawersFix]
  public AuthorityOptions StateAuthority;
  protected bool _AllowClientObjects {
    get {
      var config = (Runner && Runner.IsRunning) ? Runner.Config : NetworkProjectConfig.Global;
      return config.Simulation.Topology == SimulationConfig.Topologies.Shared;  
    }
  }

  protected ISpawnPointManagerPrototype<T> spawnManager;

  protected virtual void Awake() {
    spawnManager = GetComponent<ISpawnPointManagerPrototype<T>>();
  }

  public void Spawned() {

    if (SpawnMethod != SpawnMethods.AutoOnNetworkStart)
      return;

    // Only spawn in the Scene Loaded timing if we are using NetworkObject for callbacks AND are spawning client state auth.

    if (Object) {
      if (_AllowClientObjects && StateAuthority != AuthorityOptions.Server) {
        NetworkObject playerNetworkObject = TrySpawn(Runner, Runner.LocalPlayer);
        RegisterPlayerAndObject(Runner.LocalPlayer, playerNetworkObject);
      }
    }
  }

  public void SceneLoadDone() {

    if (SpawnMethod != SpawnMethods.AutoOnNetworkStart)
      return;

    // Only spawn in the Scene Loaded timing if we are using NetworkRunner for callbacks AND are spawning client state auth.

    if (Object)
      return;

    if (!_AllowClientObjects || StateAuthority == AuthorityOptions.Server)
      return;

    NetworkObject playerNetworkObject = TrySpawn(Runner, Runner.LocalPlayer);
    RegisterPlayerAndObject(Runner.LocalPlayer, playerNetworkObject);
  }

  public void PlayerJoined(PlayerRef player) {
    PlayerJoined(Runner, player);
  }

  public void PlayerLeft(PlayerRef player) {
    PlayerLeft(Runner, player);
  }

  void PlayerJoined(NetworkRunner runner, PlayerRef player) {

    if (SpawnMethod != SpawnMethods.AutoOnNetworkStart)
      return;

    // Only use PlayerJoined callback if spawning with Server Authority

    if (_AllowClientObjects && StateAuthority != AuthorityOptions.Server) {
      return;
    }

    NetworkObject playerNetworkObject = TrySpawn(runner, player);
    RegisterPlayerAndObject(player, playerNetworkObject);
  }

  void PlayerLeft(NetworkRunner runner, PlayerRef player) {
    DespawnPlayersObjects(runner, player);
    UnregisterPlayer(player);
  }


  public NetworkObject TrySpawn(NetworkRunner runner, PlayerRef player) {

    if (PlayerPrefab == false || !player.IsValid) {
      return null;
    } else {

      // Try to get a spawn point from a spawn manager (if one is attached) - fallback to this components transform as the spawn point.
      Transform spawnTransform = (spawnManager != null) ? spawnManager.GetNextSpawnPoint(runner, player) : null;

      if (spawnTransform == null)
        spawnTransform = transform;

      Vector3 spawnPosition = spawnTransform.position;
      Quaternion spawnRotation = spawnTransform.rotation;

      return runner.Spawn(PlayerPrefab, spawnPosition, spawnRotation, player);
    }
  }

  [BehaviourButtonAction("Spawn For All Players On Server", true, false)]
  public void TrySpawnAll() {
    var runners = NetworkRunner.GetInstancesEnumerator();
    while (runners.MoveNext()) {
      var runner = runners.Current;
      if (runner.IsRunning && runner.IsServer) {
        foreach (var p in runner.ActivePlayers) {
          var playerNetworkObject = TrySpawn(runner, p);
          RegisterPlayerAndObject(p, playerNetworkObject);
        }
      }
    }
  }

  protected virtual void RegisterPlayerAndObject(PlayerRef player, NetworkObject playerObject) {
    if (!_spawnedLookup.TryGetValue(player, out var objList)) {
      objList = new List<NetworkObject>();
      _spawnedLookup.Add(player, objList);
    }
    if (playerObject)
      objList.Add(playerObject);

    // For AOI handling, make the player aware of its own player object in all cases.
    Runner.SetPlayerAlwaysInterested(player, playerObject, true);
  }

  protected void DespawnPlayersObjects(NetworkRunner runner, PlayerRef player) {
    if (_spawnedLookup.ContainsKey(player)) {
      var playerObjects = _spawnedLookup[player];
      if (playerObjects.Count > 0) {
        foreach (var obj in playerObjects)
          runner.Despawn(obj);
      }

      UnregisterPlayer(player);
      // TODO: May need to unregister AOI always interested.
    }
  }

  protected void UnregisterPlayer(PlayerRef player) {
    if (_spawnedLookup.ContainsKey(player))
      _spawnedLookup.Remove(player);
  }

#if UNITY_EDITOR

  [BehaviourWarn("No " + nameof(ISpawnPointManagerPrototype<T>) + " found on this GameObject. This GameObject will be used as the spawn point transform.", nameof(_spawnPointManagerMissing))]
  protected bool _spawnPointManagerMissing => TryGetComponent<ISpawnPointManagerPrototype<T>>(out var dummy) == false;

#endif

}






