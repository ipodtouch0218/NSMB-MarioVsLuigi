// merged MenuEditor

#region QuantumMenuUIScreenEditor.cs

namespace Quantum.Editor {
  using Menu;
  using UnityEditor;

  /// <summary>
  /// Debug QuantumMenuUIScreen content.
  /// </summary>
  [CustomEditor(typeof(QuantumMenuUIScreen), true)]
  public class QuantumMenuUIScreenEditor : Editor {
    /// <inheritdoc/>
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      var data = (QuantumMenuUIScreen)target;

      if (data.ConnectionArgs != null) {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Connect Args", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true)) {
          EditorGUILayout.TextField("Username", data.ConnectionArgs.Username);
          EditorGUILayout.TextField("Session", data.ConnectionArgs.Session);
          EditorGUILayout.TextField("PreferredRegion", data.ConnectionArgs.PreferredRegion);
          EditorGUILayout.TextField("Region", data.ConnectionArgs.Region);
          EditorGUILayout.TextField("AppVersion", data.ConnectionArgs.AppVersion);
          EditorGUILayout.TextField("Scene", data.ConnectionArgs.Scene.ScenePath);
          EditorGUILayout.IntField("MaxPlayerCount", data.ConnectionArgs.MaxPlayerCount);
          EditorGUILayout.Toggle("Creating", data.ConnectionArgs.Creating);
        }
      }
    }
  }
}

#endregion

