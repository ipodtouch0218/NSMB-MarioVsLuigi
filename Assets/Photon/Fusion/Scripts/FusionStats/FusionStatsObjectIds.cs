
using UnityEngine;
using Fusion.StatsInternal;
using UI = UnityEngine.UI;

public class FusionStatsObjectIds : Fusion.Behaviour, IFusionStatsView {

  protected const int PAD = FusionStatsUtilities.PAD;
  protected const int MARGIN = FusionStatsUtilities.MARGIN;

  [SerializeField] [HideInInspector] UI.Text _inputValueText;
  [SerializeField] [HideInInspector] UI.Text _stateValueText;
  [SerializeField] [HideInInspector] UI.Text _objectIdLabel;

  [SerializeField] [HideInInspector] UI.Image _stateAuthBackImage;
  [SerializeField] [HideInInspector] UI.Image _inputAuthBackImage;

  FusionStats _fusionStats;

  void Awake() {
    _fusionStats = GetComponentInParent<FusionStats>();
  }

  static Color _noneAuthColor  = new Color(0.2f, 0.2f, 0.2f, 0.9f);
  static Color _inputAuthColor = new Color(0.1f, 0.6f, 0.1f, 1.0f);
  static Color _stateAuthColor = new Color(0.8f, 0.4f, 0.0f, 1.0f);

  void IFusionStatsView.Initialize() {

  }

  public static RectTransform Create(RectTransform parent, FusionStats fusionStats) {

    var rt = parent.CreateRectTransform("Object Ids Panel")
      .ExpandTopAnchor(MARGIN);

    var stats = rt.gameObject.AddComponent<FusionStatsObjectIds>();

    stats._fusionStats = fusionStats;

    stats.Generate();

    return rt;
  }
  
  const float LABEL_DIVIDING_POINT = .7f;
  const float TEXT_PAD = 4;
  const float TEXT_PAD_HORIZ = 6;
  const int   MAX_TAG_FONT_SIZE = 18;

  public void Generate() {



    var fontColor = _fusionStats.FontColor;
    var layoutRT = transform.CreateRectTransform("IDs Layout")
      .ExpandAnchor()
      .AddCircleSprite(_fusionStats.ObjDataBackColor)
      ;

    // Object ID panel on Left
    {
      var idPanelRT = layoutRT.CreateRectTransform("Object Id Panel", true)
        .ExpandTopAnchor()
        .SetAnchors(0, 0.4f, 0, 1);


      var objIdLabelRT = idPanelRT.CreateRectTransform("Object Id Label")
        .SetAnchors(0, 1, LABEL_DIVIDING_POINT, 1)
        .SetOffsets(TEXT_PAD_HORIZ, -TEXT_PAD_HORIZ, 0, -TEXT_PAD);
      
      objIdLabelRT.AddText("OBJECT ID", TextAnchor.MiddleCenter, fontColor)
        .resizeTextMaxSize = MAX_TAG_FONT_SIZE;

      var objIdValueRT = idPanelRT.CreateRectTransform("Object Id Value")
        .SetAnchors(0, 1, 0, LABEL_DIVIDING_POINT)
        .SetOffsets(TEXT_PAD_HORIZ, -TEXT_PAD_HORIZ, TEXT_PAD, 0);

      _objectIdLabel = objIdValueRT.AddText("00", TextAnchor.MiddleCenter, fontColor);
    }

    // Authority ID panel on right
    {

      AddAuthorityPanel(layoutRT, "Input", ref _inputValueText, ref _inputAuthBackImage)
        .SetAnchors(0.4f, 0.7f, 0, 1);

      AddAuthorityPanel(layoutRT, "State", ref _stateValueText, ref _stateAuthBackImage)
        .SetAnchors(0.7f, 1.0f, 0, 1);
    }
  }

  RectTransform AddAuthorityPanel(RectTransform parent, string label, ref UI.Text valueText, ref UI.Image backImage) {
    var fontColor = _fusionStats.FontColor;

    var authIdPanelRT = parent.CreateRectTransform($"{label} Id Panel", true)
        .ExpandTopAnchor()
        .SetAnchors(0.5f, 1, 0, 1)
        .AddCircleSprite(_noneAuthColor, out backImage);

    var authLabelRT = authIdPanelRT.CreateRectTransform($"{label} Label")
      .SetAnchors(0, 1, LABEL_DIVIDING_POINT, 1)
      .SetOffsets(TEXT_PAD_HORIZ, -TEXT_PAD_HORIZ, 0, -TEXT_PAD);

    var authorityText = authLabelRT.AddText(label, TextAnchor.MiddleCenter, fontColor);
    authorityText.resizeTextMaxSize = MAX_TAG_FONT_SIZE;

    var authValueRT = authIdPanelRT.CreateRectTransform($"{label} Value")
      .SetAnchors(0, 1, 0, LABEL_DIVIDING_POINT)
      .SetOffsets(TEXT_PAD_HORIZ, -TEXT_PAD_HORIZ, TEXT_PAD, 0);

    valueText = authValueRT.AddText("P0", TextAnchor.MiddleCenter, fontColor);

    return authIdPanelRT;
  }

  void IFusionStatsView.CalculateLayout() {
    //throw new System.NotImplementedException();
  }

  // cache of last applied UI values
  bool _previousHasInputAuth;
  bool _previousHasStateAuth;
  int _previousInputAuthValue = - 2;
  int _previousStateAuthValue = - 2;
  uint _previousObjectIdValue;

  //SimulationConfig.Topologies _previousTopology = (SimulationConfig.Topologies)(-1);

  void IFusionStatsView.Refresh() {
    if (_fusionStats == null) {
      return;
    }

    var obj = _fusionStats.Object;
    if (obj == null) {
      return;
    }

    if (obj.IsValid) {

      bool hasInputAuth = obj.HasInputAuthority;
      if (_previousHasInputAuth != hasInputAuth) {
        _inputAuthBackImage.color = hasInputAuth ? _inputAuthColor : _noneAuthColor;
        _previousHasInputAuth = hasInputAuth;
      }

      bool hasStateAuth = obj.HasStateAuthority || obj.Runner.IsServer;
      if (_previousHasStateAuth != hasStateAuth) {
        _stateAuthBackImage.color = hasStateAuth ? _stateAuthColor : _noneAuthColor;
        _previousHasStateAuth = hasStateAuth;
      }

      var inputAuth = obj.InputAuthority;
      if (_previousInputAuthValue != inputAuth) {
        _inputValueText.text = inputAuth == -1 ? "-" : "P" + inputAuth.PlayerId.ToString();
        _previousInputAuthValue = inputAuth;
      }

      var stateAuth = obj.StateAuthority;
      if (_previousStateAuthValue != stateAuth) {
        _stateValueText.text = stateAuth == -1 ? "-" : "P" + stateAuth.PlayerId.ToString();
        _previousStateAuthValue = stateAuth;
      }
    }

    uint objectId = obj.Id.Raw;
    if (objectId != _previousObjectIdValue) {
      _objectIdLabel.text = objectId.ToString();
      _previousObjectIdValue = objectId;
    }
  }
}
