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

          FusionEditorGUI.InjectPropertyDrawers(extraDataSerializedObject);
          FusionEditorGUI.ScriptPropertyField(extraDataSerializedObject);

          VersionInfoGUI(); 

          using (new EditorGUI.DisabledScope(HasModified())) {
            if (GUILayout.Button("Rebuild Prefab Table")) {
              NetworkProjectConfigUtilities.RebuildPrefabTable();
            }
          }

          FusionEditorGUI.DrawDefaultInspector(extraDataSerializedObject, drawScript: false);
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

      if (assetTarget != null) {
        for (int i = 0; i < extraDataTargets.Length; ++i) {
          var importer = GetImporter(i);
          var wrapper = GetConfigWrapper(i);

          importer.PrefabAssetsContainerPath = wrapper.PrefabAssetsContainerPath;
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
        extra.PrefabAssetsContainerPath = importer.PrefabAssetsContainerPath;
        extra.Prefabs = AssetDatabase.LoadAllAssetsAtPath(importer.assetPath)
          .OfType<NetworkPrefabSourceUnityBase>()
          .ToArray();

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