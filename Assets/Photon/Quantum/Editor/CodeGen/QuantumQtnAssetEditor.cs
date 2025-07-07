namespace Quantum.Editor {
  using System;
  using System.IO;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.PlayerLoop;
  using Object = UnityEngine.Object;

  [CustomEditor(typeof(QuantumQtnAsset))]
  internal class QuantumQtnAssetEditor : Editor {
    private const int     MaxChars = 7000;
    
    [NonSerialized] Hash128        _lastDependencyHash;
    [NonSerialized] GUIContent     _lastContent;
    [NonSerialized] Lazy<GUIStyle> _scriptTextStyle = new Lazy<GUIStyle>(() => new GUIStyle("ScriptText"));
    
    public override void OnInspectorGUI() {
      var assetPath = AssetDatabase.GetAssetPath(target);
      var dependencyHash = AssetDatabase.GetAssetDependencyHash(assetPath);

      if (_lastContent == null || _lastDependencyHash != dependencyHash) {
        _lastContent = ReadAndTrimText(assetPath);
        _lastDependencyHash = dependencyHash;
      }

      var wasEnabled = GUI.enabled;
      try {
        GUI.enabled = true;
        
        Rect rect = GUILayoutUtility.GetRect(_lastContent, _scriptTextStyle.Value);

        // move to the top-left
        rect.y -= rect.yMin;
        rect.xMin -= rect.xMin;
        // move closer to the scrollbar
        rect.xMax += 3;
        EditorGUI.SelectableLabel(rect, _lastContent.text, _scriptTextStyle.Value);
      } finally {
        GUI.enabled = wasEnabled;
      }
    }
    
    static GUIContent ReadAndTrimText(string assetPath) {
      QuantumEditorLog.Assert(!string.IsNullOrEmpty(assetPath));
      
      string text = File.ReadAllText(assetPath);
      QuantumEditorLog.Assert(!string.IsNullOrEmpty(text));
        
      if (text.Length >= MaxChars) {
        text = text.Substring(0, MaxChars) + "...\n\n<...etc...>";
      }
      
      return new GUIContent(text);
    }
  }
}