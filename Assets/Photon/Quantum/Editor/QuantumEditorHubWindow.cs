namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using System.Text.RegularExpressions;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using Debug = UnityEngine.Debug;
  using EditorUtility = UnityEditor.EditorUtility;

  /// <summary>
  /// The main class to manage the Quantum Hub window.
  /// The Hub for example completes the installation of a user project by creating vital config files that are not overwritten by upgrades.
  /// It can also install samples and addons that are packaged with the Quantum SDK.
  /// </summary>
  [InitializeOnLoad]
  internal partial class QuantumEditorHubWindow : EditorWindow {
    const int NavWidth = 256 + 2;
    const int NavButtonHeight = 56;
    const int NavButtonWidth = 260;
    const int IconSize = 32;
    const int IconMargin = 14;
    const string UrlDoc = "https://doc.photonengine.com/quantum/v3";
    const string UrlSDK = UrlDoc + "/getting-started/initial-setup";
    const string UrlChangelog = UrlDoc + "/getting-started/release-notes";
    const string Url100Tutorial = UrlDoc + "/tutorials/asteroids/1-overview";
    const string UrlPublicDiscord = "https://dashboard.photonengine.com/discord/joinphotonengine";
    const string UrlDashboard = "https://dashboard.photonengine.com/";
    const string UrlDocApi = "https://doc-api.photonengine.com/en/quantum/v3/index.html";
    const string UrlCircle = "https://www.photonengine.com/gaming";
    const string UrlRealtime = "https://doc.photonengine.com/realtime";
    const string WindowTitle = "Photon Quantum Hub";
    const string TextSection_Welcome =
      "Welcome to the Quantum 3 Game Engine.";
    const string TextSection_Support =
      "Take a look at the wide selection of support options and channels.";
    const string TextSection_SamplesAsteroid =
      @"Install the grey-boxing asteroids game sample programmed in Quantum to test local and online mode and make sure to check out the <b>Quantum 100 tutorial</b>, it explains step-by-step how the asteroids sample was created.";
    const string TextSection_SamplesMenu =
      @"Install the <b>menu package</b> to have a fully functional online game menu and it works well together with the asteroids game sample. Create Unity builds to play the asteroids sample <b>online</b> after installing the two packages and creating an online AppId.";
    const string TextSection_SamplesConnectionScene = "Create and set up a Unity scene that demonstrates how to connect and start a Quantum online session";
    const string TextSection_Samples100Doc = "Follow the step-by-step guide to get up and running with the Quantum SDK in Unity.";
    const string TextSection_Setup =
      "This sections shows individual Quantum 3 SDK installation steps. Uncompleted steps are shown on the welcome screen.";
    const string TextSection_Changelog =
      "The Quantum release notes can be found inside the SDK and on our website.";
    const string TextSection_ChangelogRealtime =
      "Photon Realtime is our network base layer and it solves problems like authentication, matchmaking and fast communication with a scalable approach.";
    const string TextInstallationInstructions =
      "Quantum requires an installation step to create local user scripts and asset files that won't be overwritten by upgrades:";
    const string TextAppIdInstructions =
      @"<b>A Quantum 3 AppId is required to connect to the Quantum public cloud:</b>
  - Open the Photon Dashboard link log in or register an account.
  - Select an existing Quantum 3 AppId or create a new one.
  - Copy the App Id and paste into the field below.";
    const string TextWelcome_InstallQuantum = "<size=20>Step 1</size>   Complete the installation the Quantum Unity SDK";
    const string TextWelcome_InstallAsteroids = "<size=20>Step 2</size>   Install the asteroids Quantum game sample and press play see the game running in local mode.";
    const string TextWelcome_CreateAppId = "<size=20>Step 3</size>   Register to a Photon account on our website and create an AppId to run online games.";
    const string TextWelcome_InstallMenu = "<size=20>Step 4</size>   Install the menu package and make Unity builds to see the asteroids sample in online mode.";
    const string TextWelcome_Configurations = "Further Configurations";
    const string TextWelcome_Final = "Further Reading";
    const string TextLogLevel = "<b>Quantum Log Level  [<color=#FFDDBB>{0}</color>]</b>\n<color=#aaaaaa>Change the Quantum log level to Debug or Info for development.</color>";
    const string TitleVersionReformat = "<size=22><color=white>{0}</color></size>";
    const string SectionReformat = "<i><color=lightblue>{0}</color></i>";
    const string Header1Reformat = "<size=22><color=white>{0}</color></size>";
    const string Header2Reformat = "<size=18><color=white>{0}</color></size>";
    const string Header3Reformat = "<b><color=#ffffaaff>{0}</color></b>";
    const string ClassReformat = "<color=#FFDDBB>{0}</color>";

    static Vector2 StatusIconWidthDefault = new Vector2(24, 24);
    static Vector2 StatusIconWidthLarge = new Vector2(32, 32);
    static string[] PlayerPrefKeys = new string[] {
      "Quantum.Hub.SkipStep2",
      "Quantum.Hub.SkipStep3",
      "Quantum.Hub.SkipStep4"
    };

    ButtonInfo Button_InstallAsteroidSample = new ButtonInfo(Icon.Samples,
      "Install Quantum Asteroids Sample Game",
      "Imports the asteroids game sample programmed with Quantum. Parts of the sample are covered by the Quantum online tutorials.");
    ButtonInfo Button_InstallAsteroidSample2 = new ButtonInfo(Icon.Samples,
      "Reimport Quantum Asteroids Sample Game",
      "Reimports the asteroids game sample programmed with Quantum.");
    ButtonInfo Button_InstallMenu = new ButtonInfo(Icon.Samples,
      "Install Quantum Menu",
      "Fully-fledged prototyping online game menu");
    ButtonInfo Button_InstallMenu2 = new ButtonInfo(Icon.Samples,
      "Reimport Quantum Menu",
      "Reimport the Quantum menu after Unity version upgrades for example.");
    ButtonInfo Button_InstallSimpleConnectionScene = new ButtonInfo(Icon.Samples,
      "Install Simple Connection Sample Scene",
      "Creates a scene that showcases the Quantum online connection sequence.");
    ButtonInfo Button_InstallSimpleConnectionScene2 = new ButtonInfo(Icon.Samples,
      "Reinstall Simple Connection Sample Scene",
      "Recreates the simple connection sample scene.");
    ButtonInfo Button_Docs100Tutorial = new ButtonInfo(Icon.Link,
      "Quantum Asteroids Tutorial (Opens Web Browser)",
      "Open Online Documentation About Quantum Fundamentals");
    ButtonInfo Button_CommunityDiscord = new ButtonInfo(Icon.Link,
      "Photon Discord Server (Opens Web Browser)", 
      "Join our Photon Discord Server.");
    ButtonInfo Button_QuantumXY = new ButtonInfo(Icon.BuiltIn_2DIcon,
#if QUANTUM_XY
      "Toggle Quantum XY  [<color=#7de886>Enabled</color>]",
      "Quantum 2D uses the XZ plane.");
#else
      "Toggle Quantum XY  [<color=#FFDDBB>Disabled</color>]",
      "If your project is intended to be 2D, consider enabling XY mode to use the same 2D axis as Unity.");
#endif
    ButtonInfo Button_DocsOnlineLink = new ButtonInfo(Icon.Link,
      "Quantum 3 Online Documentation (Opens Web Browser)",
      "Open the Quantum 3 online documentation to discover tons of material including tutorials, manuals, concepts, samples and addons");
    ButtonInfo Button_DocsSdkDownloadLink = new ButtonInfo(Icon.Link,
      "Quantum 3 SDK and Release Notes (Opens Web Browser)", 
      "Open the Quantum 3 SDK online download tables and release notes.");
    ButtonInfo Button_DocsApiLink = new ButtonInfo(Icon.Link,
      "Quantum 3 API Reference (Opens Web Browser)",
      "Open the online Quantum 3 API reference documentation.");
    ButtonInfo Button_DocCircleLink = new ButtonInfo(Icon.PhotonCloud,
      "Photon Gaming Circle (Opens Web Browser)",
      "Get premium development support from our engineers, access exclusive samples and features equipping you with essential resources.");
    ButtonInfo Button_DocReleaseNotes = new ButtonInfo(Icon.Link,
      "Quantum Release Notes (Opens Web Browser)",
      "Opens the online release notes web site.");
    ButtonInfo Button_FileChangelog = new ButtonInfo(Icon.Text,
      "Release Notes Text Asset",
      "Select the Changelog text asset file in the Unity project.");
    ButtonInfo Button_FileBuildInfo = new ButtonInfo(Icon.Text,
      "Build Info Text Asset",
      "Select the build_info.txt file in the Unity project.");
    ButtonInfo Button_InstallQuantum = new ButtonInfo(Icon.Installation,
      "Install",
      "Install Quantum user files.");
    ButtonInfo Button_DashboardLink = new ButtonInfo(Icon.Link,
      "Photon Dashboard (Opens Web Browser)",
      "Register a Photon account and create free AppIds");
    ButtonInfo Button_SelectPhotonServerAsset = new ButtonInfo(Icon.BuiltIn_ScriptableObjectIcon,
        "Select Photon Server Settings Asset",
        "Select the Photon network transport configuration asset that the AppId is stored in.");
    ButtonInfo Button_ToolsClearPlayerPrefs = new ButtonInfo(Icon.BuiltIn_RefreshIcon, 
      "Clear Quantum PlayerPrefs",
      "Delete all PlayerPrefs created by Quantum.");

#if QUANTUM_UPM
    static string BuildInfoFilepath => $"Packages/com.photonengine.quantum/build_info.txt";
    static string ReleaseHistoryFilepath => $"Packages/com.photonengine.quantum/CHANGELOG.md";
    static string ReleaseHistoryRealtimeFilepath => $"Packages/com.photonengine.realtime/CHANGELOG.md";
#else
    static string BuildInfoFilepath => BuildPath(Application.dataPath, "Photon", "Quantum", "build_info.txt");
    static string ReleaseHistoryFilepath => BuildPath(Application.dataPath, "Photon", "Quantum", "CHANGELOG.md");
    static string ReleaseHistoryRealtimeFilepath => BuildPath(Application.dataPath, "Photon", "PhotonRealtime", "Code", "changes-realtime.txt");
    static string QuantumMenuUnitypackagePath => BuildPath(Application.dataPath, "Photon", "QuantumMenu", "Quantum-Menu.unitypackage");
    static string QuantumMenuScenePath => BuildPath(Application.dataPath, "Photon", "QuantumMenu", "QuantumSampleMenu.unity");
    static string QuantumMenuConfigPath => BuildPath(Application.dataPath, "Photon", "QuantumMenu", "QuantumMenuConfig.asset");
    static string QuantumAsteroidsUnitypackagePath => BuildPath(Application.dataPath, "Photon", "QuantumAsteroids", "Quantum-Asteroids.unitypackage");
    static string QuantumAsteroidsScenePath => BuildPath(Application.dataPath, "Photon", "QuantumAsteroids", "Scenes", "AsteroidsGameplay.unity");
    static string QuantumAsteroidsThumbnailPath => BuildPath(Application.dataPath, "Photon", "QuantumAsteroids", "Scenes", "AsteroidsMenuThumbnail.png");
#endif

    static string _releaseHistoryHeader;
    static List<string> _releaseHistoryTextAdded;
    static List<string> _releaseHistoryTextChanged;
    static List<string> _releaseHistoryTextFixed;
    static List<string> _releaseHistoryTextRemoved;
    static List<string> _releaseHistoryTextInternal;
    static string _releaseHistory;
    static GUIStyle _navbarHeaderGraphicStyle;
    static GUIStyle _textLabelStyle;
    static GUIStyle _headerLabelStyle;
    static GUIStyle _releaseNotesStyle;
    static GUIStyle _headerTextStyle;
    static GUIStyle _buttonActiveStyle;
    static bool? _isOpen; // true after InitContent(), reset onDestroy, onEnable, etc.
    static bool _statusInstallationComplete;
    static bool _statusAppIdSetup;
    static bool _statusAsteroidsInstalled;
    static bool _statusMenuInstalled;
    static Vector2 _windowSize = new Vector2(850, 600);
    static Vector2 _windowPosition = new Vector2(100, 100);

    /// <summary>
    /// The Quantum Hub Unity skin.
    /// </summary>
    public GUISkin QuantumHubSkin;
    /// <summary>
    /// The setup icon.
    /// </summary>
    public Texture2D SetupIcon;
    /// <summary>
    /// The documentation icon.
    /// </summary>
    public Texture2D DocumentationIcon;
    /// <summary>
    /// The icon shown for link buttons
    /// </summary>
    public Texture2D LinkIcon;
    /// <summary>
    /// The samples icon.
    /// </summary>
    public Texture2D SamplesIcon;
    /// <summary>
    /// The community icon.
    /// </summary>
    public Texture2D CommunityIcon;
    /// <summary>
    /// The product logo.
    /// </summary>
    public Texture2D ProductLogo;
    /// <summary>
    /// The Photon cloud icon.
    /// </summary>
    public Texture2D PhotonCloudIcon;
    /// <summary>
    /// The Installation icon.
    /// </summary>
    public Texture2D InstallationIcon;
    /// <summary>
    /// The correct icon marking completed installation steps.
    /// </summary>
    public Texture2D CorrectIcon;
    /// <summary>
    /// The icon marking missing installation steps.
    /// </summary>
    public Texture2D MissingIcon;
    /// <summary>
    /// The icon marking missing installation steps.
    /// </summary>
    public Texture2D TextIcon;

    SectionInfo[] _sections;
    int _currentSection;
    double _nextForceRepaint;
    Vector2 _scrollRect;
    double _welcomeScreenConditionsTimestamp;

    GUIStyle GetBoxStyle => QuantumHubSkin.GetStyle("SteelBox");
    GUIStyle GetButtonPaneStyle => QuantumHubSkin.GetStyle("ButtonPane");

    enum Icon {
      BuiltIn_ScriptableObjectIcon,
      BuiltIn_RefreshIcon,
      BuiltIn_2DIcon,
      BuiltIn_ConsoleIcon,
      Setup,
      Documentation,
      Samples,
      Community,
      ProductLogo,
      PhotonCloud,
      Installation,
      Link,
      Text
    }

    static bool IsAppIdValid {
      get {
        try {
          var photonSettings = PhotonServerSettings.Global;
          var val = photonSettings.AppSettings.AppIdQuantum;
          return IsValidGuid(val);
        } catch {
          return false;
        }
      }
    }

    static bool AreImportantUserFilesInstalled {
      get {
        return PhotonServerSettings.TryGetGlobal(out _) &&
          QuantumDeterministicSessionConfigAsset.TryGetGlobal(out _) &&
          QuantumEditorSettings.TryGetGlobal(out _);
      }
    }

    [InitializeOnLoadMethod]
    static void InitializedPackageImportCallbacks() {
      // Package import callbacks are removed during domain reload, here we globally set one.
      AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
    }

    /// <summary>
    /// Open the Quantum Hub window.
    /// </summary>
    [MenuItem("Window/Quantum/Quantum Hub")]
    [MenuItem("Tools/Quantum/Quantum Hub %H", false, (int)QuantumEditorMenuPriority.TOP)]
    [MenuItem("Tools/Quantum/Window/Quantum Hub", false, (int)QuantumEditorMenuPriority.Window + 0)]
    public static void Open() {
      if (Application.isPlaying) {
        return;
      }

      var window = GetWindow<QuantumEditorHubWindow>(true, WindowTitle, true);
      window.position = new Rect(_windowPosition, _windowSize);
      _statusInstallationComplete = AreImportantUserFilesInstalled;
      _statusAppIdSetup = IsAppIdValid;
      window.Show();
    }

    static void ReOpen() {
      if (_isOpen.HasValue && _isOpen.Value == false) {
        Open();
      }

      EditorApplication.update -= ReOpen;
    }

    void OnEnable() {
      _isOpen = false;

      minSize = _windowSize;

      // Pre-load Release History
      PrepareReleaseHistoryText();
      wantsMouseMove = true;
    }

    void OnDestroy() {
      _isOpen = false;
    }

    void OnGUI() {

      GUI.skin = QuantumHubSkin;

      try {
        RefreshWelcomeScreenConditions();

        InitContent();

        _windowPosition = this.position.position;

        // full window wrapper
        using (new EditorGUILayout.HorizontalScope(GUI.skin.window)) {

          // Left Nav menu
          using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(NavWidth), GUILayout.MinWidth(NavWidth))) {
            DrawHeader();
            DrawLeftNavMenu();
          }

          // Right Main Content
          using (new EditorGUILayout.VerticalScope()) {
            DrawContent();
          }
        }

        DrawFooter();

      } catch (ExitGUIException) {
        // hide gui exception
      } catch (Exception e) {
        QuantumEditorLog.Exception($"Exception when drawing the Hub Window", e);
      }

      // Force repaints while mouse is over the window, to keep Hover graphics working (Unity quirk)
      var timeSinceStartup = Time.realtimeSinceStartupAsDouble;
      if (Event.current.type == EventType.MouseMove && timeSinceStartup > _nextForceRepaint) {
        // Cap the repaint rate a bit since we are forcing repaint on mouse move
        _nextForceRepaint = timeSinceStartup + .05f;
        Repaint();
      }
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    static void OnDidReloadScripts() {
      EnsureUserFilesExists();
    }

    /// <summary>
    /// Is used to check if important user files are installed and opens the Hub otherwise.
    /// </summary>
    public static void EnsureUserFilesExists() {
      if (EditorApplication.isPlayingOrWillChangePlaymode) {
        return;
      }
      
      // Check for important user files
      if (AreImportantUserFilesInstalled) {
        return;
      }

      if (EditorApplication.isCompiling || EditorApplication.isUpdating) {
        EditorApplication.delayCall += EnsureUserFilesExists;
        return;
      }

      EditorApplication.delayCall += Open;
    }

    void DrawContent() {
      {
        var section = _sections[_currentSection];
        GUILayout.Label(section.Description, _headerTextStyle);

        using (new EditorGUILayout.VerticalScope(QuantumHubSkin.box)) {
          _scrollRect = EditorGUILayout.BeginScrollView(_scrollRect);
          section.DrawMethod.Invoke();
          EditorGUILayout.EndScrollView();
        }
      }
    }

    void DrawSectionWelcome() {

      // Top Welcome content box
      GUILayout.Label(TextSection_Welcome);
      GUILayout.Space(8);

      // Step 1
      {
        var stepComplete = _statusInstallationComplete;

        using (new EditorGUILayout.HorizontalScope()) {
          GUILayout.Label(GetStatusIcon(stepComplete), GUILayout.Width(StatusIconWidthLarge.x), GUILayout.Height(StatusIconWidthLarge.y));
          GUILayout.Label(TextWelcome_InstallQuantum);
        }

        if (stepComplete == false) {
          DrawInstallationBox();
        }
      }

      // Step 2
      {
        var stepComplete = _statusAsteroidsInstalled || PlayerPrefs.GetInt(PlayerPrefKeys[0], 0) == 1;

        using (new EditorGUILayout.HorizontalScope()) {
          GUILayout.Label(GetStatusIcon(stepComplete), GUILayout.Width(StatusIconWidthLarge.x), GUILayout.Height(StatusIconWidthLarge.y));
          GUILayout.Label(TextWelcome_InstallAsteroids);
        }

        if (stepComplete == false) {
          DrawButtonAction(Button_InstallAsteroidSample, callback: () => {
            AssetDatabase.ImportPackage(QuantumAsteroidsUnitypackagePath, false);
          });

          if (GUILayout.Button("<i>Skip this step</i>", _textLabelStyle)) {
            PlayerPrefs.SetInt(PlayerPrefKeys[0], 1);
          }
        }
      }

      // Step 3
      {
        var stepComplete = _statusAppIdSetup || PlayerPrefs.GetInt(PlayerPrefKeys[1], 0) == 1;

        using (new EditorGUILayout.HorizontalScope()) {
          GUILayout.Label(GetStatusIcon(stepComplete), GUILayout.Width(StatusIconWidthLarge.x), GUILayout.Height(StatusIconWidthLarge.y));
          GUILayout.Label(TextWelcome_CreateAppId);
        }

        if (stepComplete == false) {
          DrawSetupAppIdBox();

          if (GUILayout.Button("<i>Skip this step</i>", _textLabelStyle)) {
            PlayerPrefs.SetInt(PlayerPrefKeys[1], 1);
          }
        }
      }

      // Step 4
      {
        var stepComplete = _statusMenuInstalled || PlayerPrefs.GetInt(PlayerPrefKeys[2], 0) == 1; ;

        using (new EditorGUILayout.HorizontalScope()) {
          GUILayout.Label(GetStatusIcon(stepComplete), GUILayout.Width(StatusIconWidthLarge.x), GUILayout.Height(StatusIconWidthLarge.y));
          GUILayout.Label(TextWelcome_InstallMenu);
        }

        if (stepComplete == false) {
          DrawButtonAction(Button_InstallMenu, callback: () => {
            AssetDatabase.ImportPackage(QuantumMenuUnitypackagePath, false);
          });

          if (GUILayout.Button("<i>Skip this step</i>", _textLabelStyle)) {
            PlayerPrefs.SetInt(PlayerPrefKeys[2], 1);
          }
        }
      }

      GUILayout.Label(TextWelcome_Configurations);


      // Toggle XY mode only when unity is in 2D mode
      if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.in2DMode) {
        DrawButtonAction(Button_QuantumXY, callback: () => {
          bool xy = false;
#if QUANTUM_XY
      xy = true;
#endif
          AssetDatabaseExt.UpdateScriptingDefineSymbol("QUANTUM_XY", !xy);
        });
      }

      // Change the log level shortcut
      {
        var currentLogLevel = QuantumUnityLogger.DefinedLogLevel;
        var height = IconSize + GUI.skin.button.padding.top + GUI.skin.button.padding.bottom;
        var rect = EditorGUILayout.GetControlRect(false, height, GUILayout.ExpandWidth(true));
        GUI.Label(rect, QuantumEditorSkin.ConsoleIcon, GetButtonPaneStyle);
        rect.xMin += IconSize + IconMargin * 2;
        GUI.Label(rect, string.Format(TextLogLevel, currentLogLevel));
        rect.xMin += rect.width - 100;
        rect.width -= IconMargin;
        var newHeight = EditorStyles.popup.CalcSize(new GUIContent("T")).y;
        var newY = rect.y + rect.height / 2 - newHeight / 2;
        rect.y = newY;
        rect.height = newHeight;
        var newLogLevel = (Quantum.LogType)EditorGUI.EnumPopup(rect, currentLogLevel);
        if (newLogLevel != currentLogLevel) {
          QuantumEditorSettingsEditor.SetLogLevel(newLogLevel);
        }
      }

      // Further readings
      GUILayout.Label(TextWelcome_Final);
      DrawButtonAction(Button_DocsOnlineLink, callback: OpenURL(UrlDoc));
    }

    void DrawSectionInstallation() {
      GUILayout.Label(TextSection_Setup);
      GUILayout.Space(8);

      DrawInstallationBox();
      DrawSetupAppIdBox();

      using (new EditorGUILayout.VerticalScope(GetBoxStyle)) {
        DrawButtonAction(Button_SelectPhotonServerAsset, callback: () => {
          EditorGUIUtility.PingObject(PhotonServerSettings.Global); Selection.activeObject = PhotonServerSettings.Global;
        });
      }

      using (new EditorGUILayout.VerticalScope(GetBoxStyle)) {
        DrawButtonAction(Button_ToolsClearPlayerPrefs, callback: () => {
          for (int i = 0; i < PlayerPrefKeys.Length; i++) {
            PlayerPrefs.DeleteKey(PlayerPrefKeys[i]);
          }
          ClearQuantumPlayerPrefs();
          ClearQuantumMenuPlayerPrefs();
        });
      }
    }

    static void ClearQuantumPlayerPrefs() {
      PlayerPrefs.DeleteKey(PhotonServerSettings.Global.BestRegionSummaryKey);
      PlayerPrefs.DeleteKey("Quantum.ReconnectInformation");
    }

    static void ClearQuantumMenuPlayerPrefs() {
      PlayerPrefs.DeleteKey("Photon.Menu.Username");
      PlayerPrefs.DeleteKey("Photon.Menu.Region");
      PlayerPrefs.DeleteKey("Photon.Menu.AppVersion");
      UnityEngine.PlayerPrefs.DeleteKey("Photon.Menu.MaxPlayerCount");
      PlayerPrefs.DeleteKey("Photon.Menu.Scene");
      UnityEngine.PlayerPrefs.DeleteKey("Photon.Menu.Framerate");
      PlayerPrefs.DeleteKey("Photon.Menu.Fullscreen");
      PlayerPrefs.DeleteKey("Photon.Menu.Resolution");
      UnityEngine.PlayerPrefs.DeleteKey("Photon.Menu.VSync");
      PlayerPrefs.DeleteKey("Photon.Menu.QualityLevel");
    }

    void DrawSectionSample() {
      if (AreImportantUserFilesInstalled) {

        GUILayout.Label(TextSection_SamplesAsteroid);

        DrawButtonAction(_statusAsteroidsInstalled ? Button_InstallAsteroidSample2 : Button_InstallAsteroidSample, 
          statusIcon: GetStatusIcon(_statusAsteroidsInstalled),
          callback: () => { AssetDatabase.ImportPackage(QuantumAsteroidsUnitypackagePath, false); });

        GUILayout.Label(TextSection_SamplesMenu);

        DrawButtonAction(_statusMenuInstalled ? Button_InstallMenu2 : Button_InstallMenu,
          statusIcon: GetStatusIcon(_statusMenuInstalled),
          callback: () => { AssetDatabase.ImportPackage(QuantumMenuUnitypackagePath, false); });

        GUILayout.Label(TextSection_SamplesConnectionScene);

        var sceneFilepath = $"{QuantumEditorUserScriptGeneration.FolderPath}/Scenes/QuantumSimpleConnectionScene.unity";
        var sceneFileExists = File.Exists(sceneFilepath);
        DrawButtonAction(sceneFileExists ? Button_InstallSimpleConnectionScene2 : Button_InstallSimpleConnectionScene,
          statusIcon: GetStatusIcon(sceneFileExists),
          callback: () => {
          QuantumEditorMenuCreateScene.CreateSimpleConnectionScene(sceneFilepath);
          GUIUtility.ExitGUI();
        });
      }
      else {
        GUILayout.Label("Complete the Quantum 3 SDK installation first.");
      }

      GUILayout.Label(TextSection_Samples100Doc);

      DrawButtonAction(Button_Docs100Tutorial, callback: OpenURL(Url100Tutorial));
    }

    /// <summary>
    /// The QPrototypes have to be reloaded to properly work.
    /// Reimporting assets only works after the package import.
    /// </summary>
    static void OnImportPackageCompleted(string packageName) {
      // Quantum-Menu
      if (string.Equals(packageName, Path.GetFileNameWithoutExtension(QuantumMenuUnitypackagePath), StringComparison.Ordinal)) {
        QuantumEditorMenuCreateScene.AddScenePathToBuildSettings(QuantumMenuScenePath, addToTop: true);
        ClearQuantumMenuPlayerPrefs();
        TryAddAsteroidSceneToMenuConfig();
        
        // Try to add the default map to the menu config by loading the default map location
        try {
          var quantumDefaultMapAsset = AssetDatabase.LoadAssetAtPath<Quantum.Map>(AssetDatabase.AssetPathToGUID($"{QuantumEditorSettings.Global.DefaultNewAssetsLocation}/QuantumMap.asset"));
          AddToQuantumMenuConfig($"{QuantumEditorUserScriptGeneration.FolderPath}/Scenes/QuantumGameScene.unity", null, new Dictionary<string, AssetRef>() {
              { "Map", new AssetRef(quantumDefaultMapAsset.Guid) } });
        } catch { }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
          EditorSceneManager.OpenScene(QuantumMenuScenePath);
        }
      }

      // Quantum-Asteroids
      else if (string.Equals(packageName, Path.GetFileNameWithoutExtension(QuantumAsteroidsUnitypackagePath), StringComparison.Ordinal)) {
        QuantumEditorMenuCreateScene.AddScenePathToBuildSettings(QuantumAsteroidsScenePath, addToTop: false);
        AssetDatabase.ImportAsset($"{Path.GetDirectoryName(QuantumAsteroidsUnitypackagePath)}/Resources", ImportAssetOptions.ImportRecursive);
        TryAddAsteroidSceneToMenuConfig();
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
          EditorSceneManager.OpenScene(QuantumAsteroidsScenePath);
        }
      }
    }

    static void TryAddAsteroidSceneToMenuConfig() {
      AddToQuantumMenuConfig(
        QuantumAsteroidsScenePath,
        QuantumAsteroidsThumbnailPath,
        new Dictionary<string, AssetRef>() {
              { "Map", new AssetRef(new AssetGuid(477700799046405574)) },
              { "SystemsConfig", new AssetRef(new AssetGuid(285306802963724605)) },
              { "SimulationConfig", new AssetRef(new AssetGuid(442089458455835007)) },
              { "GameConfig", new AssetRef(new AssetGuid(316527076537738773)) },
              { "DefaultPlayerAvatar", new AssetRef(new AssetGuid(1365103958109072725)) }});
    }

    /// <summary>
    /// Try to add the game scene to the Quantum menu config without using the actual menu dependencies.
    /// </summary>
    static void AddToQuantumMenuConfig(string scenePath, string previewPath, Dictionary<string, AssetRef> runtimeConfigSettings) {
      try {
        // TODO: fix for upm
        if (AssetDatabase.FindAssets($"{Path.GetFileNameWithoutExtension(scenePath)} t:scene")?.Length == 0) {
          return;
        }

        var menuConfig = AssetDatabase.LoadMainAssetAtPath(QuantumMenuConfigPath);
        if (menuConfig == null) {
          return;
        }

        var obj = new SerializedObject(menuConfig);
        var scenes = obj.FindProperty("_availableScenes");

        // Find already installed menu entry
        var menuConfigEntry = default(SerializedProperty);
        for (int i = 0; i < scenes.arraySize; i++) {
          var entry = scenes.GetArrayElementAtIndex(i);
          var path = entry.FindPropertyRelative("ScenePath");
          if (string.Equals(PathUtils.Normalize(path.stringValue), scenePath, StringComparison.Ordinal)) {
            menuConfigEntry = entry;
            break;
          }
        }

        // Create new menu entry
        if (menuConfigEntry == null) { 
          scenes.InsertArrayElementAtIndex(scenes.arraySize);
          menuConfigEntry = scenes.GetArrayElementAtIndex(scenes.arraySize - 1);
        }

        // Set or overwrite properties
        menuConfigEntry.FindPropertyRelative("Name").stringValue = Path.GetFileNameWithoutExtension(scenePath);
        menuConfigEntry.FindPropertyRelative("ScenePath").stringValue = scenePath;
        if (string.IsNullOrEmpty(previewPath)) {
          menuConfigEntry.FindPropertyRelative("Preview").objectReferenceValue = null;
        } else {
          menuConfigEntry.FindPropertyRelative("Preview").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(previewPath);
        }
        if (runtimeConfigSettings != null) {
          foreach (var setting in runtimeConfigSettings) {
            menuConfigEntry.FindPropertyRelative($"RuntimeConfig.{setting.Key}.Id.Value").longValue = setting.Value.Id.Value;
          }
        }

        obj.ApplyModifiedProperties();
      } catch (Exception e) {
        Log.Warn($"Failed to add the scene '{scenePath}' to the menu config: {e.Message}");
      }
    }
    
    void DrawReleaseHistoryRealtimeSection() {
      GUILayout.Label(TextSection_ChangelogRealtime);
      GUILayout.Space(8);

      var textAsset = (TextAsset)AssetDatabase.LoadAssetAtPath(ReleaseHistoryRealtimeFilepath, typeof(TextAsset));
      DrawButtonAction(Button_FileChangelog,
        callback: () => { EditorGUIUtility.PingObject(textAsset); Selection.activeObject = textAsset; });

      GUILayout.BeginVertical();
      {
        GUILayout.Label(string.Format(TitleVersionReformat, _releaseHistoryHeader));
        GUILayout.Space(5);
        DrawReleaseHistoryItem("Added:", _releaseHistoryTextAdded);
        DrawReleaseHistoryItem("Changed:", _releaseHistoryTextChanged);
        DrawReleaseHistoryItem("Fixed:", _releaseHistoryTextFixed);
        DrawReleaseHistoryItem("Removed:", _releaseHistoryTextRemoved);
        DrawReleaseHistoryItem("Internal:", _releaseHistoryTextInternal);
      }
      GUILayout.EndVertical();
    }

    void DrawSectionReleaseHistory() {
      GUILayout.Label(TextSection_Changelog);
      GUILayout.Space(8);

      var textAsset = (TextAsset)AssetDatabase.LoadAssetAtPath(ReleaseHistoryFilepath, typeof(TextAsset));
      DrawButtonAction(Button_FileChangelog,
        callback: () => { EditorGUIUtility.PingObject(textAsset); Selection.activeObject = textAsset; });

      DrawButtonAction(Button_DocReleaseNotes, callback: OpenURL(UrlChangelog));

      GUILayout.Label(_releaseHistory, _releaseNotesStyle);
    }

    void DrawReleaseHistoryItem(string label, List<string> items) {
      if (items != null && items.Count > 0) {
        GUILayout.BeginVertical();
        {
          GUILayout.Label(string.Format(SectionReformat, label));

          GUILayout.Space(5);

          foreach (string text in items) {
            GUILayout.Label(string.Format("- {0}.", text), _textLabelStyle);
          }
        }
        GUILayout.EndVertical();
      }
    }

    void DrawSectionSupport() {
      GUILayout.Label(TextSection_Support, _textLabelStyle);
      GUILayout.Space(8);

      DrawButtonAction(Button_DocCircleLink, callback: OpenURL(UrlCircle));
      DrawButtonAction(Button_CommunityDiscord, callback: OpenURL(UrlPublicDiscord));
      DrawButtonAction(Button_DocsOnlineLink, callback: OpenURL(UrlDoc));
      DrawButtonAction(Button_DocsSdkDownloadLink, callback: OpenURL(UrlSDK));
      DrawButtonAction(Button_DocsApiLink, callback: OpenURL(UrlDocApi));
    }

    void DrawSectionAbout() {
      var textAsset = (TextAsset)AssetDatabase.LoadAssetAtPath(BuildInfoFilepath, typeof(TextAsset));
      DrawButtonAction(Button_FileBuildInfo, callback: () => { EditorGUIUtility.PingObject(textAsset); Selection.activeObject = textAsset; });

      var text = textAsset.text;
      GUILayout.BeginVertical();
      GUILayout.Space(5);
      text = Regex.Replace(text, @"(build):", string.Format(ClassReformat, "$1"));
      text = Regex.Replace(text, @"(date):", string.Format(ClassReformat, "$1"));
      text = Regex.Replace(text, @"(git):", string.Format(ClassReformat, "$1"));
      GUILayout.Label(text, _textLabelStyle);
      GUILayout.EndVertical();

      try {
        var codeBase = Assembly.GetAssembly(typeof(FP)).CodeBase;
        var path = Uri.UnescapeDataString(new UriBuilder(codeBase).Path);
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(path);
        GUILayout.Label($"<color=#FFDDBB>{Path.GetFileName(codeBase)}</color>: {fileVersionInfo.ProductVersion}", _textLabelStyle);
      } catch { }

      try {
        string codeBase = Assembly.GetAssembly(typeof(Quantum.Map)).CodeBase;
        string path = Uri.UnescapeDataString(new UriBuilder(codeBase).Path);
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(path);
        GUILayout.Label($"<color=#FFDDBB>{Path.GetFileName(codeBase)}</color>: {fileVersionInfo.ProductVersion}", _textLabelStyle);
      } catch { }
    }

    void DrawGlobalObjectStatus<T>() where T : QuantumGlobalScriptableObject<T> {

      var attribute = typeof(T).GetCustomAttribute<QuantumGlobalScriptableObjectAttribute>();
      Debug.Assert(attribute != null);
      Debug.Assert(attribute.DefaultPath.StartsWith("Assets/"));
      
      var nicePath = PathUtils.GetPathWithoutExtension(attribute.DefaultPath.Substring("Assets/".Length));

      using (new EditorGUILayout.HorizontalScope()) {
        bool hasDefaultInstance = QuantumGlobalScriptableObject<T>.TryGetGlobal(out var defaultInstance);
        using (new EditorGUI.DisabledScope(!hasDefaultInstance)) {
          if (GUILayout.Button(nicePath, QuantumHubSkin.label)) {
            EditorGUIUtility.PingObject(defaultInstance);
          }
        }

        GUILayout.Label(GetStatusIcon(hasDefaultInstance), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
      }
    }
 
    void DrawCompletedStepBox() {
      using (new EditorGUILayout.VerticalScope(GetBoxStyle)) {
        GUILayout.Label(GetStatusIcon(true), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
      }
    }

    void DrawInstallationBox() {
      using (new EditorGUILayout.VerticalScope(GetBoxStyle)) {
        GUILayout.Label(TextInstallationInstructions);

        DrawButtonAction(Button_InstallQuantum,
          statusIcon: GetStatusIcon(_statusInstallationComplete),
          callback: () => {
            InstallAllUserFiles();
          });

        
        QuantumGlobalScriptableObjectUtils.CreateFindDefaultAssetPathCache();
        try {
          DrawGlobalObjectStatus<PhotonServerSettings>();
          DrawGlobalObjectStatus<QuantumDeterministicSessionConfigAsset>();
          DrawGlobalObjectStatus<QuantumUnityDB>();
          DrawGlobalObjectStatus<QuantumEditorSettings>();
          DrawGlobalObjectStatus<QuantumGameGizmosSettingsScriptableObject>();
          DrawGlobalObjectStatus<QuantumDefaultConfigs>();
          DrawGlobalObjectStatus<QuantumDotnetBuildSettings>();
          DrawGlobalObjectStatus<QuantumDotnetProjectSettings>();
        } finally {
          QuantumGlobalScriptableObjectUtils.ClearFindDefaultAssetPathCache();
        }

        using (new EditorGUILayout.HorizontalScope()) {
          if (GUILayout.Button(QuantumEditorUserScriptGeneration.FolderPath.Replace("Assets/", "") + " User Workspace", QuantumHubSkin.label)) {
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(QuantumEditorUserScriptGeneration.PingWorkspaceFile, typeof(UnityEngine.Object)));
          }
          GUILayout.Label(GetStatusIcon(QuantumEditorUserScriptGeneration.WorkspaceFilesExist), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
        }

        using (new EditorGUILayout.HorizontalScope()) {
          if (GUILayout.Button(QuantumEditorUserScriptGeneration.FolderPath.Replace("Assets/", "") + " Partial Classes (*.cs.User)", QuantumHubSkin.label)) {
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(QuantumEditorUserScriptGeneration.PingUserFile, typeof(UnityEngine.Object)));
          }
          GUILayout.Label(GetStatusIcon(QuantumEditorUserScriptGeneration.UserFilesExist), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
        }

        using (new EditorGUILayout.HorizontalScope()) {
          GUILayout.Label("QuantumUser Scenes");
          var foundAnySceneInUserFolder = Directory.Exists(QuantumEditorUserScriptGeneration.FolderPath) && AssetDatabase.FindAssets("t:Scene", new[] { QuantumEditorUserScriptGeneration.FolderPath }).Length > 0;
          GUILayout.Label(GetStatusIcon(foundAnySceneInUserFolder), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
        }

        using (new EditorGUILayout.HorizontalScope()) {
          GUILayout.Label("Quantum Qtn CodeGen");
          GUILayout.Label(GetStatusIcon(Quantum.Input.MaxCount > 0), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
        }

        using (new EditorGUILayout.HorizontalScope()) {
          GUILayout.Label("EditorSettings.ProjectGenerationUserExtensions Include Qtn Files");
          GUILayout.Label(GetStatusIcon(EditorSettings.projectGenerationUserExtensions.Contains("qtn")), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
        }
      }
    }

    /// <summary>
    /// This methods installs all user files and generates the workspace files.
    /// </summary>
    public static void InstallAllUserFiles() {
      QuantumGlobalScriptableObjectUtils.EnsureAssetExists<PhotonServerSettings>();
      QuantumGlobalScriptableObjectUtils.EnsureAssetExists<QuantumEditorSettings>();
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

      // Add qtn extension to the VS project generation
      if (EditorSettings.projectGenerationUserExtensions.Contains("qtn") == false) {
        var userExtensions = EditorSettings.projectGenerationUserExtensions;
        ArrayUtils.Add(ref userExtensions, "qtn");
        EditorSettings.projectGenerationUserExtensions = userExtensions;
      }

      if (AssetDatabase.FindAssets("t:Scene", new[] { QuantumEditorUserScriptGeneration.FolderPath }).Length == 0) {
        // Create Quantum game scene
        Directory.CreateDirectory($"{QuantumEditorUserScriptGeneration.FolderPath}/Scenes");
        QuantumEditorMenuCreateScene.CreateNewQuantumScene(
          $"{QuantumEditorUserScriptGeneration.FolderPath}/Scenes/QuantumGameScene.unity",
          $"{QuantumEditorSettings.Global.DefaultNewAssetsLocation}/QuantumMap.asset",
          true,
          true);

        if (Application.isBatchMode == false) {
          // Don't call the gui exception when coming from CI
          GUIUtility.ExitGUI();
        }
      }
    }

    void DrawSetupAppIdBox() {
      // Getting server settings data
      PhotonServerSettings.TryGetGlobal(out var photonServerSettings);
      var appId = photonServerSettings?.AppSettings.AppIdQuantum;

      // Setting up AppId content box.
      using (new EditorGUILayout.VerticalScope(GetBoxStyle)) {
        GUILayout.Label(TextAppIdInstructions);

        DrawButtonAction(Button_DashboardLink, callback: OpenURL(UrlDashboard));

        using (new EditorGUILayout.HorizontalScope(GetBoxStyle)) {
          GUILayout.Label("<b>App Id:</b>", GUILayout.Width(80));
          using (new EditorGUI.DisabledScope(photonServerSettings == null)) {
            using (new EditorGUILayout.HorizontalScope()) {
              EditorGUI.BeginChangeCheck();
              var editedAppId = EditorGUILayout.TextField("", appId, QuantumHubSkin.textField, GUILayout.Height(StatusIconWidthDefault.y));
              if (EditorGUI.EndChangeCheck()) {
                photonServerSettings.AppSettings.AppIdQuantum = editedAppId;
                EditorUtility.SetDirty(photonServerSettings);
                AssetDatabase.SaveAssets();
              }
            }
          }
          GUILayout.Label(GetStatusIcon(IsAppIdValid), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
        }
      }
    }

    void DrawLeftNavMenu() {
      for (int i = 0; i < _sections.Length; ++i) {
        var section = _sections[i];
        if (DrawNavButton(section, _currentSection == i)) {
          _currentSection = i;
        }
      }
    }

    void RefreshWelcomeScreenConditions() {
      if (EditorApplication.timeSinceStartup < _welcomeScreenConditionsTimestamp + 1.5f) {
        return;
      }

      _welcomeScreenConditionsTimestamp = EditorApplication.timeSinceStartup;
      _statusInstallationComplete = AreImportantUserFilesInstalled;
      _statusAppIdSetup = IsAppIdValid;
      
      // use types to narrow things down
      _statusAsteroidsInstalled = FindType<AssetObject>("AsteroidsGameConfig") != null;
      _statusMenuInstalled = FindType<QuantumScriptableObject>("QuantumMenuConfig") != null;
    }

    void DrawHeader() {
      GUILayout.Label(GetIcon(Icon.ProductLogo), _navbarHeaderGraphicStyle);
    }

    void DrawFooter() {
      GUILayout.BeginHorizontal(QuantumHubSkin.window);
      GUILayout.Label("\u00A9 2024, Exit Games GmbH. All rights reserved.");
      GUILayout.EndHorizontal();
    }

    bool DrawNavButton(SectionInfo section, bool currentSection) {
      var content = new GUIContent() {
        text  = "  " + section.Title,
        image = GetIcon(section.Icon),
      };

      var renderStyle = currentSection ? _buttonActiveStyle : GUI.skin.button;
      return GUILayout.Button(content, renderStyle, GUILayout.Height(NavButtonHeight), GUILayout.Width(NavButtonWidth));
    }

    void DrawButtonAction(ButtonInfo info, bool enabled = true, Action callback = null, int? width = null, Texture2D statusIcon = null) {
      DrawButtonAction(GetIcon(info.Icon), info.Header, info.Description, enabled, callback, width, statusIcon);
    }

    void DrawButtonAction(Icon icon, string header, string description = null, bool enabled = true, Action callback = null, int? width = null, Texture2D statusIcon = null) {
      DrawButtonAction(GetIcon(icon), header, description, enabled, callback, width, statusIcon);
    }

    static void DrawButtonAction(Texture2D icon, string header, string description = null, bool enabled = true, Action callback = null, int? width = null, Texture2D statusIcon = null) {
      var height = IconSize + GUI.skin.button.padding.top + GUI.skin.button.padding.bottom;

      // Draw text separately (not part of button guicontent) to have control over the space between the icon and the text.
      var rect = EditorGUILayout.GetControlRect(false, height, width.HasValue ? GUILayout.Width(width.Value) : GUILayout.ExpandWidth(true));

      var wasEnabled = GUI.enabled;
      GUI.enabled = enabled;
      bool clicked = GUI.Button(rect, icon, GUI.skin.button);
      GUI.enabled = wasEnabled;
      GUI.Label(new Rect(rect) { 
        xMin = rect.xMin + IconSize + IconMargin * 2,
        xMax = rect.xMax - (statusIcon != null ? (IconSize + 20) : 0),
      }, description == null ? "<b>" + header +"</b>" : string.Format("<b>{0}</b>\n{1}", header, "<color=#aaaaaa>" + description + "</color>"));
      if (clicked && callback != null) {
        callback.Invoke();
      }

      if (statusIcon) {
        GUI.DrawTexture(new Rect(rect) {
          yMin = rect.yMin + (rect.height - StatusIconWidthDefault.y) / 2,
          xMin = rect.xMax - (StatusIconWidthDefault.x + IconMargin),
          width = StatusIconWidthDefault.y,
          height = StatusIconWidthDefault.x,
        }, statusIcon);
      } 
    }

    class SectionInfo {
      public string Title;
      public string Description;
      public Action DrawMethod;
      public Icon Icon;

      public SectionInfo(string title, string description, Action drawMethod, Icon icon) {
        Title = title;
        Description = description;
        DrawMethod = drawMethod;
        Icon = icon;
      }
    }

    class ButtonInfo {
      public Icon Icon;
      public string Header;
      public string Description;

      public ButtonInfo(Icon icon, string header, string description) {
        Icon = icon;
        Header = header;
        Description = description;
      }
    }

    Texture2D GetIcon(Icon icon) {
      switch (icon) {
        case Icon.Setup: return SetupIcon;
        case Icon.Documentation: return DocumentationIcon;
        case Icon.Link: return LinkIcon;
        case Icon.Text: return TextIcon;
        case Icon.Samples: return SamplesIcon;
        case Icon.Community: return CommunityIcon;
        case Icon.ProductLogo: return ProductLogo;
        case Icon.PhotonCloud: return PhotonCloudIcon;
        case Icon.Installation: return InstallationIcon;
        case Icon.BuiltIn_2DIcon: return QuantumEditorSkin._2DIcon;
        case Icon.BuiltIn_ConsoleIcon: return QuantumEditorSkin.ConsoleIcon;
        case Icon.BuiltIn_RefreshIcon: return QuantumEditorSkin.RefreshIcon;
        case Icon.BuiltIn_ScriptableObjectIcon: return QuantumEditorSkin.ScriptableObjectIcon;
        default:
          return null;
      }
    }

    void InitContent() {
      if (_isOpen.HasValue && _isOpen.Value) {
        return;
      }

      _sections = new[] {
        new SectionInfo("Welcome", "Welcome to Photon Quantum 3", DrawSectionWelcome, Icon.Setup),
        new SectionInfo("Samples", "Samples And Tutorials", DrawSectionSample, Icon.Samples),
        new SectionInfo("Support", "Support, Community And Documentation", DrawSectionSupport, Icon.Community),
        new SectionInfo("Installation", "Quantum 3 SDK Installation", DrawSectionInstallation, Icon.Installation),
        new SectionInfo("Quantum Release Notes", "Photon Quantum Release Notes", DrawSectionReleaseHistory, Icon.Text),
        new SectionInfo("Realtime Release Notes", "Photon Realtime Release Notes", DrawReleaseHistoryRealtimeSection, Icon.Text),
        new SectionInfo("About", "Quantum SDK Build Information", DrawSectionAbout, Icon.Text),
      };

      Color commonTextColor = Color.white;

      var _guiSkin = QuantumHubSkin;

      _navbarHeaderGraphicStyle = new GUIStyle(GetBoxStyle) { alignment = TextAnchor.MiddleCenter };

      _headerTextStyle = new GUIStyle(_guiSkin.label) {
        fontSize = 18,
        padding = new RectOffset(12, 8, 8, 8),
        fontStyle = FontStyle.Bold,
        normal = { textColor = commonTextColor }
      };

      _buttonActiveStyle = new GUIStyle(_guiSkin.button) {
        fontStyle = FontStyle.Bold,
        normal = { background = _guiSkin.button.active.background, textColor = Color.white }
      };

      _textLabelStyle = new GUIStyle(_guiSkin.label) {
        wordWrap = true,
        normal = { textColor = commonTextColor },
        richText = true,

      };
      
      _headerLabelStyle = new GUIStyle(_textLabelStyle) {
        fontSize = 15,
      };

      _releaseNotesStyle = new GUIStyle(_textLabelStyle) {
        richText = true,
      };

      _isOpen = true;
    }

    static Action OpenURL(string url, params object[] args) {
      return () => {
        if (args.Length > 0) {
          url = string.Format(url, args);
        }

        Application.OpenURL(url);
      };
    }

    void PrepareReleaseHistoryText() {
      // Converts readme files into Unity RichText.
      {
        var text = (TextAsset)AssetDatabase.LoadAssetAtPath(ReleaseHistoryFilepath, typeof(TextAsset));
        var baseText = text.text;

        // #
        baseText = Regex.Replace(baseText, @"^# (.*)", string.Format(TitleVersionReformat, "$1"));
        baseText = Regex.Replace(baseText, @"(?<=\n)# (.*)", string.Format(Header1Reformat, "$1"));
        // ##
        baseText = Regex.Replace(baseText, @"(?<=\n)## (.*)", string.Format(Header2Reformat, "$1"));
        // ###
        baseText = Regex.Replace(baseText, @"(?<=\n)### (.*)", string.Format(Header3Reformat, "$1"));
        // **Changes**
        baseText = Regex.Replace(baseText, @"(?<=\n)\*\*(.*)\*\*", string.Format(SectionReformat, "$1"));
        // `Class`
        baseText = Regex.Replace(baseText, @"\`([^\`]*)\`", string.Format(ClassReformat, "$1"));

        _releaseHistory = baseText;
      }

      // Realtime
      {
        try {
          var text = (TextAsset)AssetDatabase.LoadAssetAtPath(ReleaseHistoryRealtimeFilepath, typeof(TextAsset));

          var baseText = text.text;

          var regexVersion = new Regex(@"Version (\d+\.?)*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexAdded = new Regex(@"\b(Added:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexChanged = new Regex(@"\b(Changed:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexUpdated = new Regex(@"\b(Updated:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexFixed = new Regex(@"\b(Fixed:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexRemoved = new Regex(@"\b(Removed:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexInternal = new Regex(@"\b(Internal:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);

          var matches = regexVersion.Matches(baseText);

          if (matches.Count > 0) {
            var currentVersionMatch = matches[0];
            var lastVersionMatch = currentVersionMatch.NextMatch();

            if (currentVersionMatch.Success && lastVersionMatch.Success) {
              Func<MatchCollection, List<string>> itemProcessor = (match) => {
                List<string> resultList = new List<string>();
                for (int index = 0; index < match.Count; index++) {
                  resultList.Add(match[index].Groups[2].Value.Trim());
                }
                return resultList;
              };

              string mainText = baseText.Substring(currentVersionMatch.Index + currentVersionMatch.Length,
                  lastVersionMatch.Index - lastVersionMatch.Length - 1).Trim();

              _releaseHistoryHeader = currentVersionMatch.Value.Trim();
              _releaseHistoryTextAdded = itemProcessor(regexAdded.Matches(mainText));
              _releaseHistoryTextChanged = itemProcessor(regexChanged.Matches(mainText));
              _releaseHistoryTextChanged.AddRange(itemProcessor(regexUpdated.Matches(mainText)));
              _releaseHistoryTextFixed = itemProcessor(regexFixed.Matches(mainText));
              _releaseHistoryTextRemoved = itemProcessor(regexRemoved.Matches(mainText));
              _releaseHistoryTextInternal = itemProcessor(regexInternal.Matches(mainText));
            }
          }
        } catch (Exception) {
          _releaseHistoryHeader = null;
          _releaseHistoryTextAdded = new List<string>();
          _releaseHistoryTextChanged = new List<string>();
          _releaseHistoryTextFixed = new List<string>();
          _releaseHistoryTextRemoved = new List<string>();
          _releaseHistoryTextInternal = new List<string>();
        }
      }
    }

    static bool Toggle(bool value) {
      GUIStyle toggle = new GUIStyle("Toggle") {
        margin = new RectOffset(),
        padding = new RectOffset()
      };

      return EditorGUILayout.Toggle(value, toggle, GUILayout.Width(15));
    }

    static string BuildPath(params string[] parts) {
      var basePath = "";

      foreach (var path in parts) {
        basePath = Path.Combine(basePath, path);
      }

      return PathUtils.Normalize(basePath.Replace(Application.dataPath, Path.GetFileName(Application.dataPath)));
    }

    Texture2D GetStatusIcon(bool isValid) {
      return isValid ? CorrectIcon : MissingIcon;
    }

    static bool IsValidGuid(string appId) {
      try {
        return new Guid(appId) != null;
      } catch {
        return false;
      }
    }

    static Type FindType<T>(string name) {
      foreach (var t in TypeCache.GetTypesDerivedFrom<T>()) {
        if (string.Equals(t.Name, name, StringComparison.Ordinal)) {
          return t;
        }
      }

      return null;
    }
  }

  class QuantumEditorHubWindowAssetPostprocessor : AssetPostprocessor {
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
      // Unity handling for post asset processing callback. Checks existence of settings assets every time assets change.
      QuantumEditorHubWindow.EnsureUserFilesExists();
    }
  }
}
