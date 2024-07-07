namespace Quantum.Editor {
#if UNITY_EDITOR
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  [ScriptedImporter(Quantum.CodeGen.Generator.Version, Extension)]
  public class QuantumQtnAssetImporter : ScriptedImporter {
    public const string Extension        = "qtn";
    public const string ExtensionWithDot = "." + Extension;


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