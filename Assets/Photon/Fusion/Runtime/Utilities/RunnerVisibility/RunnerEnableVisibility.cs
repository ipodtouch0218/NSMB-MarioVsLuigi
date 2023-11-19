namespace Fusion {
  using System;
  using System.Collections.Generic;
  using Sockets;
  using UnityEngine;

  /// <summary>
  ///   When running in Multi-Peer mode, this component automatically will register the associated
  ///   <see cref="NetworkRunner" /> with <see cref="NetworkRunnerVisibilityExtensions" />,
  ///   and will automatically attach loaded scene objects and spawned objects with the peers visibility handling.
  /// </summary>
  [ScriptHelp(BackColor = ScriptHeaderBackColor.Sand)]
  [DisallowMultipleComponent]
  public class RunnerEnableVisibility : Behaviour, INetworkRunnerCallbacks {
    
    private void Awake() {
      var runner = GetComponentInParent<NetworkRunner>();
      if (runner) {
        // Optimistically register this as if we are running multi-peer (can't know yet)
        runner.EnableVisibilityExtension();

        // Just to be safe against double registration.
        runner.ObjectAcquired -= RunnerOnObjectAcquired;
        runner.ObjectAcquired += RunnerOnObjectAcquired;
      }
    }

    private void OnDestroy() {
      if (TryGetComponent<NetworkRunner>(out var runner)) {
        runner.DisableVisibilityExtension();
        runner.RemoveCallbacks(this);
        runner.ObjectAcquired -= RunnerOnObjectAcquired;
      }
    }

    private void RunnerOnObjectAcquired(NetworkRunner runner, NetworkObject obj) {
      if (runner.IsRunning == false) return;
      if (runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Single) {
        Destroy(this);
        return;
      }

      runner.AddVisibilityNodes(obj.gameObject);
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {
    }

    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) {
      if (runner.IsRunning == false) return;
      if (runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Single) {
        Destroy(this);
        return;
      }

      var scene = runner.SimulationUnityScene;

      if (scene.IsValid())
        foreach (var obj in scene.GetRootGameObjects())
          runner.AddVisibilityNodes(obj);
    }

    #region Unused

    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner                runner, NetworkObject  obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner               runner, NetworkObject  obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner                 runner, PlayerRef      player)                     { }
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner                   runner, PlayerRef      player)                     { }
    void INetworkRunnerCallbacks.OnInput(NetworkRunner                        runner, NetworkInput   input)                      { }
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner                 runner, PlayerRef      player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner                     runner, ShutdownReason shutdownReason) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner            runner)                                                                                        { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner       runner, NetDisconnectReason reason)                                                            { }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner               runner, NetworkRunnerCallbackArgs.ConnectRequest request,       byte[]                 token)  { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner                runner, NetAddress                               remoteAddress, NetConnectFailedReason reason) { }
    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner        runner, SimulationMessagePtr                     message)                         { }
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner           runner, List<SessionInfo>                        sessionList)                     { }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object>               data)                            { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner                runner, HostMigrationToken                       hostMigrationToken)              { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner         runner, PlayerRef                                player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner               runner) { }

    #endregion
  }
}