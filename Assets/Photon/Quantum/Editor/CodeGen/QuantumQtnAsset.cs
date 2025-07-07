namespace Quantum.Editor {
  using UnityEngine;

  /// <summary>
  /// A text asset that stores Quantum DSL (domain specific language) code. Quantum requires components and other runtime game state
  /// data types to be declared within.
  /// Upon any of these assets changing, <see cref="QuantumCodeGenQtn.Run()"/> is called. To disable this behavior, define <code>QUANTUM_DISABLE_AUTO_CODEGEN</code>.
  /// </summary>
  public class QuantumQtnAsset : ScriptableObject {
  }
}