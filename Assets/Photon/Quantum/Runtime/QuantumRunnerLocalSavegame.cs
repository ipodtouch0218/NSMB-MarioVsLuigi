namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  public class QuantumRunnerLocalSavegame : QuantumMonoBehaviour {
    public  TextAsset             SavegameFile;
    public  TextAsset             DatabaseFile;
    public  InstantReplaySettings InstantReplayConfig = InstantReplaySettings.Default;
    private ResourceManagerStatic _resourceManager;
    private Native.Allocator      _resourceAllocator;

    public void Start() {
      if (QuantumRunner.Default != null)
        return;

      if (SavegameFile == null) {
        Debug.LogError("QuantumRunnerLocalSavegame - not savegame file selected.");
        return;
      }

      Debug.Log("### Starting quantum in local savegame mode ###");

      // Load replay file in json or bson
      var serializer = new QuantumUnityJsonSerializer();
      var replayFile = JsonUtility.FromJson<QuantumReplayFile>(SavegameFile.text);

      var arguments = new SessionRunner.Arguments {
        RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
        GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
        RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(replayFile.RuntimeConfigData.Decode(), compressed: true),
        SessionConfig = replayFile.DeterministicConfig,
        GameMode = DeterministicGameMode.Local,
        FrameData = replayFile.InitialFrameData,
        InitialTick = replayFile.LastTick,
        RunnerId = "LOCALSAVEGAME",
        PlayerCount = replayFile.DeterministicConfig.PlayerCount,
        InstantReplaySettings = InstantReplayConfig,
      };

      var assets = replayFile.AssetDatabaseData?.Decode();
      if (DatabaseFile != null) {
        assets = DatabaseFile.bytes;
      }

      if (assets?.Length > 0) {
        _resourceAllocator = new QuantumUnityNativeAllocator();
        _resourceManager = new ResourceManagerStatic(serializer.AssetsFromByteArray(assets), new QuantumUnityNativeAllocator());
        arguments.ResourceManager = _resourceManager;
      }

      QuantumRunner.StartGame(arguments);
    }

    private void OnDestroy() {
      _resourceManager?.Dispose();
      _resourceManager = null;
      _resourceAllocator?.Dispose();
      _resourceAllocator = null;
    }
  }
}