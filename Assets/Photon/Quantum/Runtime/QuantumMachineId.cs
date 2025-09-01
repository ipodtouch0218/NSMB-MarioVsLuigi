namespace Quantum {
  using System;
  using System.Text;
  using UnityEngine;

  /// <summary>
  /// A scriptable object that has an id used by the sample menus as a unique AppVersion.
  /// Mostly a development feature to ensure to only match compatible clients in the Photon matchmaking.
  /// Reimporting this asset will generate new <see cref="Bytes"/> and thus a new ids.
  /// </summary>
  //[CreateAssetMenu(menuName = "Quantum/MachineId")]
  [ScriptHelp(BackColor = ScriptHeaderBackColor.Blue)]
  public class QuantumMachineId : QuantumScriptableObject {
    /// <summary>
    /// A list of valid characters to generate unique readable ids.
    /// </summary>
    [InlineHelp] public string ReadableCharacters;
    /// <summary>
    /// Random data set during importing the id asset inside QuantumMachineIdImporter/>
    /// </summary>
    [InlineHelp, ReadOnly] public Byte[] Bytes;
    /// <summary>
    /// An id that should be unique to this machine, used by the demo menus as AppVersion.
    /// An explicit asset importer is used to create local ids during import (see QuantumMachineIdImporter).
    /// </summary>
    public string AppVersion => CreateReadableId(8, 21);

    /// <summary>
    /// Generate a deterministic and readable id using the <see cref="ReadableCharacters"/> and the random data <see cref="Bytes"/>.
    /// </summary>
    /// <param name="length">Length of the id.</param>
    /// <param name="offset">The bit offset in the random data.</param>
    /// <returns>A random id that only changes when <see cref="Bytes"/> changes, e.g this asset is reimported.</returns>
    /// <exception cref="ArgumentException">Too many valid characters.</exception>
    public string CreateReadableId(int length, int offset) {
      if (ReadableCharacters.Length > byte.MaxValue) {
        throw new ArgumentException($"ValidCharacters cannot be larger than {byte.MaxValue} characters.");
      }

      offset = offset % Bytes.Length;

      var bits = Mathf.RoundToInt(Mathf.Sqrt(ReadableCharacters.Length));
      
      var stringBuilder = new StringBuilder();
      var ptr = 0;
      for (int i = 0; i < length; i++) {
        var p = ((ptr >> 3) + offset) % Bytes.Length;
        var bitsUsed = ptr % 8;
        var value = 0;

        if (bitsUsed == 0 && bits == 8) {
          value = Bytes[p];
        } else {
          int first = Bytes[p] >> bitsUsed;
          int remainingBits = bits - (8 - bitsUsed);

          if (remainingBits < 1) {
            value = (byte)(first & (0xFF >> (8 - bits)));
          } else {
            int second = Bytes[p + 1] & (0xFF >> (8 - remainingBits));
            value = (byte)(first | (second << (bits - remainingBits)));
          }
        }

        stringBuilder.Append(ReadableCharacters[value % ReadableCharacters.Length]);

        ptr += bits;
      }

      return stringBuilder.ToString();
    }
  }
}
