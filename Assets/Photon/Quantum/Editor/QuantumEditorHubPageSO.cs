namespace Quantum.Editor {
  using System.Collections.Generic;
  using UnityEngine;

  /// <summary>
  /// Collection of <see cref="QuantumEditorHubPage"/>.
  /// </summary>
  [CreateAssetMenu(fileName = "QuantumEditorHubPage", menuName = "Quantum/Editor/Quantum Editor Hub Page")]
  // ReSharper disable once InconsistentNaming
  public class QuantumEditorHubPageSO : ScriptableObject {
    /// <summary/>
    public List<QuantumEditorHubPage> Content;
  }
}
