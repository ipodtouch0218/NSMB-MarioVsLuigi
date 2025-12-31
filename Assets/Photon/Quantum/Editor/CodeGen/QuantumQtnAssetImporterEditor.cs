namespace Quantum.Editor {
  using UnityEditor;
  using UnityEditor.AssetImporters;

  [CanEditMultipleObjects]
  [CustomEditor(typeof(QuantumQtnAssetImporter))]
  class QuantumQtnAssetImporterEditor : ScriptedImporterEditor {
    
    public override void OnInspectorGUI() {
      serializedObject.UpdateIfRequiredOrScript();

      var script = serializedObject.FindProperty("m_Script");
      var enabledProperty = serializedObject.FindProperty(nameof(QuantumQtnAssetImporter.UseCustomSettings));
      
      EditorGUI.BeginChangeCheck();
      
      using (new EditorGUI.DisabledScope(true)) {
        EditorGUILayout.PropertyField(script);
      }
      
      EditorGUILayout.PropertyField(enabledProperty);
      if (enabledProperty.boolValue) {
        SerializedProperty iterator = serializedObject.GetIterator();
        for (bool enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false) {
          if (iterator.propertyPath == script.propertyPath || iterator.propertyPath == enabledProperty.propertyPath) {
            continue;
          }
          EditorGUILayout.PropertyField(iterator, true);
        }
      }

      if (EditorGUI.EndChangeCheck()) {
        serializedObject.ApplyModifiedProperties();
      }
      
      this.ApplyRevertGUI();
    }
  }
}