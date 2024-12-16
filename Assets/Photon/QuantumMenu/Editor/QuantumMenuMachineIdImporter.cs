namespace Quantum.Editor {
  using Menu;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  /// <summary>
  /// All asset ending with .id will be tried for a <see cref="QuantumMenuMachineId"/> script.
  /// A local id is created for it that will never go into version control.
  /// </summary>
  [ScriptedImporter(1, "id")]
  public class QuantumMenuMachineIdImporter : ScriptedImporter {
    /// <inheritdoc/>
    public override void OnImportAsset(AssetImportContext ctx) {
      var mainAsset = ScriptableObject.CreateInstance<QuantumMenuMachineId>();
      if (mainAsset != null) {
        //mainAsset.Id = System.Guid.NewGuid().ToString();
        // Random readable code should be enough and it's readable.
        mainAsset.Id = QuantumMenuPartyCodeGenerator.Create(8, "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789");
        ctx.AddObjectToAsset("root", mainAsset);
      }
    }
  }
}
