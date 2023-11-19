namespace Fusion.Editor {

  using System;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  [CustomEditor(typeof(NetworkProjectConfigImporter))]
  internal class NetworkProjectConfigImporterEditor : ScriptedImporterEditor {

    private Exception _initializeException;

    private static bool _versionExpanded;
    private static string _version;
    private static string _allVersionInfo;

    public override bool showImportedObject => false;

    protected override Type extraDataType => typeof(NetworkProjectConfigAsset);

    public override void OnInspectorGUI() {

      try {
        if (_initializeException != null) {
          EditorGUILayout.HelpBox(_initializeException.ToString(), MessageType.Error, true);
        } else {

          FusionEditorGUI.InjectScriptHeaderDrawer(extraDataSerializedObject);
          FusionEditorGUI.ScriptPropertyField(extraDataSerializedObject);

          VersionInfoGUI();

          using (new EditorGUI.DisabledScope(HasModified())) {
            if (GUILayout.Button("Rebuild Prefab Table")) {
              NetworkProjectConfigUtilities.RebuildPrefabTable();
            }
          }

          extraDataSerializedObject.Update();
          EditorGUILayout.PropertyField(extraDataSerializedObject.FindPropertyOrThrow(nameof(NetworkProjectConfigAsset.Config)));
          extraDataSerializedObject.ApplyModifiedProperties();

          EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(NetworkProjectConfigImporter.PrefabOptions)));
          
          EditorGUILayout.Space();

          EditorGUILayout.Space();
          EditorGUILayout.LabelField("Auto-Generated", EditorStyles.boldLabel);

          if (GUILayout.Button("Show Network Prefabs Inspector")) {
            NetworkPrefabsInspector.ShowWindow();
          }

          // WORKAROUND: during initial failed imports, this may be an instance of UnityEngine.DefaultAsset instead of the actual asset
          if (assetSerializedObject.targetObject.GetType() == typeof(NetworkProjectConfigAsset)) {
            EditorGUILayout.PropertyField(assetSerializedObject.FindPropertyOrThrow(nameof(NetworkProjectConfigAsset.Prefabs)));
            EditorGUILayout.PropertyField(assetSerializedObject.FindPropertyOrThrow(nameof(NetworkProjectConfigAsset.BehaviourMeta)));  
          } else {
            EditorGUILayout.HelpBox("Asset failed to deserialize correctly. Please reimport.", MessageType.Warning);
          }
        }
      } finally {
        ApplyRevertGUI();
      }
    }

    private static void VersionInfoGUI() {
      if (_allVersionInfo == null || _allVersionInfo == "") {
        var asms = System.AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < asms.Length; ++i) {
          var asm = asms[i];
          var asmname = asm.FullName;
          if (asmname.StartsWith("Fusion.Runtime,")) {
            _version = NetworkRunner.BuildType + ": " + System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location).ProductVersion;
          }
          if (asmname.StartsWith("Fusion.") || asmname.StartsWith("Fusion,")) {
            string fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location).ToString();
            _allVersionInfo += asmname.Substring(0, asmname.IndexOf(",")) + ": " + fvi + " " + "\n";
          }
        }
      }


      var r = EditorGUILayout.GetControlRect();
      _versionExpanded = EditorGUI.Foldout(r, _versionExpanded, "");
      EditorGUI.LabelField(r, "Fusion Version", _version);

      if (_versionExpanded) {
        EditorGUILayout.HelpBox(_allVersionInfo, MessageType.None);
      }
    }

    protected override void Apply() {
      base.Apply();

      if (targets != null) {
        for (int i = 0; i < extraDataTargets.Length; ++i) {
          var importer = GetImporter(i);
          var wrapper = GetConfigWrapper(i);
          
          EditorUtility.SetDirty(importer);

          var json = EditorJsonUtility.ToJson(wrapper.Config, true);
          File.WriteAllText(importer.assetPath, json);
        }
      }
    }

    protected override void InitializeExtraDataInstance(UnityEngine.Object extraData, int targetIndex) {
      try {
        var importer = GetImporter(targetIndex);
        var extra = (NetworkProjectConfigAsset)extraData;

        extra.Config = NetworkProjectConfigImporter.LoadConfigFromFile(importer.assetPath);

        _initializeException = null;
      } catch (Exception ex) {
        _initializeException = ex;
      }
    }

    private NetworkProjectConfigImporter GetImporter(int i) {
      return (NetworkProjectConfigImporter)targets[i];
    }

    private NetworkProjectConfigAsset GetConfigWrapper(int i) {
      return (NetworkProjectConfigAsset)extraDataTargets[i];
    }
  }
}