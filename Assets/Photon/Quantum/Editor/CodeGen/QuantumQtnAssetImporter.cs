namespace Quantum.Editor {
#if UNITY_EDITOR
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;

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
        var runCodegen = false;
        foreach (var file in importedAssets.Concat(deletedAssets))
          if (file.EndsWith(ExtensionWithDot, StringComparison.OrdinalIgnoreCase)) {
            runCodegen = true;
            break;
          }

        if (runCodegen) {
          QuantumCodeGenQtn.Run(verbose: QuantumCodeGenSettings.IsMigrationEnabled);
        }
      }
    }
#endif
  }
#endif
}