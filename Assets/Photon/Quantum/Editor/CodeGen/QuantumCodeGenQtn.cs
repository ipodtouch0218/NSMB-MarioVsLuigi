namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.ComponentModel;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using CodeGen;
  using Photon.Deterministic;
  using UnityEditor;

  public static class QuantumCodeGenQtn {
    /// <summary>
    /// The Quantum CodeGen version that the SDK expects. This is used to show a hint to restart UnityEditor during upgrading for example.
    /// </summary>
    public const string VersionFilepath = "Assets/Photon/Quantum/Editor/CodeGen/QuantumCodeGenQtn.Version.txt";

    public static void Run() {
      Run(verbose: false);
    }
    
    [MenuItem("Tools/Quantum/CodeGen/Run Qtn CodeGen", priority = QuantumCodeGenSettings.MenuPriority + 1)]
    static void MenuItemRunNonVerbose() {
      Run(false);
    }
    
    [MenuItem("Tools/Quantum/CodeGen/Run Qtn CodeGen (verbose)", priority = QuantumCodeGenSettings.MenuPriority + 2)]
    static void MenuItemRunVerbose() {
      Run(true);
    }
    
    public static void Run(string[] qtnFiles, bool verbose) {
      Run(qtnFiles, verbose, null);
    }

    public static void Run(string[] qtnFiles, bool verbose, GeneratorOptions options) {
      if (qtnFiles == null) {
        throw new ArgumentNullException(nameof(qtnFiles));
      }

      VerifyVersion(true);

      Action<string> logVerbose = verbose ? x => QuantumEditorLog.LogCodeGen(x) : (Action<string>)null;

      if (verbose) {
        logVerbose?.Invoke($"Starting Qtn CodeGen");
        foreach (var file in qtnFiles) {
          logVerbose?.Invoke($"Found {file}");
        }
      }

      var stopwatch = Stopwatch.StartNew();

      if (Directory.Exists(Path.GetDirectoryName(QuantumCodeGenSettings.CodeGenQtnFolderPath)) == false) {
        Directory.CreateDirectory(Path.GetDirectoryName(QuantumCodeGenSettings.CodeGenQtnFolderPath));
      }
      
      var outputFolder = QuantumCodeGenSettings.CodeGenQtnFolderPath;
      var unityOutputFolder = QuantumCodeGenSettings.CodeGenUnityRuntimeFolderPath;
      
      // since this might log from a different thread, make sure editor log is initialized properly
      QuantumEditorLog.EnsureInitialized();
      
      IEnumerable<GeneratorOutputFile> outputFiles;
      try {
        outputFiles = Generator.Generate(qtnFiles, options ?? QuantumCodeGenSettings.Options, warning => {
          string msg = "";
          if (!string.IsNullOrEmpty(warning.Path)) {
            msg += $"{warning.Path}({warning.Position}): ";
          }
          msg += warning.Message;
          if (!string.IsNullOrEmpty(warning.SourceCode)) {
            msg += $"\n{warning.SourceCode}";
          }
          QuantumEditorLog.WarnCodeGen(msg);
        });
      } catch (Exception ex) {
        // it seems that attempting to inspect StackTrace of the exception leads to a crash in Unity;
        // throwing like this will clear the original stack trace
        // ReSharper disable once PossibleIntendedRethrow
        throw ex;
      }
      
      if (outputFolder == unityOutputFolder) {
        UpdateScriptsDirectory(outputFolder, outputFiles, logVerbose);  
      } else {
        
        bool IsUnitySpecific(GeneratorOutputFileKind kind) {
          switch (kind) {
            case GeneratorOutputFileKind.UnityPrototypeAdapters:
            case GeneratorOutputFileKind.UnityPrototypeWrapper:
            case GeneratorOutputFileKind.UnityLegacyAssetBase:
              return true;
            default:
              return false;
          }
        }

        var groups = outputFiles.ToLookup(x => IsUnitySpecific(x.Kind));

        UpdateScriptsDirectory(outputFolder, groups[false], logVerbose);
          
        if (!string.IsNullOrEmpty(unityOutputFolder)) {
          UpdateScriptsDirectory(unityOutputFolder, groups[true], logVerbose, p => {
            if (!QuantumCodeGenSettings.IsMigrationEnabled) {
              return false;
            }
            
            if (p.StartsWith("QAsset")) {
              // TODO: remove after all the internal samples have been migrated
              return true; // early v3 style
            }

            // wrappers for components and wrappers
            if (!p.StartsWith("EntityComponent") && p.EndsWith("Asset.cs")) {
              return true;
            }
            // wrappers for generic assets
            if (p == "GenericAssets.cs") {
              return true;
            }
            

            return false;
          });
        }
      }
      
      QuantumEditorLog.LogCodeGen($"Finished Qtn CodeGen in {stopwatch.Elapsed}");
      AssetDatabase.Refresh();
    }

    public static void Run(bool verbose, GeneratorOptions options) {
      var assets = AssetDatabase.FindAssets($"t:{nameof(QuantumQtnAsset)}")
       .Select(x => AssetDatabase.GUIDToAssetPath(x))
       .OrderBy(x => Path.GetFileNameWithoutExtension(x), StringComparer.OrdinalIgnoreCase)
       .ToArray();
      
      Run(assets, verbose, options);
    }
    
    public static void Run(bool verbose) {
      Run(verbose, null);
    }
    
    public static void UpdateScriptsDirectory(string outputDir, IEnumerable<GeneratorOutputFile> files, Action<string> logProgress, Predicate<string> ignoreFilter = null) {
      logProgress?.Invoke($"Generating scripts to {outputDir}");
      Directory.CreateDirectory(outputDir); // Create a directory first, because it might not exist.
      
      var generatedFiles = new HashSet<string>();

      foreach (var file in files) {
        if (string.IsNullOrEmpty(file.Contents)) {
          continue;
        }

        if (string.IsNullOrEmpty(file.UserFolder) && !generatedFiles.Add(file.Name)) {
          throw new InvalidOperationException($"File already generated: {file.Name}");
        }
        UpdateFile(file.Name, file.Contents, file.FormerNames, file.UserFolder);
      }

      var orphanedFiles = Directory.GetFiles(outputDir, "*.cs")
       .Select(x => Path.GetFileName(x))
       .Where(x => !generatedFiles.Contains(Path.GetFileName(x)))
       .ToList();

      foreach (var fileName in orphanedFiles) {
        if (ignoreFilter?.Invoke(fileName) == true) {
          logProgress?.Invoke($"Ignoring " + fileName);
          continue;
        }
        
        var filePath = Path.Combine(outputDir, fileName);
        
        if (File.Exists(filePath + ".meta")) {
          File.Delete(filePath + ".meta");
        }

        logProgress?.Invoke($"Deleting " + fileName);
        File.Delete(filePath);
      }

      void UpdateFile(string fileName, string contents, IList<string> formerNames, string userFolder) {
        var outputPath = Path.Combine(string.IsNullOrEmpty(userFolder) ? outputDir : userFolder, fileName);

        if (formerNames?.Count > 0 && !File.Exists(outputPath)) {
          // find the first match
          foreach (var formerName in formerNames) {
            var formerPath = Path.Combine(outputDir, formerName);
            if (File.Exists(formerPath)) {
              logProgress?.Invoke($"{outputPath} (moved from {formerPath})");
              File.Move(formerPath, outputPath);
              if (File.Exists(formerPath + ".meta")) {
                File.Move(formerPath + ".meta", outputPath + ".meta");
              }
              break;
            }
          }
        }
        
        
        if (AreContentsTheSame(contents, outputPath)) {
          logProgress?.Invoke($"{outputPath} (Skip)");
          return;
        }

        logProgress?.Invoke($"{outputPath} (Written)");
        File.WriteAllText(outputPath, contents);
      }
      
      bool AreContentsTheSame(string contents, string path) {
        if (!File.Exists(path)) {
          return false;
        }
    
        var oldContents = File.ReadAllText(path);
        if (oldContents.Length != contents.Length) {
          return false;
        }

        ulong checksumNew, checksumOld;
        checksumNew = CRC64.Calculate(0, contents);
        checksumOld = CRC64.Calculate(0, oldContents);
        return checksumNew == checksumOld;
      }
    }

    public static void SaveVersionToFile() {
      File.WriteAllText(VersionFilepath, Generator.Version.ToString());
    }

    public static bool VerifyVersion(bool throwError) {
      if (File.Exists(VersionFilepath)) {
        int version = 0;
        try {
          var versionText = File.ReadAllText(VersionFilepath);
          version = int.Parse(versionText);
        } catch {
          return false;
        }

        if (Generator.Version != version) {
          if (throwError) {
            throw new Exception($"CodeGen version expected {version} but got {Generator.Version}. Please restart UnityEditor.");
          }

          return false;
        }
      }

      return true;
    }
  }
}