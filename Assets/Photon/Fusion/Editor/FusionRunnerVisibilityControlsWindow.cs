namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEditor;

  /// <summary>
  /// This window contains controls for each active NetworkRunner (see Multi-Peer) including
  /// UI toggles for runner SetVisible() and ProvideInput members. NetworkRunner and Player Objects can be pinged in the hierarchy.
  /// FusionStats creation shortcuts are provided for convenience as well.
  /// </summary>
  public class FusionRunnerVisibilityControlsWindow : EditorWindow {
    private const int WINDOW_MIN_W = 82;
    private const int WINDOW_MIN_H = 48;

    private const int STATS_BTTN_WIDE = 66;
    private const int STATS_BTTN_SLIM = 24;
    private const int RUNNR_BTTN_WIDE = 60;
    private const int RUNNR_BTTN_SLIM = 24;
    private const int FONT_SIZE = 9;

    private const float TEXT_SWITCH_WIDTH = 200;
    private const float WIDE_SWITCH_WIDTH = 380;

    private const double REFRESH_RATE = 1f;

    private static class Labels {
      public const string NoActiveRunner = "No Active Runner";
      public const string NoRunners = "No Runners";
      public const string SP = "SP";
      public const string H = "H";
      public const string S = "S";
      public const string C = "C";
      public const string P = "P";
      public const string Dash = "--";
      public const string ProvidingInputs = "\u2002Providing Inputs";
      public const string NoInputs = "\u2002(No Inputs)";
      public const string StatsLeft = "<< Stats";
      public const string StatsRight = "Stats >>";
      public const string ArrowsLeft = "<<";
      public const string ArrowsRight = ">>";
      public const string UserID = "UserID: ";

      public const string VisibilityTooltip =
        "This button toggles NetworkRunner.SetVisible() for this NetworkRunner. If [Shift] is held while clicking all other active runners will SetVisible(false), soloing this runner.";

      public const string InputTooltip =
        "This button toggles NetworkRunner.ProvideInput for this NetworkRunner. If [Shift] is held while clicking all other active runners will have NetworkRunner.ProvideInput set to false, soloing this runner.";

      public const string StatsTooltip = "Clicking this button at runtime will create a FusionStats overlay associated with this NetworkRunner. ";
      public const string RunnerTooltip = "The name of the NetworkRunner this row controls. Clicking this button will ping the NetworkRunner GameObject in the hierarchy.";

      public const string PlayerObjTooltip =
        "The PlayerRef ID associated with this NetworkRunner. If NetworkRunner has a set Player Object, then this button will be enabled and will ping the Player Object in the hierarchy.";

      public static readonly string NoVisibilityWarn =
        $"Network Runner does not have a {nameof(RunnerEnableVisibility)} component, or has not been registered with {nameof(NetworkRunnerVisibilityExtensions.EnableVisibilityExtension)}";

      public const string HoldShift = "Hold Shift while clicking to solo Visibility/Input Provider.";

      public static readonly string RunnerVisibilityNotEnabled = $"Runner visibility not enabled. Add a {nameof(RunnerEnableVisibility)} component to your {nameof(NetworkRunner)} Prefab.";
    }

    private static Lazy<GUIStyle> s_labelStyle = new Lazy<GUIStyle>(() => new GUIStyle(EditorStyles.label) { fontSize = FONT_SIZE });

    private static Lazy<GUIStyle> s_labelTinyStyle = new Lazy<GUIStyle>(() => new GUIStyle(s_labelStyle.Value) { fontSize = FONT_SIZE - 1 });

    private static Lazy<GUIStyle> s_labelCenterStyle = new Lazy<GUIStyle>(() => new GUIStyle(s_labelStyle.Value) { alignment = TextAnchor.MiddleCenter });

    private static Lazy<GUIStyle> s_buttonStyle = new Lazy<GUIStyle>(() => new GUIStyle(EditorStyles.miniButton) { fontSize = FONT_SIZE });

    private static Lazy<GUIStyle> s_helpboxStyle = new Lazy<GUIStyle>(() => new GUIStyle(EditorStyles.helpBox) {
      fontSize = FONT_SIZE, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(6, 6, 6, 6)
    });

    private static Lazy<GUIStyle> s_invisibleButtonStyle = new Lazy<GUIStyle>(() => new GUIStyle(EditorStyles.label) { fontSize = FONT_SIZE, padding = new RectOffset() });

    private static Lazy<GUIStyle> s_invisibleButtonGrayStyle = new Lazy<GUIStyle>(() => new GUIStyle(EditorStyles.label) {
      fontSize = FONT_SIZE,
      normal   = { textColor = Color.gray },
      active   = { textColor = Color.gray },
      hover    = { textColor = Color.gray },
      focused  = { textColor = Color.gray },
      padding  = new RectOffset()
    });

    private static Lazy<string> Dark = new Lazy<string>(() => EditorGUIUtility.isProSkin ? "d_" : "");

    private static Lazy<GUIContent> s_runnerGC = new Lazy<GUIContent>(() => new GUIContent(string.Empty, Labels.RunnerTooltip));

    private static Lazy<GUIContent> s_playerObjGC = new Lazy<GUIContent>(() => new GUIContent(string.Empty, Labels.PlayerObjTooltip));

    private static Lazy<GUIContent> s_visibleIcon = new Lazy<GUIContent>(() => new GUIContent(EditorGUIUtility.FindTexture(Dark.Value + "scenevis_visible_hover@2x"), Labels.VisibilityTooltip));

    private static Lazy<GUIContent> s_hiddenIcon = new Lazy<GUIContent>(() => new GUIContent(EditorGUIUtility.FindTexture(Dark.Value + "scenevis_hidden@2x"), Labels.VisibilityTooltip));

    private static Lazy<GUIContent> s_inputIconLong =
      new Lazy<GUIContent>(() => new GUIContent(Labels.ProvidingInputs, EditorGUIUtility.FindTexture(Dark.Value + "UnityEditor.GameView@2x"), Labels.InputTooltip));

    private static Lazy<GUIContent> s_inputIconShort = new Lazy<GUIContent>(() => new GUIContent(null, EditorGUIUtility.FindTexture(Dark.Value + "UnityEditor.GameView@2x"), Labels.InputTooltip));

    private static Lazy<GUIContent> s_noInputIconLong = new Lazy<GUIContent>(() => new GUIContent(Labels.NoInputs, EditorGUIUtility.FindTexture(Dark.Value + "Toolbar Minus@2x"), Labels.InputTooltip));

    private static Lazy<GUIContent> s_noInputIconShort = new Lazy<GUIContent>(() => new GUIContent(null, EditorGUIUtility.FindTexture(Dark.Value + "Toolbar Minus@2x"), Labels.InputTooltip));

    private static Lazy<GUIContent> s_noVisibilityWarn = new Lazy<GUIContent>(() => new GUIContent(FusionEditorSkin.WarningIcon, Labels.NoVisibilityWarn));

    private static Lazy<GUIContent> s_statsGC = new Lazy<GUIContent>(() => new GUIContent(string.Empty, Labels.StatsTooltip));

    private GUIStyle _toolbarButtonStyle;

    /// <summary>
    /// Window instance.
    /// </summary>
    public static FusionRunnerVisibilityControlsWindow Instance { get; private set; }

    private Vector2 _scrollPosition;
    private double _lastRepaintTime;
    private readonly Dictionary<NetworkRunner, FusionStats> _stats = new Dictionary<NetworkRunner, FusionStats>();

    /// <summary>
    /// Create window instance.
    /// </summary>
    [MenuItem("Window/Fusion/Network Runner Controls")]
    [MenuItem("Tools/Fusion/Windows/Network Runner Controls")]
    public static void ShowWindow() {
      var window = GetWindow(typeof(FusionRunnerVisibilityControlsWindow), false, "Network Runner Controls");
      window.minSize = new Vector2(WINDOW_MIN_W, WINDOW_MIN_H);
      Instance       = (FusionRunnerVisibilityControlsWindow)window;
    }

    private void Awake() {
      Instance = this;
    }

    private void OnEnable() {
      Instance = this;
    }

    private void OnDestroy() {
      Instance = null;
    }

    private void Update() {
      // Force a repaint every x seconds in case runner count and runner settings have changed.
      if (Time.realtimeSinceStartup - _lastRepaintTime > REFRESH_RATE) {
        Repaint();
      }
    }

    private void OnGUI() {
      _lastRepaintTime = Time.realtimeSinceStartup;

      var currentViewWidth = EditorGUIUtility.currentViewWidth;
      var isWide           = currentViewWidth > WIDE_SWITCH_WIDTH;
      var shortText        = currentViewWidth < TEXT_SWITCH_WIDTH;

      _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

      var runnerWithoutVisibilityEnabledDetected = false;

      if (!Application.isPlaying) {
        DrawRunnerRow(null, shortText, isWide, currentViewWidth, ref runnerWithoutVisibilityEnabledDetected);
      } else {
        var enumerator = NetworkRunner.GetInstancesEnumerator();
        while (enumerator.MoveNext()) {
          var runner = enumerator.Current;
          DrawRunnerRow(runner, shortText, isWide, currentViewWidth, ref runnerWithoutVisibilityEnabledDetected);
        }

        if (NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple) {
          EditorGUILayout.LabelField(Labels.HoldShift, EditorStyles.miniLabel);
          if (runnerWithoutVisibilityEnabledDetected) {
            EditorGUILayout.HelpBox(Labels.RunnerVisibilityNotEnabled, MessageType.Warning);
          }
        }
      }

      EditorGUILayout.EndScrollView();
    }

    private void DrawRunnerRow(NetworkRunner runner, bool shortText, bool isWide, float currentViewWidth, ref bool runnerWithoutVisibilityEnabledDetected) {
      var runnerIsNull = !runner;

      // Only show active runners. If not playing, we allow a null runner in order to render the disabled buttons.
      if (Application.isPlaying && (!runner || !runner.IsRunning)) {
        return;
      }

      var config = runnerIsNull ? default : runner.Config;

      var isSinglePeer = runnerIsNull || config?.PeerMode == NetworkProjectConfig.PeerModes.Single;

      using (new EditorGUI.DisabledGroupScope(runnerIsNull)) {
        EditorGUILayout.BeginHorizontal();
        {
          var localPlayer   = runnerIsNull ? default : runner.LocalPlayer;
          var localPlayerGC = s_playerObjGC.Value;
          localPlayerGC.text = localPlayer.IsRealPlayer ? Labels.P + localPlayer.PlayerId : Labels.Dash;

          var runnerName = runnerIsNull ? Labels.NoActiveRunner :
            shortText                   ? (runner.IsServer ? (runner.IsSinglePlayer ? Labels.SP : runner.IsPlayer ? Labels.H : Labels.S) : Labels.C) :
                                          runner.name;

          var runnerGC = s_runnerGC.Value;

          // Draw Runner Names/Buttons
          runnerGC.text = runnerIsNull ? Labels.NoRunners : runner.IsSharedModeMasterClient ? $"{runnerName} [MC]" : runnerName;
          var runnerRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.MinWidth(isWide ? RUNNR_BTTN_WIDE : RUNNR_BTTN_SLIM));
          if (GUI.Button(runnerRect, runnerGC, s_buttonStyle.Value)) {
            EditorGUIUtility.PingObject(runner);
            Selection.activeGameObject = runner.gameObject;
          }


          if (shortText == false) {
            // Draw PlayerRef Id / Local Player Object buttons
            var playerRefRect = EditorGUILayout.GetControlRect(GUILayout.Width(38));

            NetworkObject playerObj;
            try {
              playerObj = runner.GetPlayerObject(localPlayer);
            } catch {
              playerObj = null;
            }

            using (new EditorGUI.DisabledGroupScope(playerObj == false)) {
              if (GUI.Button(playerRefRect, localPlayerGC, s_buttonStyle.Value)) {
                if (playerObj) {
                  EditorGUIUtility.PingObject(runner.GetPlayerObject(localPlayer));
                }
              }
            }
          }

          // Draw Visibility Icons
          using (new EditorGUI.DisabledGroupScope(isSinglePeer)) {
            runnerGC.text = "";
            var toggleRect = EditorGUILayout.GetControlRect(GUILayout.Width(18));

            if (isSinglePeer || runner.HasVisibilityEnabled()) {
              if (GUI.Button(toggleRect, isSinglePeer || runner.GetVisible() ? s_visibleIcon.Value : s_hiddenIcon.Value, s_invisibleButtonStyle.Value)) {
                if ((Event.current.modifiers & (EventModifiers.Shift | EventModifiers.Control | EventModifiers.Command | EventModifiers.Alt)) == 0) {
                  runner.SetVisible(!runner.GetVisible());
                } else {
                  var others = NetworkRunner.GetInstancesEnumerator();
                  while (others.MoveNext()) {
                    var other = others.Current;
                    // Only consider active runners.
                    if (!other || !other.IsRunning) {
                      continue;
                    }

                    other.SetVisible(other == runner);
                  }
                }
              }
            } else {
              runnerWithoutVisibilityEnabledDetected = true;
              GUI.Label(toggleRect, s_noVisibilityWarn.Value);
            }
          }

          // Draw Provide Input icon/text
          using (new EditorGUI.DisabledGroupScope(runnerIsNull || runner.Mode == SimulationModes.Server)) {
            var inputToggleRect = EditorGUILayout.GetControlRect(GUILayout.Width(isWide ? 106 : 18));
            var providingInput  = runnerIsNull || runner.ProvideInput;
            var inputContent    = isWide ? (providingInput ? s_inputIconLong.Value : s_noInputIconLong.Value) : (providingInput ? s_inputIconShort.Value : s_noInputIconShort.Value);

            if (GUI.Button(inputToggleRect, inputContent, providingInput ? s_invisibleButtonStyle.Value : s_invisibleButtonGrayStyle.Value)) {
              if ((Event.current.modifiers & (EventModifiers.Shift | EventModifiers.Control | EventModifiers.Command | EventModifiers.Alt)) == 0) {
                runner.ProvideInput = !runner.ProvideInput;
              } else {
                var others = NetworkRunner.GetInstancesEnumerator();
                while (others.MoveNext()) {
                  var other = others.Current;
                  // Only consider active runners.
                  if (!other || !other.IsRunning) {
                    continue;
                  }

                  other.ProvideInput = other == runner;
                }
              }
            }
          }

          // Draw runtime stats creation buttons. Reflection used since this namespace can't see FusionStats.
          if (currentViewWidth >= WINDOW_MIN_W + 10) {
            var statsLeftRect  = EditorGUILayout.GetControlRect(GUILayout.Width(isWide ? STATS_BTTN_WIDE : STATS_BTTN_SLIM));
            var statsRightRect = EditorGUILayout.GetControlRect(GUILayout.Width(isWide ? STATS_BTTN_WIDE : STATS_BTTN_SLIM));
            var statsGC        = s_statsGC.Value;
            statsGC.text = isWide ? Labels.StatsLeft : Labels.ArrowsLeft;
            if (GUI.Button(statsLeftRect, statsGC, s_buttonStyle.Value)) {
              CreateOrUpdateFusionStats(runner, FusionStats.DefaultLayouts.Left);
            }

            statsGC.text = isWide ? Labels.StatsRight : Labels.ArrowsRight;
            if (GUI.Button(statsRightRect, statsGC, s_buttonStyle.Value)) {
              CreateOrUpdateFusionStats(runner, FusionStats.DefaultLayouts.Right);
            }
          }

          // Draw UserID
          if (currentViewWidth > 600) {
            using (new EditorGUI.DisabledGroupScope(true)) {
              var userIdString = runnerIsNull ? null : runner.UserId;
              var userIdRect   = EditorGUILayout.GetControlRect(GUILayout.MinWidth(40), GUILayout.ExpandWidth(true));
              GUI.Label(userIdRect, Labels.UserID + (userIdString ?? Labels.Dash), s_labelTinyStyle.Value);
            }
          }
        }

        EditorGUILayout.EndHorizontal();
      }
    }

    private void CreateOrUpdateFusionStats(NetworkRunner runner, FusionStats.DefaultLayouts layouts) {
      if (_stats.TryGetValue(runner, out var stats) == false) {
        stats = FusionStats.Create(runner: runner, screenLayout: layouts);
        EditorGUIUtility.PingObject(stats.gameObject);
        Selection.activeObject = stats.gameObject;

        _stats.Add(runner, stats);
      }

      stats.ResetLayout(screenLayout: layouts);
    }

    /// <summary>
    /// Draw buttons on toolbar.
    /// Automatically called by unity.
    /// </summary>
    /// <param name="position">Position of the button.</param>
    private void ShowButton(Rect position) {
      // button style
      if (_toolbarButtonStyle == null) {
        _toolbarButtonStyle = new GUIStyle(GUI.skin.button) { padding = new RectOffset() };
      }
    
      // draw button
      if (GUI.Button(position, EditorGUIUtility.IconContent("_Help"), _toolbarButtonStyle)) {
        Application.OpenURL("https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer");
      }
    }
  }
}