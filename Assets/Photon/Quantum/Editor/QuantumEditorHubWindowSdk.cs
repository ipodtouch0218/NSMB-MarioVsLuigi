namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEditor.PackageManager.Requests;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using Object = UnityEngine.Object;

  internal partial class QuantumEditorHubWindow {
    static partial void CreateWindowUser(ref QuantumEditorHubWindow window) {
      window = GetWindow<QuantumEditorHubWindowSdk>(true, "Photon Quantum Hub", true);
    }

    static partial void CheckPopupConditionUser(ref bool shouldPopup, ref int page) {
      // Installation requires popup
      if (QuantumEditorHubWindowSdk.AreImportantUserFilesInstalled == false) {
        shouldPopup = true;
        page = 0;
        return;
      }

      // Layouts requires popup
      for (int i = 0; i < Pages.Count; i++) {
        if (Pages[i].IsPopupRequired) {
          shouldPopup = true;
          page = i;
          break;
        }
      }

      // Upgrade 3.0.3
      if (HubUtils.HasGlobalScriptableObjectCached(typeof(QuantumLookupTables)) == false) {
        page = Pages.FindIndex(p => p.Title.Equals("Installation"));
        shouldPopup = page != -1;
      }
    }

    static partial void OnImportPackageCompletedUser(string packageName) {
      if (packageName == "TMP Essential Resources") {
        // Workaround uninitialized TMP text after installing TMP essential resources
        // Ask to reload current scene to fix the issue
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
          try {
            EditorSceneManager.OpenScene(SceneManager.GetActiveScene().path);
          } catch {
            // Fail silently
          }
        }
      }
    }

    /// <summary>
    /// This methods installs all user files and generates the workspace files.
    /// </summary>
    public static void InstallAllUserFiles() {
      QuantumGlobalScriptableObjectUtils.EnsureAssetExists<PhotonServerSettings>();
      QuantumGlobalScriptableObjectUtils.EnsureAssetExists<QuantumEditorSettings>();
      QuantumGlobalScriptableObjectUtils.EnsureAssetExists<QuantumLookupTables>();
      QuantumGlobalScriptableObjectUtils.EnsureAssetExists<QuantumDeterministicSessionConfigAsset>();
      QuantumGlobalScriptableObjectUtils.EnsureAssetExists<QuantumGameGizmosSettingsScriptableObject>();
      QuantumGlobalScriptableObjectUtils.EnsureAssetExists<QuantumDefaultConfigs>();
      QuantumGlobalScriptableObjectUtils.EnsureAssetExists<QuantumDotnetProjectSettings>();
      QuantumGlobalScriptableObjectUtils.EnsureAssetExists<QuantumDotnetBuildSettings>();
      QuantumGlobalScriptableObjectUtils.EnsureAssetExists<QuantumUnityDB>();
      QuantumEditorUserScriptGeneration.GenerateWorkspaceFiles();
      QuantumEditorUserScriptGeneration.GenerateUserFiles();

      if (Quantum.Input.MaxCount == 0) {
        EditorApplication.ExecuteMenuItem("Tools/Quantum/CodeGen/Run Qtn CodeGen");
      }

      if (SceneView.lastActiveSceneView != null) {
        AssetDatabaseExt.UpdateScriptingDefineSymbol("QUANTUM_XY", SceneView.lastActiveSceneView.in2DMode);
      }

      // Add qtn extension to the VS project generation
      if (EditorSettings.projectGenerationUserExtensions.Contains("qtn") == false) {
        var userExtensions = EditorSettings.projectGenerationUserExtensions;
        ArrayUtils.Add(ref userExtensions, "qtn");
        EditorSettings.projectGenerationUserExtensions = userExtensions;
      }

      if (AssetDatabase.FindAssets("t:Map").Length == 0) {
        // Create Quantum game scene
        Directory.CreateDirectory($"{QuantumEditorUserScriptGeneration.FolderPath}/Scenes");
        QuantumEditorMenuCreateScene.CreateNewQuantumScene(
          $"{QuantumEditorUserScriptGeneration.FolderPath}/Scenes/QuantumGameScene.unity",
          $"{QuantumEditorSettings.Global.DefaultNewAssetsLocation}/QuantumMap.asset",
          saveScene: true,
          addToBuildSettings: true,
          createSceneInfoAsset: true);

        if (Application.isBatchMode == false) {
          // Don't call the gui exception when coming from CI
          GUIUtility.ExitGUI();
        }
      }
    }
  }

  internal partial class QuantumEditorHubWidgetTypeDrawer {
    static partial void RegisterTypesUser(List<string> types) {
      types.Add(QuantumEditorHubWindowSdk.CustomWidgetTypes.SdkInstallationBox);
      types.Add(QuantumEditorHubWindowSdk.CustomWidgetTypes.CreateSimpleConnectionScene);
      types.Add(QuantumEditorHubWindowSdk.CustomWidgetTypes.ClearQuantumPlayerPrefs);
    }
  }

  internal partial class QuantumEditorHubConditionDrawer {
    static partial void RegisterTypesUser(List<string> types) {
      types.Add(QuantumEditorHubWindowSdk.CustomConditions.AppIdCreated);
      types.Add(QuantumEditorHubWindowSdk.CustomConditions.SdkInstalled);
    }
  }

  internal class QuantumEditorHubWindowSdk : QuantumEditorHubWindow {
    internal static class CustomWidgetTypes {
      internal const string SdkInstallationBox = "SdkInstallationBox";
      internal const string CreateSimpleConnectionScene = "CreateSimpleConnectionScene";
      internal const string ClearQuantumPlayerPrefs = "ClearQuantumPlayerPrefs";
    }

    internal static class CustomConditions {
      internal const string AppIdCreated = "AppIdCreated";
      internal const string SdkInstalled = "SdkInstalled";
    }

    public override string AppId {
      get {
        if (HubUtils.TryGetGlobalScriptableObjectCached<PhotonServerSettings>(out var global)) {
          return global.AppSettings.AppIdQuantum;
        } else {
          return string.Empty;
        }
      }
      set {
        var photonSettings = PhotonServerSettings.Global;
        photonSettings.AppSettings.AppIdQuantum = value;
        EditorUtility.SetDirty(photonSettings);
        AssetDatabase.SaveAssets();
      }
    }

    public override Object SdkAppSettingsAsset {
      get {
        HubUtils.TryGetGlobalScriptableObjectCached(out PhotonServerSettings global);
        return global;
      }
    }

    internal static bool AreImportantUserFilesInstalled {
      get {
        return HubUtils.HasGlobalScriptableObjectCached(typeof(PhotonServerSettings))
          && HubUtils.HasGlobalScriptableObjectCached(typeof(QuantumDeterministicSessionConfigAsset))
          && HubUtils.HasGlobalScriptableObjectCached(typeof(QuantumEditorSettings))
          && HubUtils.HasGlobalScriptableObjectCached(typeof(QuantumUnityDB));
      }
    }

    public override GUIStyle GetBoxStyle => HubSkin.GetStyle("SteelBox");
    public override GUIStyle GetButtonPaneStyle => HubSkin.GetStyle("ButtonPane");

    static bool _statusInstallationComplete;
    static bool _statusAnyQuantumMapFound;
    static bool _statusAppIdSetup;

    protected override bool CustomConditionCheck(QuantumEditorHubCondition condition) {
      if (condition.Value == CustomConditions.AppIdCreated) {
        return _statusAppIdSetup;
      } else if (condition.Value == CustomConditions.SdkInstalled) {
        return _statusInstallationComplete;
      }
      
      return false;
    }

    protected override void CustomDrawWidget(QuantumEditorHubPage page, QuantumEditorHubWidget widget) {
      if (widget.WidgetMode.Value == CustomWidgetTypes.CreateSimpleConnectionScene) {

        DrawButtonAction(widget.Icon, widget.Text, widget.Subtext,
          statusIcon: widget.GetStatusIcon(this),
          callback: () => {
            QuantumEditorMenuCreateScene.CreateSimpleConnectionScene(widget.Scene);
            GUIUtility.ExitGUI();
          });
      } else if (widget.WidgetMode.Value == CustomWidgetTypes.ClearQuantumPlayerPrefs) {

        DrawButtonAction(widget.Icon, widget.Text, widget.Subtext,
          statusIcon: widget.GetStatusIcon(this),
          callback: () => {
            ClearAllPlayerPrefs();
          });

      } else if (widget.WidgetMode.Value == CustomWidgetTypes.SdkInstallationBox) {
        DrawInstallationBox(widget);
      }
    }

    protected override void OnGuiHeartbeat() {
      _statusInstallationComplete = AreImportantUserFilesInstalled;
      _statusAppIdSetup = HubUtils.IsValidGuid(AppId);
      _statusAnyQuantumMapFound = AssetDatabase.FindAssets("t:Map").Length > 0;
    }

    void ClearAllPlayerPrefs() {
      // Hub
      foreach (var page in Pages) {
        page.DeleteAllPlayerPrefKeys();
      }

      PlayerPrefs.DeleteKey(CurrentPagePlayerPrefsKey);
      PlayerPrefs.DeleteKey(ScrollRectPlayerPrefsKey);

      // Menu
      ClearQuantumMenuPlayerPrefs();

      // Quantum
      PlayerPrefs.DeleteKey(PhotonServerSettings.Global.BestRegionSummaryKey);
      PlayerPrefs.DeleteKey("Quantum.ReconnectInformation");
    }
    
    void DrawGlobalObjectStatus<T>() where T : QuantumGlobalScriptableObject<T> {
      var hasDefaultInstance = HubUtils.TryGetGlobalScriptableObjectCached<T>(out var defaultInstance);

      var attribute = typeof(T).GetCustomAttribute<QuantumGlobalScriptableObjectAttribute>();
      Debug.Assert(attribute != null);
      Debug.Assert(attribute.DefaultPath.StartsWith("Assets/"));
      var nicePath = PathUtils.GetPathWithoutExtension(attribute.DefaultPath.Substring("Assets/".Length));

      using (new EditorGUILayout.HorizontalScope()) {
        using (new EditorGUI.DisabledScope(!hasDefaultInstance)) {
          if (GUILayout.Button(nicePath, HubSkin.label)) {
            EditorGUIUtility.PingObject(defaultInstance);
          }
        }

        GUILayout.Label(GetStatusIcon(hasDefaultInstance), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
      }
    }

    // TODO: call after importing menu
    public static void ClearQuantumMenuPlayerPrefs() {
      PlayerPrefs.DeleteKey("Photon.Menu.Username");
      PlayerPrefs.DeleteKey("Photon.Menu.Region");
      PlayerPrefs.DeleteKey("Photon.Menu.AppVersion");
      PlayerPrefs.DeleteKey("Photon.Menu.MaxPlayerCount");
      PlayerPrefs.DeleteKey("Photon.Menu.Scene");
      PlayerPrefs.DeleteKey("Photon.Menu.SceneName");
      PlayerPrefs.DeleteKey("Photon.Menu.Framerate");
      PlayerPrefs.DeleteKey("Photon.Menu.Fullscreen");
      PlayerPrefs.DeleteKey("Photon.Menu.Resolution");
      PlayerPrefs.DeleteKey("Photon.Menu.VSync");
      PlayerPrefs.DeleteKey("Photon.Menu.QualityLevel");
      PlayerPrefs.DeleteKey("Photon.StartUI.IsMuted");
      PlayerPrefs.DeleteKey("Photon.StartUI.RegionName");
      PlayerPrefs.DeleteKey("Photon.StartUI.PlayerName");
      PlayerPrefs.DeleteKey("Quantum.ReconnectInformation");
    }
    
    /// <summary>
    /// Open the Fusion Hub window.
    /// </summary>
    [MenuItem("Window/Quantum/Quantum Hub")]
    [MenuItem("Tools/Quantum/Quantum Hub %H", false, (int)QuantumEditorMenuPriority.TOP)]
    public static void Open() {
      OpenCurrentPage();
    }

    public void DrawInstallationBox(QuantumEditorHubWidget widget) {
      using (new EditorGUILayout.VerticalScope(GetBoxStyle)) {
        DrawButtonAction(widget.Icon, widget.Text, widget.Subtext,
          statusIcon: GetStatusIcon(_statusInstallationComplete),
          callback: () => {
            InstallAllUserFiles();
            OnGuiHeartbeat();
            HubUtils.GlobalInstanceMissing.Clear();
          });


        QuantumGlobalScriptableObjectUtils.CreateFindDefaultAssetPathCache();
        try {
          DrawGlobalObjectStatus<PhotonServerSettings>();
          DrawGlobalObjectStatus<QuantumDeterministicSessionConfigAsset>();
          DrawGlobalObjectStatus<QuantumUnityDB>();
          DrawGlobalObjectStatus<QuantumLookupTables>();
          DrawGlobalObjectStatus<QuantumEditorSettings>();
          DrawGlobalObjectStatus<QuantumGameGizmosSettingsScriptableObject>();
          DrawGlobalObjectStatus<QuantumDefaultConfigs>();
          DrawGlobalObjectStatus<QuantumDotnetBuildSettings>();
          DrawGlobalObjectStatus<QuantumDotnetProjectSettings>();
        } finally {
          QuantumGlobalScriptableObjectUtils.ClearFindDefaultAssetPathCache();
        }

        using (new EditorGUILayout.HorizontalScope()) {
          if (GUILayout.Button(QuantumEditorUserScriptGeneration.FolderPath.Replace("Assets/", "") + " User Workspace", HubSkin.label)) {
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(QuantumEditorUserScriptGeneration.PingWorkspaceFile, typeof(UnityEngine.Object)));
          }
          GUILayout.Label(GetStatusIcon(QuantumEditorUserScriptGeneration.WorkspaceFilesExist), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
        }

        using (new EditorGUILayout.HorizontalScope()) {
          if (GUILayout.Button(QuantumEditorUserScriptGeneration.FolderPath.Replace("Assets/", "") + " Partial Classes (*.cs.User)", HubSkin.label)) {
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(QuantumEditorUserScriptGeneration.PingUserFile, typeof(UnityEngine.Object)));
          }
          GUILayout.Label(GetStatusIcon(QuantumEditorUserScriptGeneration.UserFilesExist), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
        }

        using (new EditorGUILayout.HorizontalScope()) {
          var assetGuids = default(string[]);
          if (_statusAnyQuantumMapFound) {
            assetGuids = AssetDatabase.FindAssets("t:Scene QuantumGameScene", new[] { QuantumEditorUserScriptGeneration.FolderPath });
          }
          if (assetGuids != null && assetGuids.Length > 0) {
            if (GUILayout.Button("QuantumUser Scene And Map", HubSkin.label)) {
              EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(assetGuids.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(), typeof(UnityEngine.Object)));
            }
          } else {
            GUILayout.Label("QuantumUser Scene And Map");
          }
          GUILayout.Label(GetStatusIcon(_statusAnyQuantumMapFound), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
        }

        using (new EditorGUILayout.HorizontalScope()) {
          GUILayout.Label("Quantum Qtn CodeGen");
          GUILayout.Label(GetStatusIcon(Quantum.Input.MaxCount > 0), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
        }

        using (new EditorGUILayout.HorizontalScope()) {
          GUILayout.Label("EditorSettings.ProjectGenerationUserExtensions Include Qtn Files");
          GUILayout.Label(GetStatusIcon(EditorSettings.projectGenerationUserExtensions.Contains("qtn")), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
        }

        if (SceneView.lastActiveSceneView != null) {
          using (new EditorGUILayout.HorizontalScope()) {
            var isQuantumXYEnabled =
#if QUANTUM_XY
              true;
#else
              false;
#endif
            var isModeCorrectlySet =
              (SceneView.lastActiveSceneView.in2DMode == true && isQuantumXYEnabled == true) ||
              (SceneView.lastActiveSceneView.in2DMode == false && isQuantumXYEnabled == false);
            GUILayout.Label("Toggle Quantum 2D/3D Mode (QUANTUM_XY)");
            GUILayout.Label(GetStatusIcon(isModeCorrectlySet), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
          }
        }
      }
    }
  }
}
