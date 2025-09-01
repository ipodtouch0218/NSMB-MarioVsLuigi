namespace Quantum.Editor {
  using System.Security.Cryptography;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  /// <summary>
  /// Assets ending with .quantum-id will be tried for a <see cref="QuantumMachineId"/> script.
  /// A local id is created for it that will not go into version control and is unique for every working copy.
  /// </summary>
  [ScriptedImporter(1, "quantum-id")]
  public class QuantumMachineIdImporter : ScriptedImporter {
    /// <summary>
    /// A list of valid characters to generate unique readable ids.
    /// </summary>
    [InlineHelp, MaxStringByteCount(255, "UTF-8")] public string ReadableCharacters = "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";
    /// <summary>
    /// The number of bytes to store to randomly generate ids.
    /// </summary>
    [InlineHelp] public int RandomDataLength = 64;

    /// <inheritdoc/>
    public override void OnImportAsset(AssetImportContext ctx) {
      var mainAsset = ScriptableObject.CreateInstance<QuantumMachineId>();
      if (mainAsset != null) {
        mainAsset.Bytes = new byte[RandomDataLength];
        mainAsset.ReadableCharacters = ReadableCharacters;
        // Not truly unique but pretty random.
        // The alternative would be to use SystemInfo.deviceUniqueIdentifier and hash it with the working directory,
        // but that is not supported on Linux.
        using (RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider()) {
          provider.GetBytes(mainAsset.Bytes);
        }
        ctx.AddObjectToAsset("root", mainAsset);
      }
    }
  }
}
