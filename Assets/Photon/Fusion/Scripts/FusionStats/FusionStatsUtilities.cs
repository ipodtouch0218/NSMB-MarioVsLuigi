using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UI = UnityEngine.UI;

namespace Fusion.StatsInternal {
  public interface IFusionStatsView {
    void Initialize();
    void CalculateLayout();
    void Refresh();
    bool isActiveAndEnabled { get; }
    Transform transform { get; }
  }

  public static class FusionStatsUtilities {

    public const int PAD           = 10;
    public const int MARGIN        = 6;
    public const int FONT_SIZE     = 12;
    public const int FONT_SIZE_MIN = 4;
    public const int FONT_SIZE_MAX = 200;


    static List<string> _cachedGraphVisualizationNames;
    public static List<string> CachedTelemetryNames {
      get {
        if (_cachedGraphVisualizationNames == null) {
          var enumtype = typeof(FusionGraphVisualization);
          var names = System.Enum.GetNames(enumtype);
          _cachedGraphVisualizationNames = new List<string>(names.Length);
          // Use Description for the nicified name
          for (int i = 0; i < names.Length; ++i) {
            string name;
            try {
              MemberInfo[] memberInfo = enumtype.GetMember(names[i]);
              var _Attribs = memberInfo[0].GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
              name = ((System.ComponentModel.DescriptionAttribute)(_Attribs[0])).Description;
            } catch {
              name = names[i];
            }
            _cachedGraphVisualizationNames.Add(name);
          }
        }
        return _cachedGraphVisualizationNames;
      }
    }

    static Font _font;
    public static Font Font {
      get {
        if (_font == null) {
          _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
         
        }
        return _font;
      }
    }

    const int METER_TEXTURE_WIDTH = 512;
    static Texture2D _meterTexture;
    static Texture2D MeterTexture {
      get {
        if (_meterTexture == null) {
          var tex = new Texture2D(METER_TEXTURE_WIDTH, 2);
          for (int x = 0; x < METER_TEXTURE_WIDTH; ++x) {
            for (int y = 0; y < 2; ++y) {
              var color = (x != 0 && x % 16 == 0) ? new Color(1f, 1f, 1f, 0.75f) : new Color(1f, 1f, 1f, 1f);
              tex.SetPixel(x, y, color);
            }
          }
          tex.Apply();
          return _meterTexture = tex;

        }
        return _meterTexture;
      }
    }

    static Sprite _meterSprite;
    public static Sprite MeterSprite {
      get {
        if (_meterSprite == null) {
          _meterSprite = Sprite.Create(MeterTexture, new Rect(0, 0, METER_TEXTURE_WIDTH, 2), new Vector2());
        }
        return _meterSprite;
      }
    }

    const int R = 64;

    static Texture2D _circle32Texture;
    static Texture2D Circle32Texture {
      get {
        if (_circle32Texture == null) {
          var tex = new Texture2D(R * 2, R * 2);
          for (int x = 0; x < R; ++x) {
            for (int y = 0; y < R; ++y) {
              double h = System.Math.Abs( System.Math.Sqrt(x * x + y * y));
              float a = h > R ? 0.0f : h < (R - 1) ? 1.0f :(float) (R - h);
              var c = new Color(1.0f, 1.0f, 1.0f, a);
              tex.SetPixel(R + 0 + x, R + 0 + y, c);
              tex.SetPixel(R - 1 - x, R + 0 + y, c);
              tex.SetPixel(R + 0 + x, R - 1 - y, c);
              tex.SetPixel(R - 1 - x, R - 1 - y, c);

            }
          }
          tex.Apply();
          return _circle32Texture = tex;
        }
        return _circle32Texture;
      }
    }

    static Sprite _circle32Sprite;
    public static Sprite CircleSprite {
      get {
        if (_circle32Sprite == null) {
          _circle32Sprite = Sprite.Create(Circle32Texture, new Rect(0, 0, R * 2, R * 2), new Vector2(R , R), 10f, 0, SpriteMeshType.Tight, new Vector4(R-1, R-1, R-1, R-1));
        }
        return _circle32Sprite;
      }
    }

    public static Color DARK_GREEN = new Color(0.0f, 0.5f, 0.0f, 1.0f);
    public static Color DARK_BLUE  = new Color(0.0f, 0.0f, 0.5f, 1.0f);
    public static Color DARK_RED   = new Color(0.5f, 0.0f, 0.0f, 1.0f);
    public static List<NetworkRunner> _reusableList = new List<NetworkRunner>(1);

    public static bool TryFindActiveRunner(FusionStats fusionStats, out NetworkRunner runner, SimulationModes? mode = null) {

      var gameObject = fusionStats.gameObject;
      var gameobjScene = fusionStats.gameObject.scene;

      var enumerator = NetworkRunner.GetInstancesEnumerator();
      while (enumerator.MoveNext()) {
        var found = enumerator.Current;
        if (found && found.IsRunning) {
          if (mode.HasValue && (mode.Value & found.Mode) == 0) {
            continue;
          }
          if (fusionStats.EnforceSingle) {
            runner = found;
            return true;
          } 
          if (found.SimulationUnityScene == gameobjScene) {
            runner = found;
            return true;
          }
        }
      }

      runner = null;
      return false;
    }

    public static RectTransform CreateRectTransform(this Transform parent, string name, bool expand = false) {
      var go = new GameObject(name);
      var rt = go.AddComponent<RectTransform>();
      rt.SetParent(parent);
      rt.localPosition = default;
      rt.localScale = default;
      rt.localScale = new Vector3(1, 1, 1);

      if (expand) {
        ExpandAnchor(rt);
      }
      return rt;
    }
    
    [System.Obsolete]
    internal static RectTransform CreateRectTransform(string name, Transform parent, bool expand = false) {
      var go = new GameObject(name);
      var rt = go.AddComponent<RectTransform>();
      rt.SetParent(parent);
      rt.localPosition = default;
      rt.localScale = default;
      rt.localScale = new Vector3(1, 1, 1);

      if (expand) {
        ExpandAnchor(rt);
      }
      return rt;
    }

    public static UI.Dropdown CreateDropdown(this RectTransform rt, float padding, Color fontColor) {
      var dropRT = rt.CreateRectTransform("Dropdown")
        .ExpandAnchor(-MARGIN);

      var dropimg = dropRT.gameObject.AddComponent<UI.Image>();
      var dropdown = dropRT.gameObject.AddComponent<UI.Dropdown>();
      dropimg.color = new Color(0, 0, 0, 0);
      dropdown.image = dropimg;
      
      var templateRT = dropRT.CreateRectTransform("Template", true)
        .ExpandTopAnchor()
        .SetOffsets(0, 0, -150, 0);

      var contentRT = templateRT.CreateRectTransform("Content")
        .ExpandTopAnchor()
        .SetOffsets(0, 0, -150, 0);
      
      var itemRT = contentRT.CreateRectTransform("Item", true)
        .SetAnchors(0, 1, 1, 1)
        .SetPivot(0.5f, 1)
        .SetSizeDelta(0, 50);
      
      var toggle = itemRT.gameObject.AddComponent<UI.Toggle>();
      toggle.colors = new UI.ColorBlock() {
        colorMultiplier = 1,
        normalColor = new Color(0.2f, 0.2f, 0.2f, 1f),
        highlightedColor = new Color(.3f, .3f, .3f, 1f),
        pressedColor = new Color(.4f, .4f, .4f, 4f),
        selectedColor = new Color(.25f, .25f, .25f, 1f),
      };
      var itemBackRT = itemRT.CreateRectTransform("Item Background", true);
      var itemBack = itemBackRT.gameObject.AddComponent<UI.Image>();

      var itemChckRT = itemRT.CreateRectTransform("Item Checkmark", true)
        .SetAnchors(0.05f, 0.1f, 0.1f, 0.9f)
        .SetOffsets(0, 0, 0, 0);

      var check = itemChckRT.gameObject.AddComponent<UI.Image>();
      check.sprite = CircleSprite;
      check.preserveAspect = true;

      var itemLablRT = itemRT.CreateRectTransform("Item Label", true)
        .SetAnchors(0.15f, 0.9f, 0.1f, 0.9f)
        .SetOffsets(0, 0, 0, 0);
      
      var itemLabl = itemLablRT.AddText("Sample", TextAnchor.UpperLeft, fontColor);
      itemLabl.alignment = TextAnchor.MiddleLeft;
      itemLabl.resizeTextMaxSize = 24;

      toggle.targetGraphic = itemBack;
      toggle.graphic = check;
      toggle.isOn = true;

      dropdown.template = templateRT;
      dropdown.itemText = itemLabl;

      templateRT.gameObject.SetActive(false);

      return dropdown;
    }


    public static UI.Text AddText(this RectTransform rt, string label, TextAnchor anchor, Color FontColor) {
      var text = rt.gameObject.AddComponent<UI.Text>();
      text.text = label;
      text.color = FontColor;
      text.font = Font;
      text.alignment = anchor;
      text.fontSize = FONT_SIZE;
      text.raycastTarget = false;
      //text.alignByGeometry   = true;
      text.resizeTextMinSize = FONT_SIZE_MIN;
      text.resizeTextMaxSize = FONT_SIZE_MAX;
      text.resizeTextForBestFit = true;
      return text;
    }

    public  const float BTTN_LBL_NORM_HGHT = .175f;
    private const int   BTTN_FONT_SIZE_MAX = 100;
    private const float BTTN_ALPHA         = 0.925f;

    internal static void MakeButton(this RectTransform parent, ref UI.Button button, string iconText, string labelText, out UI.Text icon, out UI.Text text, UnityAction action) {
      var rt = parent.CreateRectTransform(labelText);
      button = rt.gameObject.AddComponent<UI.Button>();

      var iconRt = rt.CreateRectTransform("Icon", true);
      iconRt.anchorMin = new Vector2(0, BTTN_LBL_NORM_HGHT);
      iconRt.anchorMax = new Vector2(1, 1.0f);
      iconRt.offsetMin = new Vector2(0, 0);
      iconRt.offsetMax = new Vector2(0, 0);

      icon = iconRt.gameObject.AddComponent<UI.Text>();
      button.targetGraphic = icon;
      icon.font = FusionStatsUtilities.Font;
      icon.text = iconText;
      icon.alignment = TextAnchor.MiddleCenter;
      icon.fontStyle = FontStyle.Bold;
      icon.fontSize = BTTN_FONT_SIZE_MAX;
      icon.resizeTextMinSize = 0;
      icon.resizeTextMaxSize = BTTN_FONT_SIZE_MAX;
      icon.alignByGeometry = true;
      icon.resizeTextForBestFit = true;

      var textRt = rt.CreateRectTransform("Label", true);
      textRt.anchorMin = new Vector2(0, 0);
      textRt.anchorMax = new Vector2(1, BTTN_LBL_NORM_HGHT);
      textRt.pivot = new Vector2(.5f, BTTN_LBL_NORM_HGHT * .5f);
      textRt.offsetMin = new Vector2(0, 0);
      textRt.offsetMax = new Vector2(0, 0);

      text = textRt.gameObject.AddComponent<UI.Text>();
      text.color = Color.black;
      text.font = FusionStatsUtilities.Font;
      text.text = labelText;
      text.alignment = TextAnchor.MiddleCenter;
      text.fontStyle = FontStyle.Bold;
      text.fontSize = 0;
      text.resizeTextMinSize = 0;
      text.resizeTextMaxSize = BTTN_FONT_SIZE_MAX;
      text.resizeTextForBestFit = true;
      text.horizontalOverflow = HorizontalWrapMode.Overflow;

      UI.ColorBlock colors = button.colors;
      colors.normalColor = new Color(.0f, .0f, .0f, BTTN_ALPHA);
      colors.pressedColor = new Color(.5f, .5f, .5f, BTTN_ALPHA);
      colors.highlightedColor = new Color(.3f, .3f, .3f, BTTN_ALPHA);
      colors.selectedColor = new Color(.0f, .0f, .0f, BTTN_ALPHA);
      button.colors = colors;

      button.onClick.AddListener(action);
    }

    public static RectTransform AddHorizontalLayoutGroup(this RectTransform rt, float spacing, int? rgtPad = null, int? lftPad = null, int? topPad = null, int? botPad = null) {
      var group = rt.gameObject.AddComponent<UI.HorizontalLayoutGroup>();
      group.childControlHeight = true;
      group.childControlWidth  = true;
      group.spacing = spacing;
      group.padding = new RectOffset(
        rgtPad.HasValue ? rgtPad.Value : 0,
        lftPad.HasValue ? lftPad.Value : 0,
        topPad.HasValue ? topPad.Value : 0,
        botPad.HasValue ? botPad.Value : 0
        );
      return rt;
    }

    public static RectTransform AddVerticalLayoutGroup(this RectTransform rt, float spacing, int? rgtPad = null, int? lftPad = null, int? topPad = null, int? botPad = null) {
      var group = rt.gameObject.AddComponent<UI.VerticalLayoutGroup>();
      group.childControlHeight = true;
      group.childControlWidth = true;
      group.spacing = spacing;
      //group.padding = new RectOffset(
      //  rgtPad.HasValue ? rgtPad.Value : 0,
      //  lftPad.HasValue ? lftPad.Value : 0,
      //  topPad.HasValue ? topPad.Value : 0,
      //  botPad.HasValue ? botPad.Value : 0
      //  );
      return rt;
    }

    public static UI.GridLayoutGroup AddGridlLayoutGroup(this RectTransform rt, float spacing, int? rgtPad = null, int? lftPad = null, int? topPad = null, int? botPad = null) {
      var group = rt.gameObject.AddComponent<UI.GridLayoutGroup>();
      group.spacing = new Vector2( spacing, spacing);
      //group.padding = new RectOffset(
      //  rgtPad.HasValue ? rgtPad.Value : 0,
      //  lftPad.HasValue ? lftPad.Value : 0,
      //  topPad.HasValue ? topPad.Value : 0,
      //  botPad.HasValue ? botPad.Value : 0
      //  );
      return group;
    }

    public static RectTransform AddImage(this RectTransform rt, Color color) {
      var image = rt.gameObject.AddComponent<UI.Image>();
      image.color = color;
      image.raycastTarget = false;
      return rt;
    }

    public static RectTransform AddCircleSprite(this RectTransform rt, Color color) {
      rt.AddCircleSprite(color, out var _);
      return rt;
    }

    public static RectTransform AddCircleSprite(this RectTransform rt, Color color, out UI.Image image) {
      image = rt.gameObject.AddComponent<UI.Image>();
      image.sprite = CircleSprite;
      image.type = UI.Image.Type.Sliced;
      image.pixelsPerUnitMultiplier = 100f;
      image.color = color;
      image.raycastTarget = false;
      return rt;

    }

    public static RectTransform ExpandAnchor(this RectTransform rt, float? padding = null) {
      rt.anchorMax = new Vector2(1, 1);
      rt.anchorMin = new Vector2(0, 0);
      rt.pivot = new Vector2(0.5f, 0.5f);
      if (padding.HasValue) {
        rt.offsetMin = new Vector2(padding.Value, padding.Value);
        rt.offsetMax = new Vector2(-padding.Value, -padding.Value);
      } else {
        rt.sizeDelta = default;
        rt.anchoredPosition = default;
      }
      return rt;
    }

    public static RectTransform ExpandTopAnchor(this RectTransform rt, float? padding = null) {
      rt.anchorMax = new Vector2(1, 1);
      rt.anchorMin = new Vector2(0, 1);
      rt.pivot = new Vector2(0.5f, 1f);
      if (padding.HasValue) {
        rt.offsetMin = new Vector2(padding.Value, padding.Value);
        rt.offsetMax = new Vector2(-padding.Value, -padding.Value);
      } else {
        rt.sizeDelta = default;
        rt.anchoredPosition = default;
      }
      return rt;
    }

    public static RectTransform ExpandMiddleLeft(this RectTransform rt) {
      rt.anchorMax = new Vector2(0, 0.5f);
      rt.anchorMin = new Vector2(0, 0.5f);
      rt.pivot = new Vector2(0.0f, .5f);
      return rt;
    }

    public static RectTransform SetSizeDelta(this RectTransform rt, float offsetX, float offsetY) {
      rt.sizeDelta = new Vector2(offsetX, offsetY);
      return rt;    
    }


    public static RectTransform SetOffsets(this RectTransform rt, float minX, float maxX, float minY, float maxY) {
      rt.offsetMin = new Vector2(minX, minY);
      rt.offsetMax = new Vector2(maxX, maxY);
      return rt;
    }

    public static RectTransform SetPivot(this RectTransform rt, float pivotX, float pivotY) {
      rt.pivot = new Vector2(pivotX, pivotY);
      return rt;
    }

    public static RectTransform SetAnchors(this RectTransform rt, float minX, float maxX, float minY, float maxY) {
      rt.anchorMin = new Vector2(minX, minY);
      rt.anchorMax = new Vector2(maxX, maxY);
      return rt;
    }

    const float GUIDE_MARGIN = .01f;
    const float GUIDE_MARGIN_HALF = GUIDE_MARGIN * .5f;

    internal static RectTransform MakeGuides(this RectTransform parent) {

      var outlineColor = new Color(0.5f, 0.5f, 0.5f, 0.75f);
      var rect = parent.CreateRectTransform("Guides", true);
      rect.SetSiblingIndex(0);

      var back = rect.CreateRectTransform("Back", true);
      back.gameObject.AddComponent<UI.Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.25f);

      var left = rect.CreateRectTransform("Left", true);
      left.anchorMin = new Vector2(-GUIDE_MARGIN, 0);
      left.anchorMax = new Vector2(0, 1);
      left.gameObject.AddComponent<UI.Image>().color = outlineColor;

      var right = rect.CreateRectTransform("Right", true);
      right.anchorMin = new Vector2(1, 0);
      right.anchorMax = new Vector2(1 + GUIDE_MARGIN, 1);
      right.gameObject.AddComponent<UI.Image>().color = outlineColor;

      var top = rect.CreateRectTransform("Top", true);
      top.anchorMin = new Vector2(-GUIDE_MARGIN, 1);
      top.anchorMax = new Vector2(1 + GUIDE_MARGIN, 1 + GUIDE_MARGIN);
      top.gameObject.AddComponent<UI.Image>().color = outlineColor;

      var bottom = rect.CreateRectTransform("Bottom", true);
      bottom.anchorMin = new Vector2(-GUIDE_MARGIN, -GUIDE_MARGIN);
      bottom.anchorMax = new Vector2(1 + GUIDE_MARGIN, 0);
      bottom.gameObject.AddComponent<UI.Image>().color = outlineColor;

      rect.CreateRectTransform("Center", true)
        .SetAnchors(0.5f - GUIDE_MARGIN_HALF, 0.5f + GUIDE_MARGIN_HALF, 0, 1)
        .AddImage(outlineColor);

      rect.CreateRectTransform("Middle", true)
        .SetAnchors(0, 1, 0.5f - GUIDE_MARGIN_HALF, 0.5f + GUIDE_MARGIN_HALF)
        .AddImage(outlineColor);

      return rect;
    }

    
  }
}

