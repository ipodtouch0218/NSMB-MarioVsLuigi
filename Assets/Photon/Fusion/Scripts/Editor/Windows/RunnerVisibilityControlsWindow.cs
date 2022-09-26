namespace Fusion.Editor {
  using System;
  using System.Reflection;
  using UnityEngine;
  using UnityEditor;

  public class RunnerVisibilityControlsWindow : EditorWindow {

    const int WINDOW_MIN_W = 82;
    const int WINDOW_MIN_H = 48;

    const int STATS_BTTN_WIDE = 66;
    const int STATS_BTTN_SLIM = 24;
    const int RUNNR_BTTN_WIDE = 60;
    const int RUNNR_BTTN_SLIM = 24;
    const int FONT_SIZE = 9;

    const float TEXT_SWITCH_WIDTH = 200;
    const float WIDE_SWITCH_WIDTH = 380;

    const double REFRESH_RATE = 1f;

    private static Lazy<GUIStyle> s_labelStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(EditorStyles.label);
      result.fontSize = FONT_SIZE;
      return result;
    });

    private static Lazy<GUIStyle> s_labelTinyStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(s_labelStyle.Value);
      result.fontSize = FONT_SIZE - 1;
      return result;
    });

    private static Lazy<GUIStyle> s_labelCenterStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(s_labelStyle.Value);
      result.alignment = TextAnchor.MiddleCenter;
      return result;
    });

    private static Lazy<GUIStyle> s_buttonStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(EditorStyles.miniButton);
      result.fontSize = FONT_SIZE;
      return result;
    });

    private static Lazy<GUIStyle> s_helpboxStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(EditorStyles.helpBox);
      result.fontSize  = FONT_SIZE;
      result.alignment = TextAnchor.MiddleCenter;
      result.padding   = new RectOffset(6, 6, 6, 6);
      return result;
    });

    static Lazy<GUIStyle> s_invisibleButtonStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(EditorStyles.label);
      result.fontSize = FONT_SIZE;
      result.padding  = new RectOffset();
      return result;
    });
    static Lazy<GUIStyle> s_invisibleButtonGrayStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(EditorStyles.label);
      result.fontSize          = FONT_SIZE;
      result.normal.textColor  = Color.gray;
      result.active.textColor  = Color.gray;
      result.hover.textColor   = Color.gray;
      result.focused.textColor = Color.gray;
      result.padding = new RectOffset();
      return result;
    });

    private static Lazy<string> Dark = new Lazy<string>(() => {
      return EditorGUIUtility.isProSkin ? "d_" : "";
    });

    private static Lazy<GUIContent> s_toggleGC = new Lazy<GUIContent>(() => {
      return new GUIContent("", "Toggles IsVisible for this Runner. [Shift + Click] will solo the selected runner.");
    });

    static Lazy<GUIContent> s_visibleIcon = new Lazy<GUIContent>(() => {
      return new GUIContent(EditorGUIUtility.FindTexture(Dark.Value + "scenevis_visible_hover@2x"), "Click to toggle this NetworkRunner visibility,");
    });

    static Lazy<GUIContent> s_hiddenIcon = new Lazy<GUIContent>(() => {
      return new GUIContent(EditorGUIUtility.FindTexture(Dark.Value + "scenevis_hidden@2x"), "Click to toggle this NetworkRunner visibility,");
    });

    static Lazy<GUIContent> s_inputIconLong = new Lazy<GUIContent>(() => {
      return new GUIContent("\u2002Providing Inputs", EditorGUIUtility.FindTexture(Dark.Value + "UnityEditor.GameView@2x"), "");
    });

    static Lazy<GUIContent> s_inputIconShort = new Lazy<GUIContent>(() => {
      return new GUIContent(null, EditorGUIUtility.FindTexture(Dark.Value + "UnityEditor.GameView@2x"), "");
    });

    static Lazy<GUIContent> s_noInputIconLong = new Lazy<GUIContent>(() => {
      return new GUIContent("\u2002(No Inputs)", EditorGUIUtility.FindTexture(Dark.Value + "Toolbar Minus@2x"), "");
    });

    static Lazy<GUIContent> s_noInputIconShort = new Lazy<GUIContent>(() => {
      return new GUIContent(null, EditorGUIUtility.FindTexture(Dark.Value + "Toolbar Minus@2x"), "");
    });

    // EditorGUIUtility.FindTexture( Dark + "UnityEditor.GameView@2x" )

    public static RunnerVisibilityControlsWindow Instance { get; private set; }

    Vector2 _scrollPosition;
    double _lastRepaintTime;

    [MenuItem("Window/Fusion/Runner Visibility Controls")]
    [MenuItem("Fusion/Windows/Runner Visibility Controls")]
    public static void ShowWindow() {
      var window = GetWindow(typeof(RunnerVisibilityControlsWindow), false, "Runner Visibility Controls");
      window.minSize = new Vector2(WINDOW_MIN_W, WINDOW_MIN_H);
      Instance = (RunnerVisibilityControlsWindow)window;
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
      if ((Time.realtimeSinceStartup - _lastRepaintTime) > REFRESH_RATE)
        Repaint();
    }

    private void OnGUI() {

      _lastRepaintTime = Time.realtimeSinceStartup;

      var currentViewWidth = EditorGUIUtility.currentViewWidth;
      bool isWide      = currentViewWidth > WIDE_SWITCH_WIDTH;
      bool shortText   = currentViewWidth < TEXT_SWITCH_WIDTH;

      _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

      if (!Application.isPlaying) {
        EditorGUILayout.LabelField("No Active Runners", s_helpboxStyle.Value);
      } else {
        var enumerator = NetworkRunner.GetInstancesEnumerator();
        while (enumerator.MoveNext()) {
          var runner = enumerator.Current;

          // Only show active runners.
          if (!runner || !runner.IsRunning) {
            continue;
          }

          NetworkProjectConfig config = runner.Config;

          bool isSinglePeer = config.PeerMode == NetworkProjectConfig.PeerModes.Single;

          EditorGUILayout.BeginHorizontal();
          {
            var lclplayer = runner.LocalPlayer;
            var lclplayerid = lclplayer.IsValid ? "P" + lclplayer.PlayerId.ToString() : "--";

            string runnerName = 
              shortText ? (runner.IsServer ? (runner.IsSinglePlayer ? "SP" : runner.IsPlayer ? "H" : "S") : "C") : 
              runner.name;

            var toggleGuiContent = s_toggleGC.Value;


            // Draw Runner Names/Buttons
            toggleGuiContent.text = runnerName;
            var runnerrect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.MinWidth(isWide ? RUNNR_BTTN_WIDE : RUNNR_BTTN_SLIM));
            if (GUI.Button(runnerrect, s_toggleGC.Value, s_buttonStyle.Value)) {
              EditorGUIUtility.PingObject(runner);
              Selection.activeGameObject = runner.gameObject;
            }


            if (shortText == false) {

              // Draw PlayerRef Id / Local Player Object buttons
              var playerRefRect = EditorGUILayout.GetControlRect(GUILayout.Width(isWide ? 38 : 38));
              var playerObj = runner.GetPlayerObject(lclplayer);
              using (new EditorGUI.DisabledGroupScope(playerObj == false)) {

                if (GUI.Button(playerRefRect, lclplayerid, s_buttonStyle.Value)) {
                  if (playerObj) {
                    EditorGUIUtility.PingObject(runner.GetPlayerObject(lclplayer));
                  }
                }
              }
            }


            // Draw Visibility Icons
            using (new EditorGUI.DisabledGroupScope(isSinglePeer)) {
              toggleGuiContent.text = "";
              var togglerect = EditorGUILayout.GetControlRect(GUILayout.Width(18));

              if (GUI.Button(togglerect, runner.IsVisible ? s_visibleIcon.Value : s_hiddenIcon.Value, s_invisibleButtonStyle.Value)) {
                if ((Event.current.modifiers & (EventModifiers.Shift | EventModifiers.Control | EventModifiers.Command | EventModifiers.Alt)) == 0)
                {
                  runner.IsVisible = !runner.IsVisible;
                } else {
                  var others = NetworkRunner.GetInstancesEnumerator();
                  while (others.MoveNext()) {
                    var other = others.Current;
                    // Only consider active runners.
                    if (!other || !other.IsRunning) {
                      continue;
                    }
                    other.IsVisible = other == runner;
                  }
                }
              }
            };


            // Draw Provide Input icon/text
            using (new EditorGUI.DisabledGroupScope(runner.Mode == SimulationModes.Server)) {
              var inputToggleRect = EditorGUILayout.GetControlRect(GUILayout.Width(isWide ? 106 : 18));
              var inputContent = isWide ?
                (runner.ProvideInput ? s_inputIconLong.Value : s_noInputIconLong.Value) :
                (runner.ProvideInput ? s_inputIconShort.Value : s_noInputIconShort.Value);

              if (GUI.Button(inputToggleRect,inputContent, runner.ProvideInput ? s_invisibleButtonStyle.Value : s_invisibleButtonGrayStyle.Value)) {
                
                if ((Event.current.modifiers & (EventModifiers.Shift | EventModifiers.Control | EventModifiers.Command | EventModifiers.Alt)) == 0)
                {
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
            };


            // Draw runtime stats creation buttons. Reflection used since this namespace can't see FusionStats.

            if (currentViewWidth >= WINDOW_MIN_W + 10) {
              var statsleftrect = EditorGUILayout.GetControlRect(GUILayout.Width(isWide ? STATS_BTTN_WIDE : STATS_BTTN_SLIM));
              var statsrghtrect = EditorGUILayout.GetControlRect(GUILayout.Width(isWide ? STATS_BTTN_WIDE : STATS_BTTN_SLIM));
              if (GUI.Button(statsleftrect, isWide ? "<< Stats" : "<<", s_buttonStyle.Value)) {
                var stats = Type.GetType("FusionStats, Assembly-CSharp").GetMethod("CreateInternal", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { runner, 1, null, null });
                EditorGUIUtility.PingObject(((UnityEngine.Component)stats).gameObject);
                Selection.activeObject = ((UnityEngine.Component)stats).gameObject;
              }
              if (GUI.Button(statsrghtrect, isWide ? "Stats >>" : ">>", s_buttonStyle.Value)) {
                var stats = Type.GetType("FusionStats, Assembly-CSharp").GetMethod("CreateInternal", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { runner, 2, null, null });
                EditorGUIUtility.PingObject(((UnityEngine.Component)stats).gameObject);
                Selection.activeObject = ((UnityEngine.Component)stats).gameObject;
              }
            }

            // Draw UserID
            if (currentViewWidth > 600) {
              using (new EditorGUI.DisabledGroupScope(true)) {
                var userIdRect = EditorGUILayout.GetControlRect(GUILayout.MinWidth(40), GUILayout.ExpandWidth(true));
                GUI.Label(userIdRect, "UserID: " + ((runner.UserId == null) ? " --" : runner.UserId.ToString()), s_labelTinyStyle.Value);
              }
            }
          }

          EditorGUILayout.EndHorizontal();

        }
        if (NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple) {
          EditorGUILayout.LabelField("Hold Shift while clicking to solo Visibility/Input Provider.", EditorStyles.miniLabel);
        }
      }
      EditorGUILayout.EndScrollView();
    }
  }
}
