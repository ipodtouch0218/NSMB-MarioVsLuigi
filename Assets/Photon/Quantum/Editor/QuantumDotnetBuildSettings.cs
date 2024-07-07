namespace Quantum.Editor {
  using System;
  using System.IO;
  using System.IO.Compression;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// A configuration asset to generate and build the non-Unity Quantum simualtion dll.
  /// </summary>
  [Serializable]
  [CreateAssetMenu(menuName = "Quantum/Configurations/Dotnet Build Settings", order = EditorDefines.AssetMenuPriorityConfigurations + 60)]
  [QuantumGlobalScriptableObject(DefaultPath)]
  public class QuantumDotnetBuildSettings : QuantumGlobalScriptableObject<QuantumDotnetBuildSettings> {
    /// <summary>
    /// Default path of the global asset.
    /// </summary>
    public const string DefaultPath = "Assets/QuantumUser/Editor/QuantumDotnetBuildSettings.asset";

    /// <summary>
    /// The platform to build for.
    /// </summary>
    public enum DotnetPlatform {
      /// <summary>
      /// Windows platform
      /// </summary>
      Windows,
      /// <summary>
      /// Linux platform
      /// </summary>
      Linux
    }

    /// <summary>
    /// The configuration to build the dll.
    /// </summary>
    public enum DotnetConfiguration {
      /// <summary>
      /// Release mode
      /// </summary>
      Release,
      /// <summary>
      /// Debug mode
      /// </summary>
      Debug
    }

    /// <summary>
    /// If true, opens and highlights the DLL after compilation.
    /// </summary>
    public bool ShowCompiledDllAfterBuild = true;

    /// <summary>
    /// The project settings to use for the generated csproj.
    /// </summary>
    public QuantumDotnetProjectSettings ProjectSettings;

    /// <summary>
    /// The project template to use for the generated simulation csproj.
    /// </summary>
    public TextAsset SimulationProjectTemplate;

    /// <summary>
    /// The project template to use for the generated runner csproj.
    /// </summary>
    public TextAsset RunnerProjectTemplate;

    /// <summary>
    /// The path to the base folder of the dotnet project structure relative to the Unity project folder.
    /// </summary>
    public string ProjectBasePath = "Quantum.Dotnet";
    
    /// <summary>
    /// Where to output the compiled DLL. Relative to the project folder.
    /// </summary>
    public string BinOutputPath = "bin";

    /// <summary>
    /// The path to the Photon Server SDK.
    /// </summary>
    public string PluginSdkPath = "";
    
    /// <summary>
    /// The target platform to build for.
    /// </summary>
    public DotnetPlatform TargetPlatform;
    
    /// <summary>
    /// The target configuration to build for. e.g. Debug or Release.
    /// </summary>
    public DotnetConfiguration TargetConfiguration;

    const string PluginSdkAssetPath = "Photon.Server/deploy_win/Plugins/QuantumPlugin3.0/bin/assets";
    const string PhotonServerPath = "Photon.Server/deploy_win/bin";
    const string PluginSdkLibPath = "Lib";
    const string SimulationProjectAssetDefaultPath = "Assets/Photon/Quantum/Editor/Dotnet/Quantum.Simulation.Dotnet.csproj.txt";
    const string RunnerProjectAssetDefaultPath = "Assets/Photon/Quantum/Editor/Dotnet/Quantum.Runner.Dotnet.csproj.txt";
    const string DependencyArchivePath = "Assets/Photon/Quantum/Editor/Dotnet/Quantum.Dotnet.{0}.zip";

    /// <summary>
    /// A quick check if the plugin sdk was found and its path saved.
    /// </summary>
    public bool HasCustomPluginSdk => string.IsNullOrEmpty(PluginSdkPath) == false && Directory.Exists(PluginSdkPath);

    private static string GetUnityProjectRoot {
      get {
        var currentPath = Application.dataPath;
        Debug.Assert(currentPath.EndsWith("/Assets"));
        return currentPath.Substring(0, currentPath.Length - "Assets".Length);
      }
    }
    
    /// <summary>
    /// Try to initialize ProjectSettings and ProjectTemplate when the scriptable object was created.
    /// </summary>
    private void Awake() {
      if (ProjectSettings == null) {
        QuantumDotnetProjectSettings.TryGetGlobal(out ProjectSettings);
        EditorUtility.SetDirty(this);
      }

      if (SimulationProjectTemplate == null) {
        SimulationProjectTemplate = AssetDatabase.LoadAssetAtPath<TextAsset>(SimulationProjectAssetDefaultPath);
        EditorUtility.SetDirty(this);
      }

      if (RunnerProjectTemplate == null) {
        RunnerProjectTemplate = AssetDatabase.LoadAssetAtPath<TextAsset>(RunnerProjectAssetDefaultPath);
        EditorUtility.SetDirty(this);
      }
    }

    /// <summary>
    /// Automatically search for the Photon Server SDK folder.
    /// </summary>
    public void DetectPluginSdk() {
      if (TryFindPluginSdkFolderWithPopup(ref PluginSdkPath) == false) {
        QuantumEditorLog.Warn("Plugin Sdk not found.");
      } else {
        var pluginSdkFullPath = Path.GetFullPath($"{GetUnityProjectRoot}/{PluginSdkPath}");
        QuantumEditorLog.Log("Plugin Sdk found at: " + pluginSdkFullPath);
        EditorUtility.SetDirty(this);
      }
    }

    /// <summary>
    /// Synchronize the Quantum Plugin SDK with the Unity project by exporting the LUT files and Quantum DB and building the project.
    /// </summary>
    /// <param name="settings"></param>
    public static void SynchronizePluginSdk(QuantumDotnetBuildSettings settings) {
      ExportPluginSdkData(settings);
      GenerateProject(settings);
      BuildProject(settings, Path.GetFullPath($"{settings.PluginSdkPath}/{PluginSdkLibPath}"), disablePopup: true);
    }

    /// <summary>
    /// Export the LUT files and Quantum DB to the Quantum Plugin SDK.
    /// </summary>
    /// <param name="settings"></param>
    public static void ExportPluginSdkData(QuantumDotnetBuildSettings settings) {
      ExportLutFiles(Path.GetFullPath($"{settings.PluginSdkPath}/{PluginSdkAssetPath}"));
      ExportQuantumDb(Path.GetFullPath($"{settings.PluginSdkPath}/{PluginSdkAssetPath}/db.json"));
    }

    /// <summary>
    /// Generate a csproj file from the ProjectSettings and ProjectTemplate.
    /// </summary>
    /// <param name="settings">Settings instance</param>
    public static void GenerateProject(QuantumDotnetBuildSettings settings) {
      if (RunDotnetCommand("--version", out var output) == false) {
        QuantumEditorLog.Error("Dotnet installation not found");
        return;
      }

      Assert.Always(settings.ProjectSettings != null, "No project settings found");
      Assert.Always(settings.SimulationProjectTemplate != null, "No project template found");
      Assert.Always(settings.RunnerProjectTemplate != null, "No runner template found");

      // Create directories
      Directory.CreateDirectory($"{settings.ProjectBasePath}/Quantum.Simulation.Dotnet");
      Directory.CreateDirectory($"{settings.ProjectBasePath}/Quantum.Runner.Dotnet");

      // Export the actual file list
      settings.ProjectSettings.Export($"{settings.ProjectBasePath}/Quantum.Simulation.Dotnet/Quantum.Simulation.Dotnet.csproj.include");

      // Export the csproj templates
      var simulationProjectText = settings.SimulationProjectTemplate.text;
      simulationProjectText = simulationProjectText.Replace("[UnityProjectPath]", Path.GetRelativePath(Path.GetFullPath($"{settings.ProjectBasePath}/Quantum.Simulation.Dotnet"), GetUnityProjectRoot));
      File.WriteAllText($"{settings.ProjectBasePath}/Quantum.Simulation.Dotnet/Quantum.Simulation.Dotnet.csproj", simulationProjectText);

      var runnerProjectText = settings.RunnerProjectTemplate.text;
      runnerProjectText = runnerProjectText.Replace("[UnityProjectPath]", Path.GetRelativePath(Path.GetFullPath($"{settings.ProjectBasePath}/Quantum.Runner.Dotnet"), GetUnityProjectRoot));
      File.WriteAllText($"{settings.ProjectBasePath}/Quantum.Runner.Dotnet/Quantum.Runner.Dotnet.csproj", runnerProjectText);

      // Extract zip folders
      ZipFile.ExtractToDirectory(string.Format(DependencyArchivePath, "Debug"), $"{settings.ProjectBasePath}/Lib/Debug", true);
      ZipFile.ExtractToDirectory(string.Format(DependencyArchivePath, "Release"), $"{settings.ProjectBasePath}/Lib/Release", true);

      // Create the solution file
      if (File.Exists($"{settings.ProjectBasePath}/{settings.ProjectBasePath}.sln") == false) {
        RunDotnetCommand($" new sln --output {settings.ProjectBasePath}", out output);
        RunDotnetCommand($" sln {settings.ProjectBasePath}/{settings.ProjectBasePath}.sln add {settings.ProjectBasePath}/Quantum.Simulation.Dotnet", out output);
        RunDotnetCommand($" sln {settings.ProjectBasePath}/{settings.ProjectBasePath}.sln add {settings.ProjectBasePath}/Quantum.Runner.Dotnet", out output);
      }
    }

    /// <summary>
    /// Run dotnet build on the generated csproj.
    /// </summary>
    /// <param name="settings">Settings instance</param>
    /// <param name="copyOutputDir">Copy result to output dir</param>
    /// <param name="disablePopup">Disable file explorer popup</param>
    public static void BuildProject(QuantumDotnetBuildSettings settings, string copyOutputDir = null, bool disablePopup = false) {
      if (RunDotnetCommand("--version", out var output) == false) {
        QuantumEditorLog.Error("Dotnet installation not found");
        return;
      }

      var arguments = $" build {Path.GetFullPath(settings.ProjectBasePath)}/Quantum.Simulation.Dotnet/Quantum.Simulation.Dotnet.csproj";
      arguments += $" --configuration {settings.TargetConfiguration}";
      arguments += $" --property:TargetPlatform={settings.TargetPlatform}";
      arguments += $" --property:OutputPath={settings.BinOutputPath}/";
      if (string.IsNullOrEmpty(copyOutputDir) == false) {
        arguments += $" --property:CopyOutput=true";
        arguments += $" --property:CopyOutputDir={copyOutputDir}";
      }

      if (RunDotnetCommand(arguments, out output)) {
        if (settings.ShowCompiledDllAfterBuild && disablePopup == false) {
          var simulationDllPath = $"{Path.GetFullPath(settings.ProjectBasePath)}/Quantum.Simulation.Dotnet/bin/Quantum.Simulation.dll";
          if (File.Exists(simulationDllPath)) {
            EditorUtility.RevealInFinder(simulationDllPath);
          }
        }
      }
    }

    private static bool RunDotnetCommand(string arguments, out string output) {
      var startInfo = new System.Diagnostics.ProcessStartInfo() {
        FileName = "dotnet",
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardError = true,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        CreateNoWindow = true
      };
      var p = new System.Diagnostics.Process { StartInfo = startInfo };
      p.Start();
      output = p.StandardOutput.ReadToEnd();
      p.WaitForExit();
      if (p.ExitCode != 0) {
        QuantumEditorLog.Error(output);
      }
      return p.ExitCode == 0;
    }

    /// <summary>
    /// Attempts to find the Photon Server SDK folder. If not found, opens a folder selection dialog.
    /// </summary>
    /// <param name="result">Plugin SDK path</param>
    /// <returns>True when the directory has been found.</returns>
    public static bool TryFindPluginSdkFolderWithPopup(ref string result) {
      if (string.IsNullOrEmpty(result) && TryFindPluginSdkFolder(out result) == false) {
        result = EditorUtility.OpenFolderPanel("Search Quantum Plugin Sdk Directory", Application.dataPath,
          "Photon.Server");
      }

      if (string.IsNullOrEmpty(result)) {
        result = null;
        return false;
      } else {
        result = PathUtils.Normalize(Path.GetRelativePath(GetUnityProjectRoot, result));
        return true;
      }
    }

    /// <summary>
    /// Searching for a folder with the subfolder called Photon.Server inside the unity project and max one above.
    /// </summary>
    /// <param name="result">Plugin SDK path</param>
    /// <returns>True when the Photon.Server directory marked folder can be found automatically.</returns>
    public static bool TryFindPluginSdkFolder(out string result) {
      var currentDirectoryPath = Path.GetFullPath($"{Application.dataPath}");
      var maxDepth = 2;

      for (var i = 0; i < maxDepth; i++) {
        currentDirectoryPath = Path.GetFullPath($"{currentDirectoryPath}/..");
        foreach (var d1 in Directory.GetDirectories(currentDirectoryPath)) {
          foreach (var d2 in Directory.GetDirectories(d1)) {
            if (d2.EndsWith("Photon.Server")) {
              result = d1;
              return true;
            }
          }
        }
      }

      result = null;
      return false;
    }

    /// <summary>
    /// Export the LUT files to the destination path.
    /// </summary>
    /// <param name="destinationPath">The path to export the files.</param>
    public static void ExportLutFiles(string destinationPath) {
      var assetDirectory = Directory.CreateDirectory(destinationPath);
      var assetPath = assetDirectory.FullName;

      // copy lut files
      var lutAssetDirectory = Directory.CreateDirectory($"{assetPath}/LUT");
      var lutAssetPath = lutAssetDirectory.FullName;
      string[] lutFiles = { "FPAcos", "FPAsin", "FPAtan", "FPCos", "FPSin", "FPSinCos", "FPSqrt", "FPTan" };
      foreach (var file in lutFiles) {
        var guids = AssetDatabase.FindAssets(file);
        foreach (var guid in guids) {
          var path = AssetDatabase.GUIDToAssetPath(guid);
          try {
            File.Copy(Path.GetFullPath($"{path}"), $"{lutAssetPath}/{Path.GetFileName(path)}", true);
          } catch (IOException e) {
            QuantumEditorLog.Error(e);
          }

          break;
        }
      }
    }

    /// <summary>
    /// Export the Quantum DB to the destination path.
    /// </summary>
    /// <param name="destinationPath">The path to export the files.</param>
    public static void ExportQuantumDb(string destinationPath) {
      Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
      QuantumUnityDBUtilities.ExportAsJson(destinationPath);
    }

    /// <summary>
    /// Launches PhotonServer.exe from the Plugin SDK folder.
    /// </summary>
    public void LaunchPhotonServer() {
      if (HasCustomPluginSdk == false) {
        QuantumEditorLog.Error("No custom Plugin SDK found.");
        return;
      }

      var arguments = "--run LoadBalancing --config PhotonServer.config";
      var path = Path.Combine(PluginSdkPath, PhotonServerPath);
      var photonServer = Path.Combine(path, "PhotonServer.exe");
      QuantumEditorLog.Log($"Launching Photon Server at: {photonServer} {arguments}");

      var startInfo = new System.Diagnostics.ProcessStartInfo() {
        FileName = "PhotonServer.exe",
        Arguments = arguments,
        WorkingDirectory = path
      };

      var p = new System.Diagnostics.Process { StartInfo = startInfo };

      p.Start();
    }


    #region Menu

    [MenuItem("Tools/Quantum/Export/Dotnet Quantum.Simulation - Generate Project", true, (int)QuantumEditorMenuPriority.Export + 22)]
    public static bool GenerateDefaultProjectCheck() => TryGetGlobal(out var settings);

    [MenuItem("Tools/Quantum/Export/Dotnet Quantum.Simulation - Generate Project", false, (int)QuantumEditorMenuPriority.Export + 22)]
    public static void GenerateDefaultProject() {
      if (TryGetGlobal(out var settings)) {
        GenerateProject(settings);
      }
    }

    [MenuItem("Tools/Quantum/Export/Dotnet Quantum.Simulation - Build", true, (int)QuantumEditorMenuPriority.Export + 22)]
    public static bool BuildDefaultProjectCheck() => TryGetGlobal(out var settings);

    [MenuItem("Tools/Quantum/Export/Dotnet Quantum.Simulation - Build", false, (int)QuantumEditorMenuPriority.Export + 22)]
    public static void BuildDefaultProject() {
      if (TryGetGlobal(out var settings)) {
        GenerateProject(settings);
        BuildProject(settings);
      }
    }

    [MenuItem("Tools/Quantum/Export/Plugin SDK - Sync Server Simulation", true, (int)QuantumEditorMenuPriority.Export + 33)]
    public static bool SynchronizePluginSdkCheck() => TryGetGlobal(out var settings) && settings.HasCustomPluginSdk;

    [MenuItem("Tools/Quantum/Export/Plugin SDK - Sync Server Simulation", false, (int)QuantumEditorMenuPriority.Export + 33)]
    public static void SynchronizePluginSdk() {
      if (TryGetGlobal(out var settings)) {
        SynchronizePluginSdk(settings);
      }
    }

    [MenuItem("Tools/Quantum/Export/Plugin SDK - Sync Assets Only", true, (int)QuantumEditorMenuPriority.Export + 33)]
    public static bool ExportPluginSdkDataCheck() => TryGetGlobal(out var settings) && settings.HasCustomPluginSdk;

    [MenuItem("Tools/Quantum/Export/Plugin SDK - Sync Assets Only", false, (int)QuantumEditorMenuPriority.Export + 33)]
    public static void ExportPluginSdkData() {
      if (TryGetGlobal(out var settings)) {
        ExportPluginSdkData(settings);
      }
    }

    #endregion
  }
}