namespace Fusion {
  using System;
  using UnityEngine;
  using UI = UnityEngine.UI;
  using StatsInternal;
  using System.Collections.Generic;

#if UNITY_EDITOR
  using UnityEditor;
#endif

  /// <summary>
  /// Individual graph components generated and used by <see cref="FusionStats"/>.
  /// </summary>
  public class FusionStatsGraph : FusionStatsGraphBase {

    public enum Layouts {
      Auto,
      FullAuto,
      FullNoOverlap,
      CenteredAuto,
      CenteredNoGraph,
      CenteredNoOverlap,
      CompactAuto,
      CompactNoGraph,
    }

    public enum ShowGraphOptions {
      Never,
      OverlayOnly,
      Always,
    }

    enum ShaderType {
      None,
      Overlay,
      GameObject
    }

    const int MAX_FONT_SIZE_MIN_MAX = 32;
    const int MAX_TITLE_SIZE        = 26;
    const int MAX_AVG_SIZE          = 200;
    
    const int GRPH_TOP_PAD          = 36;
    const int GRPH_BTM_PAD          = 36;
    const int HIDE_XTRAS_WDTH       = 200;

    const int EXPAND_GRPH_THRESH = GRPH_BTM_PAD + GRPH_TOP_PAD + 40;
    const int COMPACT_THRESH = GRPH_BTM_PAD + GRPH_TOP_PAD - 10;
    
    // Not sure height is 0f much use anymore.
    [SerializeField][HideInInspector] public float Height = 50;

    /// <summary>
    /// Select between automatic formatting (based on size and aspect ratio of the graph) and manual selection of the various graph layouts available.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [Header("Graph Layout")]
    Layouts _layout;
    public Layouts Layout {
      get => _layout;
      set {
        _layout = value;
        CalculateLayout();
      }
    }

    /// <summary>
    /// Controls if the graph shader will render. Currently the graph shader only works in Overlay mode, so if forced to show Always here, it will not render correctly in 3d space.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    ShowGraphOptions _showGraph = ShowGraphOptions.Always;
    public ShowGraphOptions ShowGraph {
      get => _showGraph;
      set {
        _showGraph = value;
        CalculateLayout();
        _layoutDirty = _layoutDirty < 1 ? 1 : _layoutDirty;
      }
    }

    /// <summary>
    /// Padding added to text and other layout objects.
    /// </summary>
    [InlineHelp]
    public float Padding = 5f;

    /// <summary>
    /// The graph shaded area (which is only visible in Overlay mode) will expand to the full extends of the <see cref="FusionStatsGraph"/> component when this is enabled, regardless of size.
    /// When false, the graph will automatically expand only as needed.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    bool _alwaysExpandGraph;
    public bool AlwaysExpandGraph {
      get => _alwaysExpandGraph;
      set {
        _alwaysExpandGraph = value;
        CalculateLayout();
        _layoutDirty = _layoutDirty < 1 ? 1 : _layoutDirty;
      }
    }


    /// <summary>
    /// Exposes the UI labels and controls of <see cref="FusionStatsGraph"/>, so they may be modified if customizing this graph.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    bool _showUITargets;

    [DrawIf(nameof(_showUITargets), Hide = true)]
    public UI.Image GraphImg;
    [DrawIf(nameof(_showUITargets), Hide = true)]
    public UI.Text LabelMin;
    [DrawIf(nameof(_showUITargets), Hide = true)]
    public UI.Text LabelMax;
    [DrawIf(nameof(_showUITargets), Hide = true)]
    public UI.Text LabelAvg;
    [DrawIf(nameof(_showUITargets), Hide = true)]
    public UI.Text LabelLast;
    [DrawIf(nameof(_showUITargets), Hide = true)]
    public UI.Text LabelPer;

    // [DrawIf(nameof(_showUITargets), Hide = true)]
    // public UI.Dropdown _viewDropdown;
    [DrawIf(nameof(_showUITargets), Hide = true)]
    public UI.Button _avgBttn;

    float   _min;
    float   _max;
    float[] _values    = new float[OverTimeStatBuffer.CAPACITY];
    float[] _intensity = new float[OverTimeStatBuffer.CAPACITY];

#if UNITY_EDITOR


    protected override void OnValidate() {
      base.OnValidate();
      if (Application.isPlaying == false) {
        // This is here so when changes are made that affect graph names/colors they get applied immediately.
        TryConnect();
      }

#if UNITY_EDITOR
      if (Selection.activeGameObject == gameObject) {
        UnityEditor.EditorApplication.delayCall += CalculateLayout;
      }
#endif
      _layoutDirty = _layoutDirty < 1 ? 1 : _layoutDirty;
    }
#endif

    private void Reset() {
      _values = null;
      _intensity = null;
      _min = 0;
      _max = 0;

      ResetGraphShader();
    }

    public void Clear() {
      if (_values != null && _values.Length > 0) {
        Array.Clear(_values, 0, _values.Length);
        for (int i = 0; i < _intensity.Length; ++i) {
          _intensity[i] = -2;
        }
        _min = 0;
        _max = 0;
      }
    }

    public override void Initialize() {

      _avgBttn?.onClick.AddListener(CyclePer);
      ResetGraphShader();
    }

    void ResetGraphShader() {
      if (GraphImg) {
        ShaderType desiredShader = FusionStats != null ? (FusionStats.CanvasType == FusionStats.StatCanvasTypes.GameObject ? ShaderType.GameObject : ShaderType.Overlay) : ShaderType.None;
        //if (_currentShader != desiredShader) {
        //  GraphImg.material = desiredShader == ShaderType.GameObject ? new Material(Shader3D) : new Material(Shader);
        //  _currentShader = desiredShader;
        //}
        //GraphImg.material.renderQueue = 3000;
        Shader shader = _fusionStats.GraphShader; // Resources.Load<Shader>("FusionStatsGraphShader");
        GraphImg.material = new Material(shader);
        GraphImg.material.SetColor("_GoodColor", FusionStats.GraphColorGood);
        GraphImg.material.SetColor("_WarnColor", FusionStats.GraphColorWarn);
        GraphImg.material.SetColor("_BadColor",  FusionStats.GraphColorBad);
        GraphImg.material.SetColor("_FlagColor", FusionStats.GraphColorFlag);
        GraphImg.material.SetInt("_ZWrite", (desiredShader == ShaderType.GameObject ? 1 : 0));
      }
    }

    protected override void CyclePer() {
      base.CyclePer();
      SetAveragingText();
    }

    void SetAveragingText() {
      LabelPer.text =
        // Histograms only show avg per sample
        //(_graphVisualization == FusionGraphVisualization.ValueHistogram | _graphVisualization == FusionGraphVisualization.CountHistogram) ? "avg per Sample" :
        CurrentAveraging == StatAveraging.PerSample  ? "avg per Sample" : 
        CurrentAveraging == StatAveraging.PerSecond  ? "avg per Second":
        CurrentAveraging == StatAveraging.RecentPeak ? "Recent Peak":
        CurrentAveraging == StatAveraging.Peak       ? "Peak":
                                                       "Latest";
    }
    
    /// <summary>
    /// Returns true if the graph rendered. False if the size was too small and the graph was hidden.
    /// </summary>
    public override void Refresh() {

      if (_layoutDirty > 0) {
        CalculateLayout();
      }
      
      //var statsBuffer = StatsBuffer;
      if (StatSourceInfo == null) {
        return;
      }

      // TODO: move initialization of this to TryConnect?
      if (_values == null) {
        int size = OverTimeStatBuffer.CAPACITY;

        _values = new float[size];
        _intensity = new float[size];
      }
      UpdateGraph();
    }

    private double lastUpdateTime = 0f;
    
    static float[] reusableTimes  = new float[OverTimeStatBuffer.CAPACITY];
    
    void UpdateGraph() {

      if (StatsObject == null) {
        return;
      }
      

      // Boxing here... Would be nice to be able to get values from OverTimeStatsBuffer by index
      var statsBuffer = (OverTimeStatBuffer)FieldInfo.GetValue(StatsObject);
      var values      = _values;

      statsBuffer.CopyInto(ref reusableTimes, ref values);

      if (values == null || values.Length == 0) {
        return;
      }

      var    multiplier = _statSourceInfo.Multiplier;
      
      float  min        = float.MaxValue;
      float  max        = float.MinValue;
      double avg        = 0f;
      float  peak       = 0f;
      int    avgCount   = 0;
      float  last       = values.Length > 0 ? (float)values[^1] : 0;

      for (int i = 0; i < statsBuffer.Length; ++i) {
        float v = values[i] * multiplier;

        if (v < min) min = v;
        if (v > max) max = v;

        values[i] = v;
        
        // Averaging accumulation

        var averaging = CurrentAveraging;
        
        // Skip averaging if we are just using a basic peak, or just the basic last
        if (averaging == StatAveraging.Peak || averaging == StatAveraging.Latest) 
          continue;
        
        // Skip values which have already been handled in previous updates
        if (averaging == StatAveraging.RecentPeak && reusableTimes[i] <= lastUpdateTime)
          continue;

        lastUpdateTime = reusableTimes[i];
        if (averaging == StatAveraging.RecentPeak) {
          if (v > peak) {
            peak = v;
          }
          continue;
        }

        // actually doing some real averaging
        avg += v;
        avgCount++;
      }

      switch (CurrentAveraging) {
        case StatAveraging.PerSample: {
          avg /= avgCount;
          break;
        }
        case StatAveraging.PerSecond: {
          if (values.Length > 1) {
            double timeMin   = reusableTimes[0];
            double timeMax   = reusableTimes[^1];
            var    timeRange = timeMax - timeMin;
            avg = timeRange == 0 ? 0 : avg / timeRange;
          } else {
            avg = 0;
          }

          break;
        }
        case StatAveraging.Latest: {
          avg = values.Length > 0 ? values[^1] : 0;
          break;
        }
        case StatAveraging.Peak: {
          avg = max;
          break;
        }
        case StatAveraging.RecentPeak: {
          avg = peak;
          break;
        }
      }
      
      ApplyScaling(ref min, ref max);
      UpdateUiText(min, max, (float)avg, last);
    }

    void ApplyScaling(ref float min, ref float max) {

      if (min > 0) {
        min = 0;
      }

      if (max > _max) {
        _max = max;
      }

      if (min < _min) {
        _min = min;
      }

      var r = _max - _min;

      for (int i = 0, len = _values.Length; i < len; ++i) {
        var val = _values[i];
        var intensity =
          val < 0 ? -1f :
          val >= ErrorThreshold ? 1f :
          val >= WarnThreshold ? Mathf.Lerp(.5f, 1f, (val - WarnThreshold) / (ErrorThreshold - WarnThreshold)) :
          0f;

        _intensity[i] = intensity;
        _values[i] = Mathf.Clamp01((val - _min) / r);
      }
    }

    // Cached strings for UI/Shader
    private static readonly string[] _decimalFormats = new string[] { "F0", "F1", "F2", "F3","F4","F5","F6" };
    private static readonly int      _Data           = Shader.PropertyToID("_Data");
    private static readonly int      _Intensity      = Shader.PropertyToID("_Intensity");
    private static readonly int      _Count          = Shader.PropertyToID("_Count");
    private static readonly int      _Height         = Shader.PropertyToID("_Height");
    private static readonly int      _ZeroCenter     = Shader.PropertyToID("_ZeroCenter");

    void UpdateUiText(float min, float max, float avg, float last) {

      var decimalFormat = _decimalFormats[StatSourceInfo.Decimals];
      // TODO: At some point this label null checks should be removed
      if (LabelMin) { LabelMin.text   = min.ToString(decimalFormat); }
      if (LabelMax) { LabelMax.text   = max.ToString(decimalFormat); }
      if (LabelAvg) { LabelAvg.text   = avg.ToString(decimalFormat); }
      if (LabelLast) { LabelLast.text = last.ToString(decimalFormat); }

      if (GraphImg && GraphImg.enabled) {
        GraphImg.material.SetFloatArray(_Data, _values);
        GraphImg.material.SetFloatArray(_Intensity, _intensity);
        GraphImg.material.SetFloat(_Count, _values.Length);
        GraphImg.material.SetFloat(_Height, Height);
        GraphImg.material.SetFloat(_ZeroCenter, min < 0 ? min / (min - max) : 0);
      }

      _min = Mathf.Lerp(_min, 0, Time.deltaTime);
      _max = Mathf.Lerp(_max, 1, Time.deltaTime);
    }

    /// <summary>
    /// Creates a new GameObject with <see cref="FusionStatsGraph"/> and attaches it to the specified parent.
    /// </summary>
    public static FusionStatsGraph Create(FusionStats fusionStats, StatSourceTypes statSourceType, int statId, RectTransform parentRT) {

      var rootRT = parentRT.CreateRectTransform(statSourceType.GetLongName(statId));
      var graph = rootRT.gameObject.AddComponent<FusionStatsGraph>();
      graph._fusionStats = fusionStats;
      graph.Generate(statSourceType, statId, rootRT);

      return graph;
    }

    /// <summary>
    /// Generates the Graph UI for this <see cref="FusionStatsGraph"/>.
    /// </summary>
    public void Generate(StatSourceTypes type, int statId, RectTransform root) {

      _statSourceType = type;

      var labelFont = _fusionStats.LabelFont;
      var valueFont = _fusionStats.ValueFont;

      _statId = statId;

      root.anchorMin = new Vector2(0.5f, 0.5f);
      root.anchorMax = new Vector2(0.5f, 0.5f);
      root.anchoredPosition3D = default;

      var background = root.CreateRectTransform("Background")
        .ExpandAnchor();

      BackImage = background.gameObject.AddComponent<UI.Image>();
      BackImage.color = BackColor;
      BackImage.raycastTarget = false;

      var graphRT = background.CreateRectTransform("Graph")
        .SetAnchors(0.0f, 1.0f, 0.2f, 0.8f)
        .SetOffsets(0.0f, 0.0f, 0.0f, 0.0f);

      GraphImg = graphRT.gameObject.AddComponent<UI.Image>();
      GraphImg.raycastTarget = false;
      ResetGraphShader();

      var fontColor    = FusionStats.FontColor;
      var fontColorDim = FusionStats.FontColor * new Color(1, 1, 1, 0.5f);

      var titleRT = root.CreateRectTransform("Title")
        .ExpandAnchor()
        .SetOffsets(PAD, -PAD, 0.0f, -2.0f);
      titleRT.anchoredPosition = new Vector2(0, 0);
      

      
      LabelTitle                   = titleRT.AddText(name, TextAnchor.UpperRight, fontColor, labelFont, MAX_TITLE_SIZE);
      LabelTitle.raycastTarget     = true;
      
      // Top Left value
      var maxRT = root.CreateRectTransform("Max")
        .SetAnchors(0.0f, 0.3f, 0.8f, 1.0f)
        .SetOffsets(MRGN, 0.0f, 0.0f, -2.0f);
      LabelMax = maxRT.AddText("-", TextAnchor.UpperLeft, fontColorDim, valueFont, MAX_FONT_SIZE_MIN_MAX);

      // Bottom Left value
      var minRT = root.CreateRectTransform("Min")
        .SetAnchors(0.0f, 0.3f, 0.0f, 0.2f)
        .SetOffsets(MRGN, 0.0f, 0.0f, -2.0f);
      LabelMin = minRT.AddText("-", TextAnchor.LowerLeft, fontColorDim, valueFont, MAX_FONT_SIZE_MIN_MAX);

      // Main Center value
      var avgRT = root.CreateRectTransform("Avg")
        .SetOffsets(0.0f, 0.0f, 0.0f, 0.0f);
      avgRT.anchoredPosition = new Vector2(0, 0);
      LabelAvg               = avgRT.AddText("-", TextAnchor.LowerCenter, fontColor, valueFont);
      LabelAvg.raycastTarget = true;
      _avgBttn               = avgRT.gameObject.AddComponent<UI.Button>();

      // Main Center value
      var perRT = root.CreateRectTransform("Per")
        .SetAnchors(0.3f, 0.7f, 0.0f, 0.125f)
        .SetOffsets(MRGN, -MRGN, MRGN, 0.0f);
      LabelPer = perRT.AddText("avg per XXX", TextAnchor.LowerCenter, fontColor, labelFont, MAX_FONT_SIZE_MIN_MAX);

      // Bottom Right value
      var _lstRT = root.CreateRectTransform("Last")
        .SetAnchors(0.7f, 1.0f, 0.0f, 0.2f)
        .SetOffsets(PAD, -PAD, 0.0f, -2.0f);
      LabelLast = _lstRT.AddText("-", TextAnchor.LowerRight, fontColorDim, valueFont, MAX_FONT_SIZE_MIN_MAX);

      _layoutDirty = _layoutDirty < 1 ? 1 : _layoutDirty;
#if UNITY_EDITOR
      EditorUtility.SetDirty(this);
#endif

    }

    [EditorButton("CalculateLayout")]
    public override void CalculateLayout() {
      // This Try/Catch is here to prevent errors resulting from a delayCall to this method when entering play mode.
      try {
        if (gameObject == null) {
          return;
        }
      } catch {
        Debug.LogWarning($"This catch is needed it seems.");
        return;
      }

      if (gameObject.activeInHierarchy == false) {
        return;
      }
      _layoutDirty--;

      var rt = GetComponent<RectTransform>();

      if (StatSourceInfo == null) {
        TryConnect();
      }
      ApplyTitleText();

      SetAveragingText();
      
      var fusionStats = FusionStats;

      GraphImg.material.SetInt("_ZWrite", (fusionStats.CanvasType == FusionStats.StatCanvasTypes.GameObject ? 1 : 0));

      bool isOverlayCanvas = fusionStats.CanvasType == FusionStats.StatCanvasTypes.GameObject;
      bool vrSafe          = fusionStats.NoGraphShader /*&& _fusionStats.CanvasType == FusionStats.StatCanvasTypes.GameObject*/;

      var height = rt.rect.height;
      var width = rt.rect.width;


      Layouts layout;
      if (_layout != Layouts.Auto) {
        layout = _layout;
      } else {
        if (fusionStats.DefaultLayout != Layouts.Auto) {
          layout = fusionStats.DefaultLayout;
        } else {
          if (height < COMPACT_THRESH) {
            layout = Layouts.CompactAuto;
          } else {
            if (width < HIDE_XTRAS_WDTH) {
              layout = Layouts.CenteredAuto;
            } else {
              layout = Layouts.FullAuto;
            }
          }
        }
      }
      
      // Force layout to a non-text overlap mode if forced by the root FusionStats
      if (fusionStats.NoTextOverlap) {
        if (layout == Layouts.CenteredAuto) {
          layout = Layouts.CenteredNoOverlap;
        } else if (layout == Layouts.CompactAuto) {
          layout = Layouts.CompactNoGraph;
        } else if (layout == Layouts.FullAuto) {
          layout = Layouts.FullNoOverlap;
        }
      }

      bool noGraph   = vrSafe                    || layout == Layouts.CompactNoGraph || layout == Layouts.CenteredNoGraph || (fusionStats.NoTextOverlap && layout == Layouts.CompactAuto);
      bool noOverlap = fusionStats.NoTextOverlap || layout == Layouts.FullNoOverlap  || layout == Layouts.CenteredNoOverlap;
      bool showGraph = !noGraph && (ShowGraph == ShowGraphOptions.Always || (ShowGraph == ShowGraphOptions.OverlayOnly && isOverlayCanvas));

      bool expandGraph = !noOverlap && (_alwaysExpandGraph || !showGraph || layout == Layouts.CompactAuto || (!noOverlap && height < EXPAND_GRPH_THRESH));
      bool isSuperShort = height < MRGN * 3;

      var graphRT = GraphImg.rectTransform;
      if (graphRT) {
        graphRT.gameObject.SetActive(showGraph);

        if (expandGraph) {
          graphRT.SetAnchors(0, 1, 0, 1);
        } else {
          graphRT.SetAnchors(0, 1, noOverlap ?.25f : .2f, .8f);
        }
      }

      bool showExtras = layout == Layouts.FullAuto || layout == Layouts.FullNoOverlap /*|| (layout == Layouts.Compact && width > HIDE_XTRAS_WDTH)*/;

      var titleRT = LabelTitle.rectTransform;
      var avgRT = LabelAvg.rectTransform;

      var perRT = LabelPer.rectTransform;

      LabelAvg.resizeTextMaxSize = MAX_AVG_SIZE;
      
      void SetFullAutoBase() {
        titleRT.anchorMin    = new Vector2(showExtras ? 0.3f : 0.0f, expandGraph ? 0.6f : 0.8f);
        titleRT.anchorMax    = new Vector2(1.0f,                     1.0f);
        titleRT.offsetMin    = new Vector2(MRGN,                     0);
        titleRT.offsetMax    = new Vector2(-MRGN,                    -MRGN);
        LabelTitle.alignment = showExtras ? TextAnchor.UpperRight : TextAnchor.UpperCenter;

        LabelAvg.alignment = TextAnchor.LowerCenter;
        LabelPer.alignment = TextAnchor.UpperCenter;
      }
      
      switch (layout) {
        case Layouts.FullAuto: {
            SetFullAutoBase();
            avgRT.anchorMin = new Vector2(showExtras ? 0.3f : 0.0f, expandGraph ? 0.15f : 0.20f);
            avgRT.anchorMax = new Vector2(showExtras ? 0.7f : 1.0f, expandGraph ? 0.60f : 0.50f);
            avgRT.SetOffsets(0.0f, 0.0f, 0.0f, 0.0f);
            perRT.SetAnchors(0.3f, 0.7f, 0.0f, expandGraph ? 0.2f : 0.2f);
            perRT.SetOffsets(MRGN, -MRGN, MRGN, 0.0f);
            break;
          }
        case Layouts.FullNoOverlap: {
          SetFullAutoBase();
          avgRT.anchorMin = new Vector2(showExtras ? 0.3f : 0.0f, 0.10f);
          avgRT.anchorMax = new Vector2(showExtras ? 0.7f : 1.0f, 0.250f);
          avgRT.SetOffsets(0.0f, 0.0f, 0.0f, 0.0f);
          perRT.SetAnchors(0.3f, 0.7f, 0.0f, expandGraph ? 0.2f : 0.1f);
          perRT.SetOffsets(MRGN, -MRGN, MRGN, 0.0f);
          break;
        }

        case Layouts.CenteredNoOverlap:
        case Layouts.CenteredNoGraph:
        {
          titleRT.anchorMin    = new Vector2(0.0f,  expandGraph ? 0.5f : 0.8f);
          titleRT.anchorMax    = new Vector2(1.0f,  1.0f);
          titleRT.offsetMin    = new Vector2(MRGN,  0);
          titleRT.offsetMax    = new Vector2(-MRGN, -MRGN);
          LabelTitle.alignment = TextAnchor.UpperCenter;

          avgRT.anchorMin = new Vector2(0.0f, expandGraph ? 0.15f : 0.10f);
          avgRT.anchorMax = new Vector2(1.0f, expandGraph ? 0.50f : 0.25f);
          avgRT.SetOffsets(MRGN, -MRGN, 0.0f, 0.0f);

          perRT.SetAnchors(0.0f, 1.0f, 0.0f, expandGraph ? 0.2f : 0.1f);
          perRT.SetOffsets(MRGN, -MRGN, MRGN, 0.0f);
          LabelPer.alignment = TextAnchor.LowerCenter;

          LabelAvg.alignment = TextAnchor.LowerCenter;
          break;
        }
        
        case Layouts.CenteredAuto: {
            titleRT.anchorMin = new Vector2(0.0f, expandGraph ? 0.6f : 0.8f);
            titleRT.anchorMax = new Vector2(1.0f, 1.0f);
            titleRT.offsetMin = new Vector2(MRGN, 0);
            titleRT.offsetMax = new Vector2(-MRGN, -MRGN);
            LabelTitle.alignment = TextAnchor.UpperCenter;

            avgRT.anchorMin = new Vector2(0.0f, expandGraph ? 0.15f : 0.20f);
            avgRT.anchorMax = new Vector2(1.0f, expandGraph ? 0.60f : 0.50f);
            avgRT.SetOffsets(MRGN, -MRGN, 0.0f, 0.0f);

            perRT.SetAnchors(0.0f, 1.0f, 0.0f, expandGraph ? 0.2f : 0.2f);
            perRT.SetOffsets(MRGN, -MRGN,MRGN, -MRGN);

            LabelPer.alignment = TextAnchor.LowerCenter;

            LabelAvg.alignment = TextAnchor.LowerCenter;
            break;
          }

        case Layouts.CompactNoGraph:
        case Layouts.CompactAuto: {
            titleRT.anchorMin = new Vector2(0.0f, 0.0f);
            titleRT.anchorMax = new Vector2(0.5f, 1.0f);
            if (isSuperShort) {
              titleRT.SetOffsets(MRGN, 0, 0, 0);
              avgRT.SetOffsets(MRGN, -0, 0, 0);
            } else {
              titleRT.SetOffsets(MRGN * 2, 0, MRGN, -MRGN);
              avgRT.SetOffsets(MRGN, -0, MRGN, -MRGN);
            }
            LabelTitle.alignment = TextAnchor.MiddleLeft;

            avgRT.SetAnchors(0.5f, 1.0f, 0.00f, 1.00f);
            avgRT.SetOffsets(MRGN, -MRGN, MRGN, -MRGN);
            LabelAvg.resizeTextMaxSize = MAX_TITLE_SIZE;

            perRT.SetAnchors(0.5f, 1.0f, 0.05f, 0.25f);
            perRT.SetOffsets(MRGN, -MRGN, 0, 0);
            
            LabelPer.alignment = TextAnchor.LowerRight;

            LabelAvg.alignment = TextAnchor.MiddleRight;
            break;
          }
      }

      LabelMin.enabled = showExtras;
      LabelMax.enabled = showExtras;
      LabelLast.enabled = showExtras;
    }

  }
}