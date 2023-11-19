namespace Fusion.Editor {
#if FUSION_WEAVER && UNITY_EDITOR
  using System;
  using System.Collections.Generic;
  using Photon.Realtime;
  using UnityEditor;
  using UnityEngine;
  using EditorUtility = UnityEditor.EditorUtility;

  public partial class FusionHubWindow : EditorWindow {
    private const int NavWidth = 256 + 2;

    private static bool? _ready; // true after InitContent(), reset onDestroy, onEnable, etc.

    private static Vector2 _windowSize;
    private static Vector2 _windowPosition = new Vector2(100, 100);

    private int _currentSection;

    [MenuItem("Tools/Fusion/Fusion Hub &f", false, 0)]
    public static void Open() {
      if (Application.isPlaying) {
        return;
      }

      var window = GetWindow<FusionHubWindow>(true, Constants.WindowTitle, true);
      window.position = new Rect(_windowPosition, _windowSize);
      window.Show();
    }

    private static void ReOpen() {
      if (_ready.HasValue && _ready.Value == false) {
        Open();
      }

      EditorApplication.update -= ReOpen;
    }

    private void OnEnable() {
      _ready = false;
      _windowSize = new Vector2(800, 540);

      this.minSize = _windowSize;

      // Pre-load Release History
      this.PrepareReleaseHistoryText();
      wantsMouseMove = true;
    }

    private void OnDestroy() {
      _ready = false;
    }

    private void OnGUI() {
      // skip until ready
      if (InitContent() == false) { return; }

      GUI.skin = FusionHubSkin;

      FusionGlobalScriptableObjectUtils.EnsureAssetExists<PhotonAppSettings>();
      FusionGlobalScriptableObjectUtils.EnsureAssetExists<NetworkProjectConfigAsset>();

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

      // Force repaints while mouse is over the window, to keep Hover graphics working (Unity quirk)
      var timeSinceStartup = Time.realtimeSinceStartupAsDouble;
      if (Event.current.type == EventType.MouseMove && timeSinceStartup > _nextForceRepaint) {
        // Cap the repaint rate a bit since we are forcing repaint on mouse move
        _nextForceRepaint = timeSinceStartup + .05f;
        Repaint();
      }
    }

    private double _nextForceRepaint;
    private Vector2 _scrollRect;

    private void DrawContent() {
      var section = _sections[_currentSection];
      GUILayout.Label(section.Description, headerTextStyle);

      using (new EditorGUILayout.VerticalScope(FusionHubSkin.box)) {
        using (var scrollScope = new EditorGUILayout.ScrollViewScope(_scrollRect)) {
          _scrollRect = scrollScope.scrollPosition;
          section.DrawMethod.Invoke();
        }
      }
    }

    private void DrawWelcomeSection() {
      // Top Welcome content box
      GUILayout.Label(Constants.WelcomeText);
      GUILayout.Space(16);

      DrawSetupAppIdBox();
    }

    private void DrawSetupSection() {
      DrawSetupAppIdBox();
      DrawButtonAction(Icon.FusionIcon, "Fusion Network Project Settings", "Network settings specific to Fusion.",
        callback: () => NetworkProjectConfigUtilities.PingGlobalConfigAsset(true));
      DrawButtonAction(Icon.PhotonCloud, "Photon App Settings", "Network settings specific to the Photon transport.",
        callback: () => {
          EditorGUIUtility.PingObject(Photon.Realtime.PhotonAppSettings.Global);
          Selection.activeObject = Photon.Realtime.PhotonAppSettings.Global;
        });
    }

    private void DrawDocumentationSection() {
      DrawButtonAction(Icon.Documentation, "Fusion Introduction", "The Fusion Introduction web page.", callback: OpenURL(Constants.UrlFusionIntro));
      DrawButtonAction(Icon.Documentation, "SDK and Release Notes", "Link to the latest Fusion version SDK.", callback: OpenURL(Constants.UrlFusionSDK));
      DrawButtonAction(Icon.Documentation, "API Reference", "The API library reference documentation.", callback: OpenURL(Constants.UrlFusionDocApi));
    }

    private void DrawSamplesSection() {
      GUILayout.Label("Tutorials", headerLabelStyle);
      DrawButtonAction(Icon.Samples, "Fusion 100 Tutorial", "Fusion Fundamentals Tutorial", callback: OpenURL(Constants.UrlFusion100));
    }

    private void DrawFusionReleaseSection() {
      GUILayout.Label(fusionReleaseHistory, releaseNotesStyle);
    }

    private void DrawReleaseHistoryItem(string label, List<string> items) {
      if (items != null && items.Count > 0) {
        GUILayout.BeginVertical();
        {
          GUILayout.Space(5);

          foreach (string text in items) {
            GUILayout.Label(string.Format("- {0}.", text), textLabelStyle);
          }
        }
        GUILayout.EndVertical();
      }
    }

    private void DrawSupportSection() {
      GUILayout.BeginVertical();
      GUILayout.Space(5);
      GUILayout.Label(Constants.Support, textLabelStyle);
      GUILayout.EndVertical();

      GUILayout.Space(15);

      DrawButtonAction(Icon.Community, Constants.DiscordHeader, Constants.DiscordText, callback: OpenURL(Constants.UrlDiscordGeneral));
      DrawButtonAction(Icon.Documentation, Constants.DocumentationHeader, Constants.DocumentationText, callback: OpenURL(Constants.UrlFusionDocsOnline));
    }

    private void DrawSetupAppIdBox() {
      var realtimeSettings = Photon.Realtime.PhotonAppSettings.Global;
      var realtimeAppId = realtimeSettings.AppSettings.AppIdFusion;
      // Setting up AppId content box.
      using (new EditorGUILayout.VerticalScope(FusionHubSkin.GetStyle("SteelBox") /*contentBoxStyle*/)) {
        GUILayout.Label(Constants.RealtimeAppidSetupInstructions);

        DrawButtonAction(Icon.PhotonCloud, "Open the Photon Dashboard", callback: OpenURL(Constants.UrlDashboard));
        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope(FusionHubSkin.GetStyle("SteelBox"))) {
          EditorGUI.BeginChangeCheck();
          GUILayout.Label("Fusion App Id:", GUILayout.Width(120));
          var icon = IsAppIdValid() ? CorrectIcon : EditorGUIUtility.FindTexture("console.erroricon.sml");
          GUILayout.Label(icon, GUILayout.Width(24), GUILayout.Height(24));
          var editedAppId = EditorGUILayout.DelayedTextField("", realtimeAppId, FusionHubSkin.textField, GUILayout.Height(24));
          if (EditorGUI.EndChangeCheck()) {
            if (Guid.TryParse(editedAppId, out _)) {
              var currentAppId = realtimeSettings.AppSettings.AppIdFusion;

              if (string.IsNullOrEmpty(currentAppId) || currentAppId.Equals(editedAppId) == false) {
                VSAttribution.SendAttributionEvent(editedAppId);
              }
            }
            
            realtimeSettings.AppSettings.AppIdFusion = editedAppId;
            EditorUtility.SetDirty(realtimeSettings);
            AssetDatabase.SaveAssets();
          }
        }
      }
    }

    private void DrawLeftNavMenu() {
      for (var i = 0; i < _sections.Length; ++i) {
        var section = _sections[i];
        if (DrawNavButton(section, _currentSection == i)) {
          _currentSection = i;
        }
      }
    }

    private void DrawHeader() {
      GUILayout.Label(GetIcon(Icon.ProductLogo), _navbarHeaderGraphicStyle);
    }

    private void DrawFooter() {
      GUILayout.BeginHorizontal(FusionHubSkin.window);
      {
        GUILayout.Label("\u00A9 2022, Exit Games GmbH. All rights reserved.");
      }
      GUILayout.EndHorizontal();
    }

    private bool DrawNavButton(Section section, bool currentSection) {
      var content = new GUIContent() { text = "  " + section.Title, image = GetIcon(section.Icon), };

      var renderStyle = currentSection ? buttonActiveStyle : GUI.skin.button;
      return GUILayout.Button(content, renderStyle);
    }

    private void DrawButtonAction(Icon icon, string header, string description = null, bool? active = null, Action callback = null, int? width = null) {
      DrawButtonAction(GetIcon(icon), header, description, active, callback, width);
    }

    private static void DrawButtonAction(Texture2D icon, string header, string description = null, bool? active = null, Action callback = null, int? width = null) {
      var padding = GUI.skin.button.padding.top + GUI.skin.button.padding.bottom;
      var height = icon.height + padding;

      var renderStyle = active.HasValue && active.Value == true ? buttonActiveStyle : GUI.skin.button;
      // Draw text separately (not part of button guiconent) to have control over the space between the icon and the text.
      var rect = EditorGUILayout.GetControlRect(false, height, width.HasValue ? GUILayout.Width(width.Value) : GUILayout.ExpandWidth(true));
      var clicked = GUI.Button(rect, icon, renderStyle);
      GUI.Label(new Rect(rect) { xMin = rect.xMin + icon.width + 20 },
        description == null ? "<b>" + header + "</b>" : string.Format("<b>{0}</b>\n{1}", header, "<color=#aaaaaa>" + description + "</color>"));
      if (clicked && callback != null) {
        callback.Invoke();
      }
    }

    /// <summary>
    /// Unity handling for post asset processing callback. Checks existence of settings assets every time assets change.
    /// </summary>
    private class FusionHubPostProcessor : AssetPostprocessor {
      private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
        EnsureAssetExists();
      }

      /// <summary>
      /// Attempts enforce existence of singleton. If Editor is not ready, this method will be deferred one editor update and try again until it succeeds.
      /// </summary>
      [UnityEditor.Callbacks.DidReloadScripts]
      private static void EnsureAssetExists() {
        if (!PhotonAppSettings.TryGetGlobal(out var global) || global.AppSettings.AppIdFusion == null) {
          FusionEditorLog.Trace($"Opening HUB due to missing settings.");
          EditorApplication.delayCall += () => { Open(); };
        }
      }
    }
  }
#endif
}