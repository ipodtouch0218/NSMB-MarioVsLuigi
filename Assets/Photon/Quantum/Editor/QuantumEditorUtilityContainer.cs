namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  internal sealed class QuantumEditorUtilityContainer : ScriptableSingleton<QuantumEditorUtilityContainer> {

    public new static QuantumEditorUtilityContainer instance {
      get {
        var result = ScriptableSingleton<QuantumEditorUtilityContainer>.instance;
        result.hideFlags = HideFlags.None;
        return result;
      }
    }

    [SerializeReference]
    public object Object;

    [SerializeReference]
    [SerializeReferenceTypePicker]
    public ComponentPrototype[] PendingComponentPrototypes = Array.Empty<ComponentPrototype>();
  }
}