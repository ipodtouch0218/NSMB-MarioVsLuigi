namespace Quantum.Editor {
#if UNITY_EDITOR
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using CodeGen;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;
  using UnityEngine.Serialization;

  /// <summary>
  /// Importer for <see cref="QuantumQtnAsset"/>. Handles files with the <see cref="QuantumQtnAssetImporter.Extension"/> extension.
  /// </summary>
  [ScriptedImporter(CodeGen.Generator.Version, Extension)]
  public class QuantumQtnAssetImporter : ScriptedImporter {
    /// <summary>
    /// The extension of the Quantum Qtn asset.
    /// </summary>
    public const string Extension        = "qtn";
    internal const string ExtensionWithDot = "." + Extension;
    
    /// <summary>
    /// Use custom settings for self-contained, library modules/addons. After enabling, make sure to set <see cref="CodeGen.GeneratorOptions.LibName"/>
    /// to the library name of your choosing. The resulting code will be stripped of following:
    /// <list type="bullet">
    /// <item><description>No inputs</description></item>
    /// <item><description>No events</description></item>
    /// <item><description>No component type ids</description></item>
    /// <item><description>No globals</description></item>
    /// </list>
    /// <para/>
    /// To hook it up into the main simulation:
    /// <list type="bullet">
    /// <item><description>Use generated static classes.</description></item>
    /// <item><description>Use User partial methods.</description></item>
    /// <item><description>Import components to the main simulation's QTN.</description></item>
    /// </list>
    /// </summary>
    [InlineHelp]
    public bool UseCustomSettings;
    
    /// <summary/>
    [Header("Global settings can be changed with partial static \n" +
            "methods of " + nameof(QuantumCodeGenSettings) + ".\n")]
    [DirectoryPath]
    public string OutputFolder = QuantumCodeGenSettings.CodeGenQtnFolderPath;
    
    /// <summary>
    /// Folder path for Unity-side generated files.
    /// </summary>
    [DirectoryPath]
    [InlineHelp]
    public string UnityOutputFolderPath = QuantumCodeGenSettings.CodeGenUnityRuntimeFolderPath;

    /// <summary>
    /// If true, any file (other than .meta) from either output folders will be removed if it was not generated.
    /// </summary>
    [InlineHelp]
    public bool DeleteOrphanedFiles = true;
    
    /// <summary/>
    public GeneratorOptions GeneratorOptions;
    
    /// <summary>
    /// Creates a new instance of <see cref="QuantumQtnAsset"/> and sets it as the main object.
    /// </summary>
    /// <param name="ctx"></param>
    public override void OnImportAsset(AssetImportContext ctx) {
      var ast = ScriptableObject.CreateInstance<QuantumQtnAsset>();
      ctx.AddObjectToAsset("main", ast);
      ctx.SetMainObject(ast);
    }
    
#if !QUANTUM_DISABLE_AUTO_CODEGEN
    private class InternalPostprocessor : AssetPostprocessor {
      private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
        
        var runGlobalCodeGen = false;
        foreach (var file in importedAssets.Concat(deletedAssets)) {
          if (!file.EndsWith(ExtensionWithDot, StringComparison.OrdinalIgnoreCase)) {
            continue;
          }
          
          if (!(AssetImporter.GetAtPath(file) is QuantumQtnAssetImporter importer)) {
            QuantumEditorLog.ErrorCodeGen($"No importer for {file}");
            continue;
          }

          if (importer.UseCustomSettings) {
            QuantumCodeGenQtn.Run(new [] { file }, verbose: QuantumCodeGenSettings.IsMigrationEnabled, importer.GeneratorOptions, importer.OutputFolder, importer.UnityOutputFolderPath, importer.DeleteOrphanedFiles);  
          } else {
            runGlobalCodeGen = true;
          }
        }

        if (runGlobalCodeGen) {
          QuantumCodeGenQtn.Run(verbose: QuantumCodeGenSettings.IsMigrationEnabled);
        }
      }
    }
#endif
  }
#endif
}