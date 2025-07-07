namespace Quantum.Demo {
  using System;
  using System.Collections.Generic;
  using Photon.Deterministic;
  using Photon.Realtime;
  using UnityEngine;

  /// <summary>
  /// A Unity script that demonstrates how to connect to a Quantum cloud and start a Quantum game session.
  /// </summary>
  public class QuantumSimpleConnectionGUI : QuantumMonoBehaviour {
    /// <summary>
    /// The RuntimeConfig to use for the Quantum game session. The RuntimeConfig describes custom game properties.
    /// </summary>
    public RuntimeConfig RuntimeConfig;
    /// <summary>
    /// The RuntimePlayers to add to the Quantum game session. The RuntimePlayers describe individual custom player properties.
    /// </summary>
    public List<RuntimePlayer> RuntimePlayers;
    /// <summary>
    /// The Photon RealtimeClient object that represents the connection to the Quantum cloud.
    /// </summary>
    public RealtimeClient Client;
    /// <summary>
    /// Set this number as maximum Quantum player count. If 0, the default value is used. Default is Quantum.Input.MAX_COUNT.
    /// </summary>
    public int OverwritePlayerCount;
    /// <summary>
    /// Overwrite the AppVersion used by the global <see cref="PhotonServerSettings.AppSettings"/>.
    /// </summary>
    public string OverwriteAppVersion;

    #region OnGUI

    void OnGUI() {
      GUIStyle labelsStyle = new GUIStyle(GUI.skin.label);
      labelsStyle.fontSize = 16;

      //Client status label
      Rect labelRect = new Rect(10, 10, 400, 30);
      string clientTextInfo = "Client: ";
      if (Client == null) {
        labelsStyle.normal.textColor = Color.red;
        clientTextInfo += " not created";
      } else {
        if (Client.State == ClientState.Joined) {
          labelsStyle.normal.textColor = Color.green;
        }

        clientTextInfo += " " + Client.State;
      }

      GUI.Label(labelRect, clientTextInfo, labelsStyle);

      //Game status label
      labelRect = new Rect(10, 30, 400, 30);
      string gameTextInfo = "Quantum Runner: ";
      if (QuantumRunner.Default == null) {
        labelsStyle.normal.textColor = Color.red;
        gameTextInfo += " not running";
      } else {
        labelsStyle.normal.textColor = Color.green;
        gameTextInfo += " running";
      }

      GUI.Label(labelRect, gameTextInfo, labelsStyle);

      //Connect button
      GUIStyle buttonsStyle = new GUIStyle(GUI.skin.button);
      buttonsStyle.fontSize = 24;
      if (Client == null || !Client.IsConnectedAndReady) {
        if (GUI.Button(new Rect(10, 60, 200, 60), "Connect", buttonsStyle)) {
          StartConnection();
        }
      } else {
        //Disconnect button
        if (GUI.Button(new Rect(10, 60, 200, 60), "Disconnect", buttonsStyle)) {
          Disconnect();
        }
      }

      //StartRunner button
      GUI.enabled = Client != null && Client.IsConnectedAndReady && QuantumRunner.Default == null;
      if (GUI.Button(new Rect(10, 120, 200, 60), "Start", buttonsStyle)) {
        StartRunner();
      }
    }

    #endregion

    private void StartConnection() {
      var appSettings = new AppSettings(PhotonServerSettings.Global.AppSettings);
      if (string.IsNullOrEmpty(OverwriteAppVersion) == false) {
        appSettings.AppVersion = OverwriteAppVersion;
      }

      var connectionArguments = new MatchmakingArguments {
        PhotonSettings = appSettings,
        PluginName = "QuantumPlugin",
        MaxPlayers = OverwritePlayerCount > 0 ? Math.Min(OverwritePlayerCount, Quantum.Input.MAX_COUNT) : Quantum.Input.MAX_COUNT,
        UserId = Guid.NewGuid().ToString()
      };

      Connect(connectionArguments);
    }

    async void Connect(MatchmakingArguments connectionArguments) {
      Client = await MatchmakingExtensions.ConnectToRoomAsync(connectionArguments);
    }

    async void Disconnect() {
      if (QuantumRunner.Default != null) {
        QuantumRunner.Default.Shutdown();
      }

      await Client.DisconnectAsync();
    }

    async void StartRunner() {
      var runtimeConfig = new QuantumUnityJsonSerializer().CloneConfig(RuntimeConfig);

      var sessionRunnerArguments = new SessionRunner.Arguments {
        RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
        GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
        ClientId = Client.UserId,
        RuntimeConfig = runtimeConfig,
        SessionConfig = QuantumDeterministicSessionConfigAsset.DefaultConfig,
        GameMode = DeterministicGameMode.Multiplayer,
        PlayerCount = OverwritePlayerCount > 0 ? Math.Min(OverwritePlayerCount, Quantum.Input.MAX_COUNT) : Quantum.Input.MAX_COUNT,
        Communicator = new QuantumNetworkCommunicator(Client)
      };

      var runner = (QuantumRunner)await SessionRunner.StartAsync(sessionRunnerArguments);
      for (int i = 0; i < RuntimePlayers.Count; i++) { 
        runner.Game.AddPlayer(i, RuntimePlayers[i]);
      }
    }
  }
}
