namespace Quantum.Editor {
  using System.IO;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  [ScriptedImporter(0, "editorconfig")]
  class QuantumEditorConfigImporter : ScriptedImporter {
    public override void OnImportAsset(AssetImportContext ctx) {
      var path      = ctx.assetPath;
      var contents  = File.ReadAllText(path);
      
      // create internal text asset for convenience
      var mainAsset = new TextAsset(contents);
      ctx.AddObjectToAsset("main", mainAsset);
      ctx.SetMainObject(mainAsset);

      // write the actual editorconfig for editors to consume
      var editorConfigPath = Path.Combine(Path.GetDirectoryName(path), ".editorconfig");
      File.WriteAllText(editorConfigPath, contents);
    }
  }
}
