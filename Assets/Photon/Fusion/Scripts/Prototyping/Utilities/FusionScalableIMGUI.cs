using System.Reflection;
using UnityEngine;

/// <summary>
/// In-Game IMGUI style used for the <see cref="NetworkDebugStartGUI"/> interface.
/// </summary>
public static class FusionScalableIMGUI
{
  private static GUISkin _scalableSkin;

  private static void InitializedGUIStyles(GUISkin baseSkin) {
    _scalableSkin = baseSkin == null ? GUI.skin : baseSkin;

    // If no skin was provided, make the built in GuiSkin more tolerable.
    if (baseSkin == null) {
      _scalableSkin = GUI.skin;
      _scalableSkin.button.alignment = TextAnchor.MiddleCenter;
      _scalableSkin.label.alignment = TextAnchor.MiddleCenter;
      _scalableSkin.textField.alignment = TextAnchor.MiddleCenter;

      _scalableSkin.button.normal.background = _scalableSkin.box.normal.background;
      _scalableSkin.button.hover.background = _scalableSkin.window.normal.background;

      _scalableSkin.button.normal.textColor = new Color(.8f, .8f, .8f);
      _scalableSkin.button.hover.textColor = new Color(1f, 1f, 1f);
      _scalableSkin.button.active.textColor = new Color(1f, 1f, 1f);
      _scalableSkin.button.border = new RectOffset(6, 6, 6, 6);
      _scalableSkin.window.border = new RectOffset(8, 8, 8, 10);
    } else {
      // Use the supplied skin as the base.
      _scalableSkin = baseSkin;
    }
  }

  /// <summary>
  /// Get the custom scalable skin, already resized to the current screen. Provides the height, width, padding and margin used.
  /// </summary>
  /// <returns></returns>
  public static GUISkin GetScaledSkin(GUISkin baseSkin, out float height, out float width, out int padding, out int margin, out float boxLeft) {

    if(_scalableSkin == null) {
      InitializedGUIStyles(baseSkin);
    }

    var dimensions = ScaleGuiSkinToScreenHeight();
    height  = dimensions.Item1;
    width   = dimensions.Item2;
    padding = dimensions.Item3;
    margin  = dimensions.Item4;
    boxLeft = dimensions.Item5;
    return _scalableSkin;
  }

  /// <summary>
  /// Modifies a skin to make it scale with screen height.
  /// </summary>
  /// <param name="skin"></param>
  /// <returns>Returns (height, width, padding, top-margin, left-box-margin) values applied to the GuiSkin</returns>
  public static (float, float, int, int, float) ScaleGuiSkinToScreenHeight() {

    bool isVerticalAspect = Screen.height > Screen.width;
    bool isSuperThin = Screen.height / Screen.width > (17f / 9f);

    float height = Screen.height * .08f;
    float width = System.Math.Min(Screen.width * .9f, Screen.height * .6f);
    int padding = (int)(height / 4);
    int margin = (int)(height / 8);
    float boxLeft = (Screen.width - width) * .5f;

    int fontsize = (int)(isSuperThin ? (width - (padding * 2)) * .07f : height * .4f);
    var margins = new RectOffset(0, 0, margin, margin);

    _scalableSkin.button.fontSize = fontsize;
    _scalableSkin.button.margin = margins;
    _scalableSkin.label.fontSize = fontsize;
    _scalableSkin.label.padding = new RectOffset(padding, padding, padding, padding);
    _scalableSkin.textField.fontSize = fontsize;
    _scalableSkin.window.padding = new RectOffset(padding, padding, padding, padding);
    _scalableSkin.window.margin = new RectOffset(margin, margin, margin, margin);

    return (height, width, padding, margin, boxLeft);
  }
}

