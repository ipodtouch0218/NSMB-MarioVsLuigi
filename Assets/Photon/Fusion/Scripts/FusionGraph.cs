using System;
using UnityEngine;
using Fusion;
using UI = UnityEngine.UI;
using Stats = Fusion.Simulation.Statistics;
using Fusion.StatsInternal;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Individual graph components generated and used by <see cref="FusionStats"/>.
/// </summary>
public class FusionGraph : FusionGraphBase {

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

  const int GRPH_TOP_PAD = 36;
  const int GRPH_BTM_PAD = 36;
  const int HIDE_XTRAS_WDTH = 200;
  const int INTERMITTENT_DATA_ARRAYSIZE = 128;

  const int EXPAND_GRPH_THRESH = GRPH_BTM_PAD + GRPH_TOP_PAD + 40;
  const int COMPACT_THRESH = GRPH_BTM_PAD + GRPH_TOP_PAD - 20;

  static Shader Shader {
    get => Resources.Load<Shader>("FusionGraphShader");
  }

  // Not sure height is 0f much use anymore.
  [SerializeField] [HideInInspector] public float Height = 50;

  /// <summary>
  /// Select between automatic formating (based on size and aspect ratio of the graph) and manual selection of the various graph layouts available.
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
      _layoutDirty = true;
    }
  }

  /// <summary>
  /// Padding added to text and other layout objects.
  /// </summary>
  [InlineHelp]
  public float Padding = 5f;

  /// <summary>
  /// The graph shaded area (which is only visible in Overlay mode) will expand to the full extends of the <see cref="FusionGraph"/> component when this is enabled, regardless of size.
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
      _layoutDirty = true;
    }
  }


  /// <summary>
  /// Exposes the UI labels and controls of <see cref="FusionGraph"/>, so they may be modified if customizing this graph.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  bool _showUITargets;

  [DrawIf(nameof(_showUITargets), true, DrawIfHideType.Hide)]
  public UI.Image GraphImg;
  [DrawIf(nameof(_showUITargets), true, DrawIfHideType.Hide)]
  public UI.Text LabelMin;
  [DrawIf(nameof(_showUITargets), true, DrawIfHideType.Hide)]
  public UI.Text LabelMax;
  [DrawIf(nameof(_showUITargets), true, DrawIfHideType.Hide)]
  public UI.Text LabelAvg;
  [DrawIf(nameof(_showUITargets), true, DrawIfHideType.Hide)]
  public UI.Text LabelLast;
  [DrawIf(nameof(_showUITargets), true, DrawIfHideType.Hide)]
  public UI.Text LabelPer;

  [DrawIf(nameof(_showUITargets), true, DrawIfHideType.Hide)]
  public UI.Dropdown _viewDropdown;
  [DrawIf(nameof(_showUITargets), true, DrawIfHideType.Hide)]
  public UI.Button _avgBttn;

  float _min;
  float _max;
  float[] _values;
  float[] _intensity;
  float[] _histogram;

#if UNITY_EDITOR

  
  protected override void OnValidate() {
    base.OnValidate();
    if (Application.isPlaying == false) {
      //This is here so when changes are made that affect graph names/colors they get applied immediately.
      TryConnect();
    }

#if UNITY_EDITOR
    if (Selection.activeGameObject == gameObject) {
      UnityEditor.EditorApplication.delayCall += CalculateLayout;
    }
#endif
    _layoutDirty = true;
  }
#endif

  List<int> DropdownLookup = new List<int>();

  protected override bool TryConnect() {

    bool isConnected = base.TryConnect();

    if (isConnected) {

      var flags = _statsBuffer.VisualizationFlags;

      DropdownLookup.Clear();
      _viewDropdown.ClearOptions();
      for (int i = 0; i < 16; ++i) {
        if (((int)flags & (1 << i)) != 0) {
          DropdownLookup.Add(1 << i);
          _viewDropdown.options.Add(new UI.Dropdown.OptionData(FusionStatsUtilities.CachedTelemetryNames[i + 1]));
          if ((1 << i & (int)_statsBuffer.DefaultVisualization) != 0) {
            _viewDropdown.value = i - 1;
          }
        }
      }
      SetPerText();
      return true;
    }
    return false;
  }

  [InlineHelp]
  FusionGraphVisualization _graphVisualization;
  public FusionGraphVisualization GraphVisualization {
    set {
      _graphVisualization = value;
      Reset();
    }
  }

  private void Reset() {
    _values = null;
    _histogram = null;
    _intensity = null;
    _min = 0;
    _max = 0;

    ResetGraphShader();
  }

  public void Clear() {
    if (_values != null && _values.Length > 0) {
      Array.Clear(_values, 0, _values.Length);
      Array.Clear(_histogram, 0, _histogram.Length);
      for (int i = 0; i < _intensity.Length; ++i) {
        _intensity[i] = -2;
      }
      _min = 0;
      _max = 0;
      _histoHighestUsedBucketIndex = 0;
      _histoAvg = 0;
      _histoAvgSampleCount = 0;
    }
  }

  public override void Initialize() {

    _viewDropdown?.onValueChanged.AddListener(OnDropdownChanged);
    _avgBttn?.onClick.AddListener(CyclePer);
  }

  public void OnDropdownChanged(int value) {
    GraphVisualization = (FusionGraphVisualization)DropdownLookup[value];
    SetPerText();
  }

  [BehaviourButtonAction("ResetShader")]
  void ResetShaderButton() {
    //TEST
    _intensity = new float[200];
    _values = new float[200];
    for (int i = 0; i < _values.Length; ++i) {
      _values[i] = (float)i / _values.Length;
      _intensity[i] = (float)i / 200; ;
    }

    GraphImg.material.SetFloat("_ZeroCenter", .3f);
    GraphImg.material.SetFloatArray("_Data", _values);
    GraphImg.material.SetFloatArray("_Intensity", _intensity);
    GraphImg.material.SetInt("_Count", _values.Length);
  }

  ShaderType _currentShader;
  
  void ResetGraphShader() {
    if (GraphImg) {
      ShaderType desiredShader = LocateParentFusionStats() != null ? (_fusionStats.CanvasType == FusionStats.StatCanvasTypes.GameObject ? ShaderType.GameObject : ShaderType.Overlay) : ShaderType.None;
      //if (_currentShader != desiredShader) {
      //  GraphImg.material = desiredShader == ShaderType.GameObject ? new Material(Shader3D) : new Material(Shader);
      //  _currentShader = desiredShader;
      //}
      //GraphImg.material.renderQueue = 3000;
      GraphImg.material = new Material(Shader);
      GraphImg.material.SetColor("_GoodColor", _fusionStats.GraphColorGood);
      GraphImg.material.SetColor("_WarnColor", _fusionStats.GraphColorWarn);
      GraphImg.material.SetColor("_BadColor", _fusionStats.GraphColorBad);
      GraphImg.material.SetColor("_FlagColor", _fusionStats.GraphColorFlag);
      GraphImg.material.SetInt("_ZWrite", (desiredShader == ShaderType.GameObject ? 1 : 0));
    }
  }

  public override void CyclePer() {
    if (_graphVisualization != FusionGraphVisualization.CountHistogram && _graphVisualization != FusionGraphVisualization.ValueHistogram) {
      base.CyclePer();
      SetPerText();
    }
  }

  void SetPerText() {
    // TODO: Temporary - here to avoid breaking existing implementations. Can be removed. Added Dec 15 2021
    if (LabelPer == null) {
      var prt = LabelAvg.rectTransform.parent.CreateRectTransform("Per")
      .SetAnchors(0.3f, 0.7f, 0.0f, 0.125f)
      .SetOffsets(MRGN, -MRGN, MRGN, 0.0f);
      LabelPer = prt.AddText("per sample", TextAnchor.LowerCenter, _fusionStats.FontColor);
    }

    LabelPer.text =
      // Histograms only show avg per sample
      (_graphVisualization == FusionGraphVisualization.ValueHistogram | _graphVisualization == FusionGraphVisualization.CountHistogram) ? "avg per Sample" :
      CurrentPer == Stats.StatsPer.Second ? "avg per Second" :
      CurrentPer == Stats.StatsPer.Tick   ? "avg per Tick" :
                                            "avg per Sample";
  }
  /// <summary>
  /// Returns true if the graph rendered. False if the size was too small and the graph was hidden.
  /// </summary>
  /// <returns></returns>
  public override void Refresh() {

    if (_layoutDirty) {
      CalculateLayout();
    }

    var statsBuffer = StatsBuffer;
    if (statsBuffer == null || statsBuffer.Count < 1) {
      return;
    }

    var visualization = _graphVisualization == FusionGraphVisualization.Auto ? _statsBuffer.DefaultVisualization : _graphVisualization;

    // TODO: move initialization of this to TryConnect?
    if (_values == null) {
      int size =
        visualization == FusionGraphVisualization.ContinuousTick ? statsBuffer.Capacity :
        visualization == FusionGraphVisualization.ValueHistogram ? StatSourceInfo.HistoBucketCount + 3 :// _histoBucketCount + 3 :
        INTERMITTENT_DATA_ARRAYSIZE;

      _values    = new float[size];
      _histogram = new float[size];
      _intensity = new float[size];
    }


    switch (visualization) {
      case FusionGraphVisualization.ContinuousTick: {
          UpdateContinuousTick(ref statsBuffer);
          break;
        }
      case FusionGraphVisualization.IntermittentTick: {
          UpdateIntermittentTick(ref statsBuffer);
          break;
        }
      case FusionGraphVisualization.IntermittentTime: {
          UpdateIntermittentTime(ref statsBuffer);
          break;
        }
      case FusionGraphVisualization.CountHistogram: {
          //UpdateIntermittentTime(data);
          break;
        }
      case FusionGraphVisualization.ValueHistogram: {
          UpdateTickValueHistogram(ref statsBuffer);
          break;
        }
    }
  }

  void UpdateContinuousTick(ref IStatsBuffer data) 
    {
    var min = float.MaxValue;
    var max = float.MinValue;
    var avg = 0f;
    var last = 0f;

    for (int i = 0; i < data.Count; ++i) {
      var v = (float)(StatSourceInfo.Multiplier * data.GetSampleAtIndex(i).FloatValue);

      min = Math.Min(v, min);
      max = Math.Max(v, max);

      if (i >= _values.Length)
        Debug.LogWarning(name + " Out of range " + i + " " + _values.Length + " " + data.Count);
      _values[i] = last = v;

      avg += v;
    }

    avg /= data.Count;

    ApplyScaling(ref min, ref max);
    UpdateUiText(min, max, avg, last);
  }

  // Intermittent Tick pulls values from a very short buffer, so we collect those values and merge them into a larger cache.
  (int tick, float value)[] _cachedValues;
  double _lastCachedTickTime;
  int    _lastCachedTick;

  void UpdateIntermittentTick(ref IStatsBuffer data) {

    if (_cachedValues == null) {
      _cachedValues = new (int, float)[INTERMITTENT_DATA_ARRAYSIZE];
    }

    int latestServerStateTick = _fusionStats.Runner.Simulation.LatestServerState.Tick;

    var min = float.MaxValue;
    var max = float.MinValue;
    var sum = 0f;
    var last = 0f;

    var oldestAllowedBufferedTick = latestServerStateTick - INTERMITTENT_DATA_ARRAYSIZE + 1;

    var tailIndex = latestServerStateTick % INTERMITTENT_DATA_ARRAYSIZE;
    var headIndex = (tailIndex + 1) % INTERMITTENT_DATA_ARRAYSIZE;

    int gapcheck = _lastCachedTick;
    // Copy all data from the buffer into our larger intermediate cached buffer
    for (int i = 0; i < data.Count; ++i) {
      var sample = data.GetSampleAtIndex(i);
      var sampleTick = sample.TickValue;
      
      // sample on buffer is older than the range we are displaying.
      if (sampleTick < oldestAllowedBufferedTick) {
        gapcheck = sampleTick;
        continue;
      }

      // sample on the buffer has already been merged into cached buffer.
      if (sampleTick <= _lastCachedTick) {
        gapcheck = sampleTick;
        continue;
      }

      // Fill any gaps in the buffer data 
      var gap = sampleTick - gapcheck;
      for (int g = gapcheck + 1; g < sampleTick; ++g) {
        _cachedValues[g % INTERMITTENT_DATA_ARRAYSIZE] = (g, 0);
      }

      _lastCachedTick = sampleTick;
      _cachedValues[sampleTick % INTERMITTENT_DATA_ARRAYSIZE] = (sampleTick, (float)(StatSourceInfo.Multiplier * sample.FloatValue));

      gapcheck = sampleTick;
    }

    // Loop through once to determine scaling
    for (int i = 0; i < INTERMITTENT_DATA_ARRAYSIZE; ++i) {
      var sample = _cachedValues[(i + headIndex) % INTERMITTENT_DATA_ARRAYSIZE];
      var v = sample.value;
      // Any outdated values are ticks that had no data, set them to zero.
      if (sample.tick < oldestAllowedBufferedTick) {
        sample.tick = oldestAllowedBufferedTick + i;
        sample.value = v = 0;
      }

      min = Math.Min(v, min);
      max = Math.Max(v, max);

      _values[i] = last = v;

      sum += v;
    }

    var avg = GetIntermittentAverageInfo(ref data, sum);
    ApplyScaling(ref min, ref max);
    UpdateUiText(min, max, avg, last);

  }

  void UpdateIntermittentTime(ref IStatsBuffer data) {
    var min = float.MaxValue;
    var max = float.MinValue;
    var sum = 0f;
    var last = 0f;

    for (int i = 0; i < data.Count; ++i) {
      var v = (float)(StatSourceInfo.Multiplier * data.GetSampleAtIndex(i).FloatValue);

      min = Math.Min(v, min);
      max = Math.Max(v, max);

      _values[i] = last = v;

      sum += v;
    }
    var avg = GetIntermittentAverageInfo(ref data, sum);

    ApplyScaling(ref min, ref max);
    UpdateUiText(min, max, avg, last);
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
        //val <= ErrorThreshold ? Mathf.Lerp(.5f, 1f, (val / WarnThreshold) - .5f) : // .5f :
        0f;

      _intensity[i] = intensity;
      _values[i] = Mathf.Clamp01((val - _min) / r);
    }
  }

  void UpdateUiText(float min, float max, float avg, float last) {

    var decimals = StatSourceInfo.Decimals;
    // TODO: At some point this label null checks should be removed
    if (LabelMin) { LabelMin.text = Math.Round(min, decimals).ToString(); }
    if (LabelMax) { LabelMax.text = Math.Round(max, decimals).ToString(); }
    if (LabelAvg) { LabelAvg.text = Math.Round(avg, decimals).ToString(); }
    if (LabelLast) { LabelLast.text = Math.Round(last, decimals).ToString(); }

    if (GraphImg && GraphImg.enabled) {
      GraphImg.material.SetFloatArray("_Data", _values);
      GraphImg.material.SetFloatArray("_Intensity", _intensity);
      GraphImg.material.SetFloat("_Count", _values.Length);
      GraphImg.material.SetFloat("_Height", Height);
      GraphImg.material.SetFloat("_ZeroCenter", min < 0 ? min / (min - max) : 0);
    }

    _min = Mathf.Lerp(_min, 0, Time.deltaTime);
    _max = Mathf.Lerp(_max, 1, Time.deltaTime);
  }


  float GetIntermittentAverageInfo(ref IStatsBuffer data, float sum) {

    switch (CurrentPer) {
      case Stats.StatsPer.Second: {
          var oldestTimeRecord = data.GetSampleAtIndex(0).TimeValue;
          var currentTime = (float)_fusionStats.Runner.Simulation.LatestServerState.Time;
          var avg = sum / (currentTime - oldestTimeRecord);
          return avg;
        }

      case Stats.StatsPer.Tick: {
          var oldestTickRecord = data.GetSampleAtIndex(0).TickValue;
          var currentTick = (float)_fusionStats.Runner.Simulation.LatestServerState.Tick;
          var avg = sum / (currentTick - oldestTickRecord);
          return avg;
        }

      default: {
          var avg = sum / _values.Length; // data.Count;
          return avg;
        }
    }
  }

  int    _histoHighestUsedBucketIndex;
  int    _histoAvgSampleCount;
  double _histoStepInverse;
  double _histoAvg;

  void UpdateTickValueHistogram(ref IStatsBuffer data) {

    var histoBucketCount = StatSourceInfo.HistoBucketCount;
    var histoMaxValue = StatSourceInfo.HistogMaxValue;

    // Determine histogram bucket sizes if they haven't yet been determined.
    if (_histoStepInverse == 0) {
      _histoStepInverse = histoBucketCount / StatSourceInfo.HistogMaxValue;
    }

    var mostCurrentSample = data.GetSampleAtIndex(data.Count - 1);
    var mostCurrentTick = mostCurrentSample.TickValue;
    var latestServerState = _fusionStats.Runner.Simulation.LatestServerState;
    var usingTick = mostCurrentTick > 0;
    double latestServerStateTickTime;
    
    if (usingTick) {
      latestServerStateTickTime = latestServerState.Tick;
      double mostCurrentSampleTickTime = mostCurrentTick;

      // count non-existent ticks as zero values. Only for tick based data.
      if (mostCurrentSampleTickTime < latestServerStateTickTime) {
        int countbackto = Math.Max((int)mostCurrentSampleTickTime, (int)_lastCachedTickTime);
        int newZeroCount = (int)latestServerStateTickTime - countbackto;
        float zerocountTotal = _histogram[0] + newZeroCount;
        _histogram[0] = zerocountTotal;

        if (zerocountTotal > _max) {
          _max = zerocountTotal;
        }
      }

    } else {
      latestServerStateTickTime = latestServerState.Time;
    }

    var info = StatSourceInfo;
    double multiplier = info.Multiplier;
    // Read data in stat buffer backwards until we reach a tick already recorded
    for (int i = data.Count - 1; i >= 0; --i) {
      var v = (float)(multiplier * data.GetSampleAtIndex(i).FloatValue);

      var sample = data.GetSampleAtIndex(i);
      double ticktime = usingTick ? sample.TickValue : sample.TimeValue;

      if (ticktime <= _lastCachedTickTime) {
        break;
      }

      var val = sample.FloatValue * multiplier;

      int bucketIndex;
      if (val == 0) {
        bucketIndex = 0;
      }
      else if (val == histoMaxValue) {
        bucketIndex = histoBucketCount;

      } 
      else if (val > histoMaxValue) {
        bucketIndex = histoBucketCount + 1;
      }      
      else {
        bucketIndex = (int)(val * _histoStepInverse) + 1;
      }

      _histoAvg = (_histoAvg * _histoAvgSampleCount + val) / (++_histoAvgSampleCount);

      var newval = _histogram[bucketIndex] + 1;
      
      if (newval > _max) {
        _max = newval;
      }
      _histogram[bucketIndex] = newval;

      if (bucketIndex > _histoHighestUsedBucketIndex) {
        _histoHighestUsedBucketIndex = bucketIndex;
      }
      //if (val > _histoHighestValue) {
      //  _histoHighestValue = val;
      //  _histoHighestUsedBucketIndex = bucketIndex;
      //}
    }

    int medianIndex = 0;
    float mostValues = 0;
    {
      var r = (_max - _min) * 1.1f;

      // Loop again to apply scaling
      for (int i = 0, cnt = _histogram.Length; i < cnt; ++i) {
        var value = _histogram[i];
        _intensity[i] = 0;
        if (i != 0 && value > mostValues) {
          mostValues = value;
          medianIndex = i;
        }
        _values[i] = Mathf.Clamp01((_histogram[i] - _min) / r);
      }
    }

    // Color the highest bar
    _intensity[medianIndex] = 2f;

    _lastCachedTickTime = latestServerStateTickTime;

    if (GraphImg && GraphImg.enabled) {
      GraphImg.material.SetFloatArray("_Data", _values);
      GraphImg.material.SetFloatArray("_Intensity", _intensity);
      GraphImg.material.SetFloat("_Count", _histoHighestUsedBucketIndex + 1);
      GraphImg.material.SetFloat("_Height", Height);
    }

    _min = 0;
    var decimals = info.Decimals;
    LabelMax.text  = $"<color=yellow>{Math.Ceiling((medianIndex + 1) / _histoStepInverse)}</color>";
    LabelAvg.text  = Math.Round(_histoAvg, decimals).ToString();
    LabelMin.text  = Math.Floor(_min).ToString();
    LabelLast.text = Math.Round((_histoHighestUsedBucketIndex + 1) / _histoStepInverse, decimals).ToString();
  }

  /// <summary>
  /// Creates a new GameObject with <see cref="FusionGraph"/> and attaches it to the specified parent.
  /// </summary>
  public static FusionGraph Create(FusionStats iFusionStats, Stats.StatSourceTypes statSourceType, int statId, RectTransform parentRT) {
    
    var statInfo = Stats.GetDescription(statSourceType, statId);

    var rootRT = parentRT.CreateRectTransform(statInfo.LongName);
    var graph = rootRT.gameObject.AddComponent<FusionGraph>();
    graph._fusionStats = iFusionStats;
    graph.Generate(statSourceType, (int)statId, rootRT);

    return graph;
  }

  /// <summary>
  /// Generates the Graph UI for this <see cref="FusionGraph"/>.
  /// </summary>
  public void Generate(Stats.StatSourceTypes type, int statId, RectTransform root) {

    _statSourceType = type;
    
    var rt = GetComponent<RectTransform>();
    
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

    var fontColor = _fusionStats.FontColor;
    var fontColorDim = _fusionStats.FontColor * new Color(1, 1, 1, 0.5f);

    var titleRT = root.CreateRectTransform("Title")
      .ExpandAnchor()
      .SetOffsets(PAD, -PAD, 0.0f, -2.0f);
    titleRT.anchoredPosition = new Vector2(0, 0);

    LabelTitle = titleRT.AddText(name, TextAnchor.UpperRight, fontColor);
    LabelTitle.resizeTextMaxSize = MAX_FONT_SIZE_WITH_GRAPH;
    LabelTitle.raycastTarget = true;

    // Top Left value
    var maxRT = root.CreateRectTransform("Max")
      .SetAnchors(0.0f, 0.3f, 0.85f, 1.0f)
      .SetOffsets(MRGN, 0.0f, 0.0f, -2.0f);
    LabelMax = maxRT.AddText("-", TextAnchor.UpperLeft, fontColorDim);

    // Bottom Left value
    var minRT = root.CreateRectTransform("Min")
      .SetAnchors(0.0f, 0.3f, 0.0f, 0.15f)
      .SetOffsets(MRGN, 0.0f, 0.0f, -2.0f);
    LabelMin = minRT.AddText("-", TextAnchor.LowerLeft, fontColorDim);

    // Main Center value
    var avgRT = root.CreateRectTransform("Avg")
      .SetOffsets(0.0f, 0.0f,  0.0f, 0.0f);
    avgRT.anchoredPosition = new Vector2(0, 0);
    LabelAvg = avgRT.AddText("-", TextAnchor.LowerCenter, fontColor);
    LabelAvg.raycastTarget = true;
    _avgBttn = avgRT.gameObject.AddComponent<UI.Button>();

    // Main Center value
    var perRT = root.CreateRectTransform("Per")
      .SetAnchors(0.3f, 0.7f, 0.0f, 0.125f)
      .SetOffsets(MRGN, -MRGN, MRGN, 0.0f);
    LabelPer = perRT.AddText("avg per Sample", TextAnchor.LowerCenter, fontColor);

    // Bottom Right value
    var _lstRT = root.CreateRectTransform("Last")
      .SetAnchors(0.7f, 1.0f, 0.0f,  0.15f)
      .SetOffsets(PAD, -PAD,  0.0f, -2.0f);
    LabelLast = _lstRT.AddText("-", TextAnchor.LowerRight, fontColorDim);

    _viewDropdown = titleRT.CreateDropdown(PAD, fontColor);

    _layoutDirty = true;
#if UNITY_EDITOR
    EditorUtility.SetDirty(this);
#endif

  }

  [BehaviourButtonAction("Update Layout")]
  public override void CalculateLayout() {
    // This Try/Catch is here to prevent errors resulting from a delayCall to this method when entering play mode.
    try {
      if (gameObject == null) {
        return;
      }
    } catch {
      return;
    }

    if (gameObject.activeInHierarchy == false) {
      return;
    }
    _layoutDirty = false;

    var rt = GetComponent<RectTransform>();

    if (_statsBuffer == null) {
      TryConnect();
    }
    ApplyTitleText();

    bool graphIsValid = StatSourceInfo.InvalidReason == null;

    LabelMin.gameObject.SetActive(graphIsValid);
    LabelMax.gameObject.SetActive(graphIsValid);
    LabelAvg.gameObject.SetActive(graphIsValid);
    LabelPer.gameObject.SetActive(graphIsValid);

    if (!graphIsValid) {
      LabelTitle.rectTransform.ExpandAnchor(PAD);
      LabelTitle.alignment = TextAnchor.MiddleCenter;
      LabelTitle.raycastTarget = false;
      _viewDropdown.gameObject.SetActive(false);
      return;
    }

    GraphImg.material.SetInt("_ZWrite", (_fusionStats.CanvasType == FusionStats.StatCanvasTypes.GameObject ? 1 : 0));


    bool isOverlayCanvas = _fusionStats.CanvasType == FusionStats.StatCanvasTypes.GameObject;
    bool vrSafe = _fusionStats.NoGraphShader /*&& _fusionStats.CanvasType == FusionStats.StatCanvasTypes.GameObject*/;

    var height = rt.rect.height;
    var width  = rt.rect.width;
    
    Layouts layout;
    if (_layout != Layouts.Auto) {
      layout = _layout;
    } else {
      if (_fusionStats.DefaultLayout != Layouts.Auto) {
        layout = _fusionStats.DefaultLayout;
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

    bool noGraph = vrSafe || layout == Layouts.CompactNoGraph || layout == Layouts.CenteredNoGraph || (_fusionStats.NoTextOverlap && layout == Layouts.CompactAuto);
    bool noOverlap = _fusionStats.NoTextOverlap || layout == Layouts.FullNoOverlap || layout == Layouts.CenteredNoOverlap;
    bool showGraph = !noGraph && (ShowGraph == ShowGraphOptions.Always || (ShowGraph == ShowGraphOptions.OverlayOnly && isOverlayCanvas));

    bool expandGraph = !noOverlap && (_alwaysExpandGraph || !showGraph || layout == Layouts.CompactAuto || (!noOverlap && height < EXPAND_GRPH_THRESH));
    bool isSuperShort = height < MRGN * 3;

    var graphRT = GraphImg.rectTransform;
    if (graphRT) {
      graphRT.gameObject.SetActive(showGraph);
      
      if (expandGraph) {
        graphRT.SetAnchors(0, 1, 0, 1);
      } else {
        graphRT.SetAnchors(0, 1, .25f, .8f);
      }
    }

    bool showExtras = layout == Layouts.FullAuto || layout == Layouts.FullNoOverlap /*|| (layout == Layouts.Compact && width > HIDE_XTRAS_WDTH)*/;

    var titleRT = LabelTitle.rectTransform;
    var avgRT = LabelAvg.rectTransform;

    // TODO: Temporary - here to avoid breaking existing implementations. Can be removed. Added Dec 15 2021
    if (LabelPer == null) {
      var prt = avgRT.parent.CreateRectTransform("Per")
      .SetAnchors(0.3f, 0.7f, 0.0f, 0.125f)
      .SetOffsets(MRGN, -MRGN, MRGN, 0.0f);
      LabelPer = prt.AddText("per sample", TextAnchor.LowerCenter, _fusionStats.FontColor);
    }

    var perRT = LabelPer.rectTransform;

    switch (layout) {
      case Layouts.FullNoOverlap:
      case Layouts.FullAuto: {
          titleRT.anchorMin = new Vector2(showExtras ? 0.3f : 0.0f, expandGraph ? 0.5f : 0.8f);
          titleRT.anchorMax = new Vector2(1.0f, 1.0f);
          titleRT.offsetMin = new Vector2(MRGN, 0);
          titleRT.offsetMax = new Vector2(-MRGN, -MRGN);
          LabelTitle.alignment = showExtras ? TextAnchor.UpperRight : TextAnchor.UpperCenter;

          avgRT.anchorMin = new Vector2(showExtras ? 0.3f : 0.0f, expandGraph ? 0.15f : 0.10f);
          avgRT.anchorMax = new Vector2(showExtras ? 0.7f : 1.0f, expandGraph ? 0.50f : 0.25f);
          avgRT.SetOffsets(0.0f, 0.0f, 0.0f, 0.0f);
          LabelAvg.alignment = TextAnchor.LowerCenter;

          perRT.SetAnchors(0.3f, 0.7f, 0.0f, expandGraph ? 0.2f : 0.1f);
          LabelPer.alignment = TextAnchor.LowerCenter;

          break;
        }
      case Layouts.CenteredNoOverlap:
      case Layouts.CenteredNoGraph:
      case Layouts.CenteredAuto: {
          titleRT.anchorMin = new Vector2(0.0f, expandGraph ? 0.5f : 0.8f);
          titleRT.anchorMax = new Vector2(1.0f, 1.0f);
          titleRT.offsetMin = new Vector2( MRGN,  0);
          titleRT.offsetMax = new Vector2(-MRGN, -MRGN);
          LabelTitle.alignment = TextAnchor.UpperCenter;

          avgRT.anchorMin = new Vector2(0.0f, expandGraph ? 0.15f : 0.10f);
          avgRT.anchorMax = new Vector2(1.0f, expandGraph ? 0.50f : 0.25f);
          avgRT.SetOffsets(MRGN, -MRGN, 0.0f, 0.0f);

          perRT.SetAnchors(0.0f, 1.0f, 0.0f, expandGraph ? 0.2f : 0.1f);
          LabelPer.alignment = TextAnchor.LowerCenter;

          LabelAvg.alignment = TextAnchor.LowerCenter;
          break;
        }

      case Layouts.CompactNoGraph:
      case Layouts.CompactAuto: {
          titleRT.anchorMin = new Vector2(0.05f, 0.0f);
          titleRT.anchorMax = new Vector2(0.5f, 1.0f);
          if (isSuperShort) {
            titleRT.SetOffsets(0,     0, 0, 0);
            avgRT  .SetOffsets(MRGN, -0, 0, 0);
          } else {
            titleRT.SetOffsets(0, 0,  MRGN, -MRGN);
            avgRT  .SetOffsets(MRGN, -0, MRGN, -MRGN);
          }
          LabelTitle.alignment = TextAnchor.MiddleLeft;

          avgRT.SetAnchors(0.5f, 0.95f, 0.0f, 1.0f);

          perRT.SetAnchors(0.5f, 0.95f, 0.0f, 0.15f)
               .SetOffsets(MRGN, -MRGN * 2, MRGN, 0.0f);
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