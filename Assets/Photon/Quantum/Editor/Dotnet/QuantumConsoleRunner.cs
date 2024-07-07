namespace Quantum {
#if !UNITY_EDITOR
  using Photon.Deterministic;
  using Quantum.Json;
  using System.IO;
  using System;
  using Newtonsoft.Json;
  using System.Threading;

  /// <summary>
  /// Simple command-line runner for Quantum simulations.
  /// </summary>
  public class QuantumConsoleRunner {
    /// <summary>
    /// Json settings.
    /// </summary>
    public static JsonSerializerSettings JsonSettings => new JsonSerializerSettings {
      Formatting = Formatting.Indented,
      NullValueHandling = NullValueHandling.Ignore,
    };

    /// <summary>
    /// Main method to start a Quantum runner.
    /// </summary>
    /// <param name="replayPath">Path to the Quantum replay json file.</param>
    /// <param name="lutPath">Path to the LUT folder.</param>
    /// <param name="dbPath">Optionally an extra path to the Quantum database json file.</param>
    /// <param name="checksumPath">Optional an extra path to the checksum file.</param>
    [STAThread]
    public static void Main(string replayPath, string lutPath, string dbPath, string checksumPath) {
      new QuantumConsoleRunner().Run(replayPath, lutPath, dbPath, checksumPath);
    }

    /// <summary>
    /// Run the Quantum simulation from a replay.
    /// This is very similar to the QuantumRunnerLocalReplay script.
    /// </summary>
    /// <param name="replayPath">Path to the Quantum replay json file.</param>
    /// <param name="lutPath">Path to the LUT folder.</param>
    /// <param name="dbPath">Optionally an extra path to the Quantum database json file.</param>
    /// <param name="checksumPath">Optionally an extra path to the checksum file.</param>
    /// <returns></returns>
    public bool Run(string replayPath, string lutPath, string dbPath, string checksumPath) {
      Quantum.Log.InitForConsole();

      if (string.IsNullOrEmpty(replayPath)) { Log.Error("ReplayPath must be specified"); return false; }
      if (File.Exists(replayPath) == false) { Log.Error($"File not found {replayPath}"); return false; }
      if (string.IsNullOrEmpty(lutPath)) { Log.Error("LutPath must be specified"); return false; }
      if (Directory.Exists(lutPath) == false) { Log.Error($"Folder not found {lutPath}"); return false; }

      FPLut.Init(lutPath);

      var replayFile = UnityJsonUtilityConvert.DeserializeObject<QuantumReplayFile>(File.ReadAllText(replayPath));

      // Create the input from the input history either from the verbose or delta-compressed input history.
      var inputProvider = replayFile.CreateInputProvider();
      if (inputProvider == null) {
        return false;
      }

      // Load the resource manager from the replay file or from a separate file. 
      ResourceManagerStatic resourceManager;
      var serializer = new QuantumJsonSerializer();
      {
        var assetDBData = replayFile.AssetDatabaseData?.Decode();
        if (string.IsNullOrEmpty(dbPath) == false) {
          assetDBData = File.ReadAllBytes(dbPath);
        }
        if (assetDBData?.Length <= 0) {
          Log.Error("Asset Database is missing");
          return false;
        }

        resourceManager = new ResourceManagerStatic(serializer.AssetsFromByteArray(assetDBData), DotNetRunnerFactory.CreateNativeAllocator());
      }

      var arguments = new SessionRunner.Arguments {
        RunnerFactory = new DotNetRunnerFactory(),
        AssetSerializer = serializer,
        // Create own callback or event dispatcher instances here to subscribe to callbacks
        // var callbackDispatcher = new CallbackDispatcher();
        // var eventDispatcher = new EventDispatcher();
        CallbackDispatcher = null,
        EventDispatcher = null,
        ResourceManager = resourceManager,
        GameFlags = QuantumGameFlags.DisableInterpolatableStates,
        ReplayProvider = inputProvider,
        GameMode = DeterministicGameMode.Replay,
        SessionConfig = replayFile.DeterministicConfig,
        RuntimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(replayFile.RuntimeConfigData.Decode(), compressed: true),
      };

      var runner = SessionRunner.Start(arguments);

      // Start checksum checking 
      {
        var checksumFile = replayFile.Checksums;

        if (string.IsNullOrEmpty(checksumPath) == false) {
          checksumFile = JsonConvert.DeserializeObject<ChecksumFile>(File.ReadAllText(checksumPath), JsonSettings);
        }

        if (checksumFile?.Checksums != null) {
          ((QuantumGame)runner.DeterministicGame).StartVerifyingChecksums(checksumFile);
        }
      }

      // Run the replay
      while (runner.Session.FramePredicted == null || runner.Session.FramePredicted.Number < replayFile.LastTick) {
        Thread.Sleep(1);
        runner.Service(1.0f);

        if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Escape) {
          break;
        }
      }

      runner.Shutdown();
      resourceManager.Dispose();

      return true;
    }
  }
#endif
}