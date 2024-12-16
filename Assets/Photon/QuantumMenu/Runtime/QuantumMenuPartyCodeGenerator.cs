namespace Quantum.Menu {
  using System;
  using System.Security.Cryptography;
  using System.Text;
  using UnityEngine;

  /// <summary>
  /// Party code generator.
  /// Creates human readable random codes to be shared with other players.
  /// </summary>
  [CreateAssetMenu(menuName = "Quantum/Menu/Party Code Generator")]
  public class QuantumMenuPartyCodeGenerator : QuantumScriptableObject {
    /// <summary>
    /// Available characters for the code generation. The default setup skips O and 0 for example.
    /// </summary>
    [InlineHelp] public string ValidCharacters = "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";
    /// <summary>
    /// The length of the party code.
    /// </summary>
    [InlineHelp, Range(1, 32)] public int Length = 8;
    /// <summary>
    /// The position of the encoded region.
    /// </summary>
    [InlineHelp, Range(1, 32)] public int EncodedRegionPosition = 4;

    /// <summary>
    /// Create a random party code with default length.
    /// </summary>
    /// <returns>Random code</returns>
    public virtual string Create() {
      return Create(Length);
    }

    /// <summary>
    /// Create a party code with variable length.
    /// </summary>
    /// <param name="length">Code length</param>
    /// <returns>Random code</returns>
    public virtual string Create(int length) {
      return Create(length, ValidCharacters);
    }

    /// <summary>
    /// Creates a random party code.
    /// </summary>
    /// <param name="length">Code length</param>
    /// <param name="validCharacters">Useable characters</param>
    /// <returns>Random code</returns>
    public static string Create(int length, string validCharacters) {
      length = Math.Max(1, Math.Min(length, 128));

      // m = 238 = highest multiple of 34 in 255
      var m = Mathf.FloorToInt((255.0f / validCharacters.Length)) * validCharacters.Length;
      if (m <= 0) {
        Debug.LogError($"Number of valid character ({validCharacters.Length}) has to be less than 255.");
        return null;
      }

      var res = new StringBuilder();
      using (RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider()) {
        while (res.Length != length) {
          var bytes = new byte[8];
          provider.GetBytes(bytes);
          foreach (var b in bytes) {
            if (b >= m || res.Length == length) continue;
            var character = validCharacters[b % validCharacters.Length];
            res.Append(character);
          }
        }
      }
      return res.ToString();
    }

    /// <summary>
    /// Checks if a party code is valid.
    /// </summary>
    /// <param name="code">Code</param>
    /// <returns>True, if the party code consists of the desired length and characters</returns>
    public virtual bool IsValid(string code) {
      return IsValid(code, Length);
    }

    /// <summary>
    /// Checks if a party code is valid.
    /// </summary>
    /// <param name="code">Code</param>
    /// <param name="length">Variable length</param>
    /// <returns>True, if the party code consists of the desired length and characters</returns>
    public virtual bool IsValid(string code, int length) {
      if (string.IsNullOrEmpty(code)) {
        return false;
      }

      if (code.Length != Length) {
        return false;
      }

      for (int i = 0; i < code.Length; i++) {
        if (ValidCharacters.Contains(code[i]) == false) {
          return false;
        }
      }

      return true;
    }

    /// <summary>
    /// Substitutes one character with the region.
    /// </summary>
    /// <param name="code">Code</param>
    /// <param name="region">Region index</param>
    /// <returns>New code</returns>
    public virtual string EncodeRegion(string code, int region) {
      if (string.IsNullOrEmpty(code)) {
        return null;
      }

      if (region < 0 || region >= 32) {
        return null;
      }

      if (region >= ValidCharacters.Length) {
        return null;
      }

      var index = Math.Clamp(EncodedRegionPosition, 0, code.Length - 1);

      if (index < 0 || index >= code.Length) {
        return null;
      }

      return code.Remove(index, 1).Insert(index, ValidCharacters[region].ToString());
    }

    /// <summary>
    /// Reads the character at the position <see cref="EncodedRegionPosition"/> as a int.
    /// </summary>
    /// <param name="code">Code</param>
    /// <returns>Region index</returns>
    public virtual int DecodeRegion(string code) {
      if (string.IsNullOrEmpty(code)) {
        return -1;
      }

      var index = Math.Clamp(EncodedRegionPosition, 0, code.Length - 1);

      if (index < 0 || index >= code.Length) {
        return -1;
      }

      return ValidCharacters.IndexOf(code[index]);
    }
  }
}
