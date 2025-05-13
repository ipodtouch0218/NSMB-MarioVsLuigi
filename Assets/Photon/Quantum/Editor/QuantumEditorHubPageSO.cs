namespace Quantum.Editor {
  using System.Collections.Generic;
  using UnityEngine;

  [CreateAssetMenu(fileName = "QuantumEditorHubPage", menuName = "Quantum/Editor/Quantum Editor Hub Page")]
  public class QuantumEditorHubPageSO : ScriptableObject {
    public List<QuantumEditorHubPage> Content;
  }
}
