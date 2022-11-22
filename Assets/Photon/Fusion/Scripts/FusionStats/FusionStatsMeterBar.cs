using Fusion;
using UnityEngine;
using UnityEngine.UI;
using Stats = Fusion.Simulation.Statistics;
using Fusion.StatsInternal;

public class FusionStatsMeterBar : FusionGraphBase
{
  //public float WarningValueThreshold;
  //public float ErrorValueThreshold;


  public float HoldPeakTime = 0.1f;
  public float DecayTime = 0.25f;

  /// <summary>
  /// Values greater than 0 will limit the meter to a range of 0 to MeterMax.
  /// Value of 0 will adjust the max to the largest value occurance.
  /// </summary>
  [InlineHelp]
  public int MeterMax = 0;


  /// <summary>
  /// Exposes the UI labels and controls of <see cref="FusionGraph"/>, so they may be modified if customizing this graph.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  bool _showUITargets;

  [DrawIf(nameof(_showUITargets), Hide = true)]
  public Text ValueLabel;
  [DrawIf(nameof(_showUITargets), Hide = true)]
  public Image Bar;


  //public string CurrentLabel;
  //double _currentRawValue;
  double _currentDisplayValue;
  double _currentBarValue;
  Color CurrentColor;


  protected override Color BackColor => base.BackColor * new Color(.5f, .5f, .5f, 1);

#if UNITY_EDITOR

  protected override void OnValidate() {
    base.OnValidate();

    if (MeterMax < 0) {
      MeterMax = 0;
    }

    if (Application.isPlaying == false) {
      TryConnect();
      _layoutDirty = true;
    }
  }
#endif

  public override void Initialize() {
    base.Initialize();

    _max = MeterMax;
    // Prefabs lose editor generated sprites - recreate as needed.
    if (BackImage.sprite == null) {
      BackImage.sprite = FusionStatsUtilities.MeterSprite;
      Bar.sprite = BackImage.sprite;
    }

    // TODO: Can remove these later. Backwards compat for mask removal on Dec 30 2021
    BackImage.type = Image.Type.Simple;
    if (Bar.rectTransform.parent != BackImage.rectTransform.parent) {
      var oldMask = Bar.transform.parent;
      Bar.rectTransform.SetParent(BackImage.rectTransform.parent);
      Bar.transform.SetSiblingIndex(BackImage.transform.GetSiblingIndex() + 1);
      //Destroy(oldMask);
    }
    Bar.type = Image.Type.Filled;
    Bar.fillMethod = Image.FillMethod.Horizontal;
    Bar.fillAmount = 0;
  }

  double _lastImportedSampleTickTime;
  double _max;
  double _total;
  float  _lastPeakSetTime;

  public override void Refresh() {
    if (_layoutDirty) {
      CalculateLayout();
    }

    var statsBuffer = StatsBuffer;
    if (statsBuffer == null || statsBuffer.Count < 1) {
      return;
    }

    // Awkward temp RPC handling
    if (statsBuffer.DefaultVisualization == FusionGraphVisualization.CountHistogram) {

      if (statsBuffer.Count > 0) {
    
        int highestRpcsFoundForTick = 0;
        float newestSampleTick = statsBuffer.GetSampleAtIndex(statsBuffer.Count - 1).TickValue;
        var tick = newestSampleTick;
        // Only look back at ticks we have not yet already looked at on previous updates.
        if (newestSampleTick > _lastImportedSampleTickTime) {
          int tickRpcCount = 0;
          for (int i = statsBuffer.Count - 1; i >= 0; i--) {
            var sampletick = statsBuffer.GetSampleAtIndex(i).TickValue;

            if (sampletick > _lastImportedSampleTickTime) {
              // If we are now looking at samples from a different tick that previous for loop, reset to get count for this tick now.
              if (sampletick != tick) {
                tick = sampletick;
                // Capture the RPC count for the last recorded tick if it is the new high.
                if (tickRpcCount > highestRpcsFoundForTick) {
                  highestRpcsFoundForTick = tickRpcCount;
                }
                tickRpcCount = 0;
              }
              tickRpcCount++;
              _total++;
            } else {
              break;
            }
          }
          _lastImportedSampleTickTime = newestSampleTick;
        }

        SetValue(highestRpcsFoundForTick);
      }
      return;
    }

    if (statsBuffer.Count > 0) {
      var value = statsBuffer.GetSampleAtIndex(statsBuffer.Count - 1);
      if (value.TickValue == _fusionStats.Runner.Simulation.LatestServerState.Tick) {
        SetValue(value.FloatValue);
      } else {
        SetValue(0);
      }
    }
  }

  public void LateUpdate() {

    if (DecayTime <= 0) {
      return;
    }

    if (_currentBarValue <= 0) {
      return;
    }

    if (Time.time < _lastPeakSetTime + HoldPeakTime) {
      return;
    }

    double decayedVal = System.Math.Max(_currentBarValue - Time.deltaTime / DecayTime * _max, 0);
    SetBar(decayedVal);

  }

  public void SetValue(double rawvalue) {

    var info = StatSourceInfo;

    double multiplied = rawvalue * info.Multiplier;

    if (MeterMax == 0) {
      if (multiplied > _max) {
        _max = multiplied;
      }
    }


    double clampedValue = System.Math.Max(System.Math.Min(multiplied, _max), 0);
    var roundedValue = System.Math.Round(clampedValue, info.Decimals);
    var newDisplayValue = _total > 0 ? _total : roundedValue;

    if (clampedValue >= _currentBarValue) {
      _lastPeakSetTime = Time.time;
    }

    if (newDisplayValue != _currentDisplayValue) {
      ValueLabel.text = _total > 0 ? _total.ToString() : clampedValue.ToString();
      _currentDisplayValue = newDisplayValue;
    }

    // Only set values greater than the current shown value when using decay.
    if (DecayTime >= 0 && clampedValue <= _currentBarValue) {
      return;
    }

    if (clampedValue != _currentBarValue) {
      SetBar(clampedValue);
    }

  }

  void SetBar(double value) {

    var fusionStats = _fusionStats;

    Bar.fillAmount = (float)(value / _max);

    _currentBarValue = value;

    if (value < WarnThreshold) {
      var GoodColor = fusionStats.GraphColorGood;
      if (CurrentColor != GoodColor) {
        CurrentColor = GoodColor;
        Bar.color = GoodColor;
      }
    } else if (value < ErrorThreshold) {
      var WarnColor = fusionStats.GraphColorWarn;
      if (CurrentColor != WarnColor) {
        Bar.color = WarnColor;
        CurrentColor = WarnColor;
      }
    } else {
      var ErrorColor = fusionStats.GraphColorBad;
      if (CurrentColor != ErrorColor) {
        Bar.color = ErrorColor;
        CurrentColor = ErrorColor;
      }
    }
  }

  public override void CalculateLayout() {

    _layoutDirty = false;

    // Special padding handling because Arial vertically sits below center.
    var pad = LabelTitle.transform.parent.GetComponent<RectTransform>().rect.height * .2f;
    LabelTitle.rectTransform.offsetMax = new Vector2(0, -pad);
    LabelTitle.rectTransform.offsetMin = new Vector2(PAD, pad * 1.2f);

    ValueLabel.rectTransform.offsetMax = new Vector2(-PAD, -pad);
    ValueLabel.rectTransform.offsetMin = new Vector2(0, pad * 1.2f);

    ApplyTitleText();
  }

  // unfinished
  public static FusionStatsMeterBar Create(
    RectTransform parent, 
    FusionStats fusionStats,
    Stats.StatSourceTypes statSourceType, 
    int statId, 
    float warnThreshold,
    float alertThreshold
    ) {

    var info = Stats.GetDescription(statSourceType, statId);
    var barRT = parent.CreateRectTransform(info.LongName, true);
    var bar   = barRT.gameObject.AddComponent<FusionStatsMeterBar>();
    bar.StatSourceInfo = info;
    bar._fusionStats = fusionStats;
    //bar.WarningValueThreshold = warnThreshold;
    //bar.ErrorValueThreshold = alertThreshold;
    bar._statSourceType = statSourceType;
    bar._statId = statId;
    bar.GenerateMeter();
    return bar;
  }

  public void GenerateMeter() {

    var info = Stats.GetDescription(_statSourceType, _statId);
    var backRT = transform.CreateRectTransform("Back", true);
    BackImage = backRT.gameObject.AddComponent<Image>();
    BackImage.raycastTarget = false;
    BackImage.sprite = FusionStatsUtilities.MeterSprite;
    BackImage.color = BackColor;
    BackImage.type = Image.Type.Simple;
    var barRT = transform.CreateRectTransform("Bar", true);
    Bar = barRT.gameObject.AddComponent<Image>();
    Bar.raycastTarget = false;
    Bar.sprite = BackImage.sprite;
    Bar.color = _fusionStats.GraphColorGood;
    Bar.type = Image.Type.Filled;
    Bar.fillMethod = Image.FillMethod.Horizontal;
    Bar.fillAmount = 0;

    var titleRT = transform.CreateRectTransform("Label", true)
      .ExpandAnchor()
      .SetAnchors(0.0f, 0.5f, 0.0f,1.0f)
      .SetOffsets(6, -6, 6, -6);


    LabelTitle = titleRT.AddText(info.LongName, TextAnchor.MiddleLeft, _fusionStats.FontColor);
    LabelTitle.alignByGeometry = false;

    var valueRT = transform.CreateRectTransform("Value", true)
      .ExpandAnchor()
      .SetAnchors(0.5f, 1.0f, 0.0f, 1.0f)
      .SetOffsets(6, -6, 6, -6);
    
    ValueLabel = valueRT.AddText("0.0", TextAnchor.MiddleRight, _fusionStats.FontColor);
    ValueLabel.alignByGeometry = false;

  }

}
