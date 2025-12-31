namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// An editor scriptable object that stores UI skins and different <see cref="GUIStyle"/> Unity inspectors and custom windows.
  /// </summary>
#if QUANTUM_DEV
  [CreateAssetMenu(menuName = "Quantum/Editor Skin")]
#endif
  partial class QuantumEditorSkin : ScriptableObject {
    
    private static QuantumEditorSkin s_Instance;

    public static QuantumEditorSkin instance {
      get {
        if (s_Instance == null) {
          s_Instance           = CreateInstance<QuantumEditorSkin>();
          s_Instance.hideFlags = HideFlags.HideAndDontSave;
        }

        return s_Instance;
      }
    }
    
    public GUISkin Skin;
    
    public static GUIStyle HelpButtonStyle              => instance.Skin.GetStyle(EditorGUIUtility.isProSkin ? "dark-help-button" : "light-help-button");
    public static GUIStyle InlineBoxFullWidthStyle      => instance.Skin.GetStyle("inline-box-full-width");
    public static GUIStyle InlineBoxFullWidthScopeStyle => instance.Skin.GetStyle("inline-box-full-width-scope");
    public static GUIStyle ScriptHeaderBackgroundStyle  => instance.Skin.GetStyle("script-header-bg");
    public static GUIStyle ScriptHeaderIconStyle        => instance.Skin.GetStyle("script-header-icon");
    public static GUIStyle ScriptHeaderLabelStyle       => instance.Skin.GetStyle("script-header-label");
    public static GUIStyle RichLabelStyle               => instance.Skin.GetStyle(EditorGUIUtility.isProSkin ? "dark-rich-label" : "light-rich-label");
    public static GUIStyle InlineSelectorStyle          => instance.Skin.GetStyle("inline-selector");
    public static GUIStyle OutlineBoxStyle              => instance.Skin.GetStyle("outline-box");
    public static GUIStyle ThumbnailBoxStyle            => instance.Skin.GetStyle("thumbnail-box");
    public static GUIStyle ThumbnailImageStyle          => instance.Skin.GetStyle("thumbnail-image");

    public static Color    HelpInlineBoxColor          => EditorGUIUtility.isProSkin ? new Color(0.317f, 0.337f, 0.352f, 1.000f) : new Color(0.686f, 0.776f, 0.859f);
    public static Color    WarningInlineBoxColor       => EditorGUIUtility.isProSkin ? new Color(0.36f, 0.33f, 0.22f, 1.00f) : new Color(0.98f, 0.94f, 0.80f, 0.90f);
    public static Color    ErrorInlineBoxColor         => EditorGUIUtility.isProSkin ? new Color(0.40f, 0.15f, 0.10f, 1.00f) : new Color(0.9f, 0.70f, 0.70f, 1.00f);
    
    public static readonly LazyAsset<Texture2D> InfoIcon      = LazyAsset.Create(() => FindTextureOrThrow(EditorGUIUtility.isProSkin ? "d_console.infoicon.sml@2x" : "console.infoicon.sml@2x"));
    public static readonly LazyAsset<Texture2D> WarningIcon   = LazyAsset.Create(() => FindTextureOrThrow(EditorGUIUtility.isProSkin ? "d_console.warnicon.sml@2x" : "console.warnicon.sml@2x"));
    public static readonly LazyAsset<Texture2D> ErrorIcon     = LazyAsset.Create(() => FindTextureOrThrow(EditorGUIUtility.isProSkin ? "d_console.erroricon.sml@2x" : "console.erroricon.sml@2x"));
    public static readonly LazyAsset<Texture2D> LoadStateIcon = LazyAsset.Create(() => FindTextureOrThrow("blendSampler"));
    public static readonly LazyAsset<Texture2D> RefreshIcon   = LazyAsset.Create(() => FindTextureOrThrow(EditorGUIUtility.isProSkin ? "d_Refresh@2x" : "Refresh@2x"));
    
    public static readonly LazyGUIStyle OverlayLabelStyle = LazyGUIStyle.Create(_ => {
      var result = new GUIStyle(EditorStyles.miniLabel);
      result.alignment        = TextAnchor.MiddleRight;
      result.contentOffset    = new Vector2(-2, 0);
      result.normal.textColor = EditorGUIUtility.isProSkin ? new Color(255f / 255f, 221 / 255f, 0 / 255f, 1f) : Color.blue;
      return result;
    });
    
    public static readonly LazyGUIStyle RawDataStyle = LazyGUIStyle.Create(_ => new GUIStyle(EditorStyles.textArea) { wordWrap = true });
    
    private static Texture2D FindTextureOrThrow(string id) {
      var texture = EditorGUIUtility.FindTexture(id);
      if (texture) {
        return texture;
      }
      
      var icon = EditorGUIUtility.IconContent(id);
      if (icon?.image) {
        return (Texture2D)icon.image;
      }
      
      throw new ArgumentOutOfRangeException($"Could not find texture with id {id}");
    }

    private Dictionary<ScriptHeaderBackColor, Color> _scriptHeaderStyles = new() {
      { ScriptHeaderBackColor.None, new Color() },
      { ScriptHeaderBackColor.Gray, new Color(0.35f, 0.35f, 0.35f) },
      { ScriptHeaderBackColor.Blue, new Color(0.15f, 0.25f, 0.6f) },
      { ScriptHeaderBackColor.Red, new Color(0.5f, 0.1f, 0.1f) },
      { ScriptHeaderBackColor.Green, new Color(0.1f, 0.4f, 0.1f) },
      { ScriptHeaderBackColor.Orange, new Color(0.6f, 0.35f, 0.1f) },
      { ScriptHeaderBackColor.Black, new Color(0.1f, 0.1f, 0.1f) },
      { ScriptHeaderBackColor.Steel, new Color(0.32f, 0.35f, 0.38f) },
      { ScriptHeaderBackColor.Sand, new Color(0.38f, 0.35f, 0.32f) },
      { ScriptHeaderBackColor.Olive, new Color(0.25f, 0.33f, 0.15f) },
      { ScriptHeaderBackColor.Cyan, new Color(0.25f, 0.5f, 0.5f) },
      { ScriptHeaderBackColor.Violet, new Color(0.35f, 0.2f, 0.4f) },
    };

    public static Color GetScriptHeaderColor(ScriptHeaderBackColor settingsBackColor) {
      if (instance._scriptHeaderStyles.TryGetValue(settingsBackColor, out var color)) {
        return color;
      }
      return default;
    }
  }
}
