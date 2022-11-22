using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UI = UnityEngine.UI;
using Fusion;
using Stats = Fusion.Simulation.Statistics;
using Fusion.StatsInternal;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Creates and controls a Canvas with one or multiple telemetry graphs. Can be created as a scene object or prefab,
/// or be created at runtime using the <see cref="Create"/> methods. If created as the child of a <see cref="NetworkObject"/>
/// then <see cref="EnableObjectStats"/> will automatically be set to true.
/// </summary>
[ScriptHelp(BackColor = EditorHeaderBackColor.Olive)]
[ExecuteAlways]
public class FusionStats : Fusion.Behaviour {

#if UNITY_EDITOR

  [MenuItem("Fusion/Add Fusion Stats", false, 1000)]
  [MenuItem("GameObject/Fusion/Add Fusion Stats")]
  public static void AddFusionStatsToScene() {

    var selected = Selection.activeGameObject;

    if (selected && PrefabUtility.IsPartOfPrefabAsset(selected)) {
      Debug.LogWarning("Open prefabs before running 'Add Fusion Stats' on them.");
      return;
    }

    var fs = new GameObject("FusionStats");

    if (selected) {
      fs.transform.SetParent(Selection.activeGameObject.transform);
    }

    fs.transform.localPosition = default;
    fs.transform.localRotation = default;
    fs.transform.localScale = Vector3.one;

    fs.AddComponent<FusionStatsBillboard>();
    fs.AddComponent<FusionStats>();
    EditorGUIUtility.PingObject(fs.gameObject);
    Selection.activeGameObject = fs.gameObject;
  }

#endif

  /// <summary>
  /// Options for displaying stats as screen overlays or world GameObjects.
  /// </summary>
  public enum StatCanvasTypes {
    Overlay,
    GameObject,
  }

  /// <summary>
  /// Predefined layout default options.
  /// </summary>
  public enum DefaultLayouts {
    Custom,
    Left,
    Right,
    UpperLeft,
    UpperRight,
    Full,
  }


  // Lookup for all FusionStats associated with active runners.
  static Dictionary<NetworkRunner, List<FusionStats>> _statsForRunnerLookup = new Dictionary<NetworkRunner, List<FusionStats>>();

  // Record of active SimStats, used to prevent more than one _guid version from existing (in the case of SimStats existing in a scene that gets cloned in Multi-Peer).
  static Dictionary<string, FusionStats> _activeGuids = new Dictionary<string, FusionStats>();

  // Added to make calling by reflection cleaner internally. Used in RunnerVisibilityControls.
  internal static FusionStats CreateInternal(NetworkRunner runner = null, DefaultLayouts layout = DefaultLayouts.Left, Stats.NetStatFlags? netStatsMask = null, Stats.SimStatFlags? simStatsMask = null) {
    return Create(null, runner, layout, layout, netStatsMask, simStatsMask);
  }

  /// <summary>
  /// Creates a new GameObject with a <see cref="FusionStats"/> component, attaches it to any supplied parent, and generates Canvas/Graphs.
  /// </summary>
  /// <param name="runner"></param>
  /// <param name="parent">Generated FusionStats component and GameObject will be added as a child of this transform.</param>
  /// <param name="objectLayout">Uses a predefined position.</param>
  /// <param name="netStatsMask">The network stats to be enabled. If left null, default statistics will be used.</param>
  /// <param name="simStatsMask">The simulation stats to be enabled. If left null, default statistics will be used.</param>
  /// <returns></returns>
  public static FusionStats Create(Transform parent = null, NetworkRunner runner = null, DefaultLayouts? screenLayout = null, DefaultLayouts? objectLayout = null, Stats.NetStatFlags? netStatsMask = null, Stats.SimStatFlags? simStatsMask = null) {

    var go = new GameObject($"{nameof(FusionStats)} {(runner ? runner.name : "null")}");
    FusionStats stats;
    if (parent) {
      go.transform.SetParent(parent);
    }

    stats = go.AddComponent<FusionStats>();

    stats.ResetInternal(null, netStatsMask, simStatsMask, objectLayout, screenLayout);

    stats.Runner = runner;

    if (runner != null) {
      stats.AutoDestroy = true;
    }
    return stats;
  }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStatics() {
    _statsForRunnerLookup.Clear();
    _activeGuids.Clear();
    _newInputSystemFound = null;
  }

  public static Stats.NetStatFlags DefaultNetStatsMask => Stats.NetStatFlags.RoundTripTime | Stats.NetStatFlags.ReceivedPacketSizes | Stats.NetStatFlags.SentPacketSizes;

  /// <summary>
  /// The gets the default SimStats. Some are only intended for Fusion internal development and aren't useful to users.
  /// </summary>
#if FUSION_DEV
  public const Stats.SimStatFlags DefaultSimStatsMask = (Stats.SimStatFlags)(-1);
#else
  public const Stats.SimStatFlags DefaultSimStatsMask =
    Stats.SimStatFlags.ForwardSimCount |
    Stats.SimStatFlags.ResimCount      |
    Stats.SimStatFlags.PacketSize;
#endif


  const int SCREEN_SCALE_W = 1080;
  const int SCREEN_SCALE_H = 1080;
  const float TEXT_MARGIN = 0.25f;
  const float TITLE_HEIGHT = 20f;
  const int MARGIN = FusionStatsUtilities.MARGIN;
  const int PAD = FusionStatsUtilities.PAD;

  const string PLAY_TEXT = "PLAY";
  const string PAUS_TEXT = "PAUSE";
  const string SHOW_TEXT = "SHOW";
  const string HIDE_TEXT = "HIDE";
  const string CLER_TEXT = "CLEAR";
  const string CNVS_TEXT = "CANVAS";
  const string CLSE_TEXT = "CLOSE";

  const string PLAY_ICON = "\u25ba";
  const string PAUS_ICON = "\u05f0";
  const string HIDE_ICON = "\u25bc";
  const string SHOW_ICON = "\u25b2";
  const string CLER_ICON = "\u1d13";
  const string CNVS_ICON = "\ufb26"; //"\u2261";
  const string CLSE_ICON = "x";

  // Used by DrawIfAttribute to determine inspector visibility of fields are runtime.
  bool ShowColorControls => !Application.isPlaying && _modifyColors;
  bool IsNotPlaying      => !Application.isPlaying;


  /// <summary>
  /// Interval (in seconds) between Graph redraws. Higher values (longer intervals) reduce CPU overhead, draw calls and garbage collection. 
  /// </summary>
  [InlineHelp]
  [Unit(Units.Seconds, 1f, 0f, DecimalPlaces = 2)]
  [MultiPropertyDrawersFix]
  public float RedrawInterval = .1f;


  /// <summary>
  /// Selects between displaying Canvas as screen overlay, or a world GameObject.
  /// </summary>
  [Header("Layout")]

  [InlineHelp]
  [SerializeField]
  StatCanvasTypes _canvasType;
  /// <summary>
  /// Selects between displaying Canvas as screen overlay, or a world GameObject.
  /// </summary>
  public StatCanvasTypes CanvasType {
    get => _canvasType;
    set {
      _canvasType = value;
      //_canvas.enabled = false;
      DirtyLayout(2);
    }
  }

  /// <summary>
  /// Enables text labels for the control buttons.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  bool _showButtonLabels = true;
  /// <summary>
  /// Enables text labels for the control buttons.
  /// </summary>
  public bool ShowButtonLabels {
    get => _showButtonLabels;
    set {
      _showButtonLabels = value;
      DirtyLayout();
    }
  }


  /// <summary>
  /// Height of button region at top of the stats panel. Values less than or equal to 0 hide the buttons, and reduce the header size.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [Range(0, 200)]
  [MultiPropertyDrawersFix]
  int _maxHeaderHeight = 70;
  /// <summary>
  /// Height of button region at top of the stats panel. Values less than or equal to 0 hide the buttons, and reduce the header size.
  /// </summary>
  public int MaxHeaderHeight {
    get => _maxHeaderHeight;
    set {
      _maxHeaderHeight = value;
      DirtyLayout();
    }
  }

  /// <summary>
  ///  The size of the canvas when <see cref="CanvasType"/> is set to <see cref="StatCanvasTypes.GameObject"/>.
  /// </summary>
  [InlineHelp]
  [DrawIf(nameof(_canvasType), (long)StatCanvasTypes.GameObject, Hide = true)]
  [Range(0, 20f)]
  [MultiPropertyDrawersFix]
  public float CanvasScale = 5f;

  /// <summary>
  /// The distance on the Z axis the canvas will be positioned. Allows moving the canvas in front of or behind the parent GameObject.
  /// </summary>
  [InlineHelp]
  [DrawIf(nameof(_canvasType), (long)StatCanvasTypes.GameObject, Hide = true)]
  [Range(-10, 10f)]
  [MultiPropertyDrawersFix]
  public float CanvasDistance = 0f;

  /// <summary>
  /// The Rect which defines the position of the stats canvas on a GameObject. Sizes are normalized percentages.(ranges of 0f-1f).
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(CanvasType), (long)StatCanvasTypes.GameObject, Hide = true)]
  [NormalizedRect(aspectRatio: 1)]
  [MultiPropertyDrawersFix]
  Rect _gameObjectRect = new Rect(0.0f, 0.0f, 0.3f, 1.0f);
  public Rect GameObjectRect {
    get => _gameObjectRect;
    set {
      _gameObjectRect = value;
      DirtyLayout();
    }
  }



  /// <summary>
  /// The Rect which defines the position of the stats canvas overlay on the screen. Sizes are normalized percentages.(ranges of 0f-1f).
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(CanvasType), (long)StatCanvasTypes.Overlay, Hide = true)]
  [NormalizedRect]
  [MultiPropertyDrawersFix]
  Rect _overlayRect = new Rect(0.0f, 0.0f, 0.3f, 1.0f);
  public Rect OverlayRect {
    get => _overlayRect;
    set {
      _overlayRect = value;
      DirtyLayout();
    }
  }


  /// <summary>
  /// <see cref="FusionGraph.Layouts"/> value which all child <see cref="FusionGraph"/> components will use if their <see cref="FusionGraph.Layouts"/> value is set to Auto.
  /// </summary>
  [Header("Fusion Graphs Layout")]
  [InlineHelp]
  [SerializeField]
  FusionGraph.Layouts _defaultLayout;
  public FusionGraph.Layouts DefaultLayout {
    get => _defaultLayout;
    set {
      _defaultLayout = value;
      DirtyLayout();
    }
  }

  /// <summary>
  /// UI Text on FusionGraphs can only overlay the bar graph if the canvas is perfectly facing the camera. 
  /// Any other angles will result in ZBuffer fighting between the text and the graph bar shader.
  /// For uses where perfect camera billboarding is not possible (such as VR), this toggle prevents FusionGraph layouts being used where text and graphs overlap.
  /// Normally leave this unchecked, unless you are experiencing corrupted text rendering.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  bool _noTextOverlap;
  public bool NoTextOverlap {
    get => _noTextOverlap;
    set {
      _noTextOverlap = value;
      DirtyLayout();
    }
  }

  /// <summary>
  /// Disables the bar graph in <see cref="FusionGraph"/>, and uses a text only layout.
  /// Enable this if <see cref="FusionGraph"/> is not rendering correctly in VR.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  bool _noGraphShader;
  public bool NoGraphShader {
    get => _noGraphShader;
    set {
      _noGraphShader = value;
      DirtyLayout();
    }
  }

  /// <summary>
  /// Force graphs layout to use X number of columns.
  /// </summary>
  [InlineHelp]
  [Range(0, 16)]
  [MultiPropertyDrawersFix]
  public int GraphColumnCount = 1;

  /// <summary>
  /// If <see cref="GraphColumnCount"/> is set to zero, then columns will automatically be added as needed to limit graphs to this width or less.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(GraphColumnCount), 0)]
  [Range(30, SCREEN_SCALE_W)]
  [MultiPropertyDrawersFix]
  int _graphMaxWidth = SCREEN_SCALE_W / 4;

  /// <summary>
  /// If <see cref="GraphColumnCount"/> is set to zero, then columns will automatically be added as needed to limit graphs to this width or less.
  /// </summary>
  public int GraphMaxWidth {
    get => _graphMaxWidth;
    set {
      _graphMaxWidth = value;
      DirtyLayout();
    }
  }

  /// <summary>
  /// Enables/Disables all NetworkObject related elements.
  /// </summary>
  [Header("Network Object Stats")]
  [InlineHelp]
  [SerializeField]
  [WarnIf(nameof(ShowMissingNetObjWarning), "No NetworkObject found on this GameObject, nor parent. Object stats will be unavailable.")]
  bool _enableObjectStats;
  public bool EnableObjectStats {
    get => _enableObjectStats;
    set {
      _enableObjectStats = value;
      DirtyLayout();
    }
  }

  bool ShowMissingNetObjWarning {
    get => _enableObjectStats && this.Object == null;
  }

  /// <summary>
  /// The <see cref="NetworkObject"/> source for any <see cref="Stats.ObjStats"/> specific telemetry.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(EnableObjectStats))]
  NetworkObject _object;
  public NetworkObject Object {
    get {
      if (_object == null) {
        _object = GetComponentInParent<NetworkObject>();
      }
      return _object;
    }
  }

  /// <summary>
  /// Height of Object title region at top of the stats panel.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(EnableObjectStats))]
  [Range(0, 200)]
  [MultiPropertyDrawersFix]
  int _objectTitleHeight = 48;
  public int ObjectTitleHeight {
    get => _objectTitleHeight;
    set {
      _objectTitleHeight = value;
      DirtyLayout();
    }
  }

  /// <summary>
  /// Height of Object info region at top of the stats panel.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(EnableObjectStats))]
  [Range(0, 200)]
  [MultiPropertyDrawersFix]
  int _objectIdsHeight = 60;
  public int ObjectIdsHeight {
    get => _objectIdsHeight;
    set {
      _objectIdsHeight = value;
      DirtyLayout();
    }
  }

  /// <summary>
  /// Height of Object info region at top of the stats panel.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(EnableObjectStats))]
  [Range(0, 200)]
  [MultiPropertyDrawersFix]
  int _objectMetersHeight = 90;
  public int ObjectMetersHeight {
    get => _objectMetersHeight;
    set {
      _objectIdsHeight = value;
      DirtyLayout();
    }
  }

  /// <summary>
  /// The <see cref="NetworkRunner"/> currently associated with this <see cref="FusionStats"/> component and graphs.
  /// </summary>
  [Header("Data")]
  [SerializeField]
  [InlineHelp]
  [EditorDisabled]
  [MultiPropertyDrawersFix]
  NetworkRunner _runner;
  public NetworkRunner Runner {
    get {

      if (Application.isPlaying == false) {
        return null;
      }

      // If the current runner shutdown, reset the runner so a new one can be found
      if (_runner) {
        if (_runner.IsShutdown) {
          Runner = null;
        } else {
          return _runner;
        }
      }

      if (Object) {
        var runner = _object.Runner;
        
        if (runner && (!EnforceSingle || (runner.Mode & ConnectTo) != 0)) {
          Runner = runner;
          return _runner;
        }
      }

      FusionStatsUtilities.TryFindActiveRunner(this, out var found, ConnectTo);

      Runner = found;
      return found;
    }
    set {
      if (_runner == value) {
        return;
      }
      // Keep track of which runners have active stats windows - needed so pause/unpause can affect all (since pause affects other panels)
      DisassociateWithRunner(_runner);
      _runner = value;
      AssociateWithRunner(value);

      UpdateTitle();
    }
  }

  /// <summary>
  /// Initializes a <see cref="FusionGraph"/> for all available stats, even if not initially included. 
  /// If disabled, graphs added after initialization will be added to the bottom of the interface stack.
  /// </summary>
  [InlineHelp]
  public bool InitializeAllGraphs;

  /// <summary>
  /// When <see cref="_runner"/> is null and no <see cref="NetworkRunner"/> exists in the current scene, FusionStats will continuously attempt to find and connect to an active <see cref="NetworkRunner"/> which matches these indicated modes.
  /// </summary>
  [InlineHelp]
  [VersaMask]
  [MultiPropertyDrawersFix]
  public SimulationModes ConnectTo = /*SimulationModes.Host | SimulationModes.Server | */SimulationModes.Client;

  /// <summary>
  /// Selects which NetworkObject stats should be displayed.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [VersaMask]
  [DrawIf(nameof(EnableObjectStats))]
  [MultiPropertyDrawersFix]
  Stats.ObjStatFlags _includedObjStats;
  public Stats.ObjStatFlags IncludedObjectStats {
    get => _includedObjStats;
    set {
      _includedObjStats = value;
      _activeDirty = true;
    }
  }

  /// <summary>
  /// Selects which NetConnection stats should be displayed.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [VersaMask]
  [MultiPropertyDrawersFix]
  Stats.NetStatFlags _includedNetStats;
  public Stats.NetStatFlags IncludedNetStats {
    get => _includedNetStats;
    set {
      _includedNetStats = value;
      _activeDirty = true;
    }
  }

  /// <summary>
  /// Selects which Simulation stats should be displayed.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [VersaMask]
  [MultiPropertyDrawersFix]
  Stats.SimStatFlags _includedSimStats;
  public Stats.SimStatFlags IncludedSimStats {
    get => _includedSimStats;
    set {
      _includedSimStats = value;
      _activeDirty = true;
    }
  }

  /// <summary>
  /// Automatically destroys this <see cref="FusionStats"/> GameObject if the associated runner is null or inactive.
  /// Otherwise attempts will continuously be made to find an new active runner which is running in <see cref="SimulationModes"/> specified by <see cref="ConnectTo"/>, and connect to that.
  /// </summary>
  [Header("Life-Cycle")]
  [InlineHelp]
  [SerializeField]
  public bool AutoDestroy;

  /// <summary>
  /// Only one instance with the <see cref="Guid"/> can exist. Will destroy any clones on Awake.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  public bool EnforceSingle = true;

  /// <summary>
  /// Identifier used to enforce single instances of <see cref="FusionStats"/> when running in Multi-Peer mode. 
  /// When <see cref="EnforceSingle"/> is enabled, only one instance of <see cref="FusionStats"/> with this GUID will be active at any time,
  /// regardless of the total number of peers running.
  /// </summary>
  [InlineHelp]
  [DrawIf(nameof(EnforceSingle))]
  [SerializeField]
  public string Guid;

  /// <summary>
  /// Shows/hides controls in the inspector for defining element colors.
  /// </summary>
  [Header("Customization")]
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(IsNotPlaying), Hide = true)]
  [MultiPropertyDrawersFix]
  private bool _modifyColors;
  public bool ModifyColors => _modifyColors;

  /// <summary>
  /// The color used for the telemetry graph data.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(ShowColorControls), Hide = true)]
  Color _graphColorGood = new Color(0.1f, 0.5f, 0.1f, 0.9f);

  /// <summary>
  /// The color used for the telemetry graph data.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(ShowColorControls), Hide = true)]
  Color _graphColorWarn = new Color(0.75f, 0.75f, 0.2f, 0.9f);

  /// <summary>
  /// The color used for the telemetry graph data.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(ShowColorControls), Hide = true)]
  Color _graphColorBad = new Color(0.9f, 0.2f, 0.2f, 0.9f);

  /// <summary>
  /// The color used for the telemetry graph data.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(ShowColorControls), Hide = true)]
  Color _graphColorFlag = new Color(0.8f, 0.75f, 0.0f, 1.0f);

  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(ShowColorControls), Hide = true)]
  Color _fontColor = new Color(1.0f, 1.0f, 1.0f, 1f);

  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(ShowColorControls), Hide = true)]
  Color PanelColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);

  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(ShowColorControls), Hide = true)]
  Color _simDataBackColor = new Color(0.1f, 0.08f, 0.08f, 1.0f);

  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(ShowColorControls), Hide = true)]
  Color _netDataBackColor = new Color(0.15f, 0.14f, 0.09f, 1.0f);

  [InlineHelp]
  [SerializeField]
  [DrawIf(nameof(ShowColorControls), Hide = true)]
  Color _objDataBackColor = new Color(0.0f, 0.2f, 0.4f, 1.0f);

  // IFusionStats interface requirements
  public Color FontColor        => _fontColor;
  public Color GraphColorGood   => _graphColorGood;
  public Color GraphColorWarn   => _graphColorWarn;
  public Color GraphColorBad    => _graphColorBad;
  public Color GraphColorFlag    => _graphColorFlag;
  public Color SimDataBackColor => _simDataBackColor;
  public Color NetDataBackColor => _netDataBackColor;
  public Color ObjDataBackColor => _objDataBackColor;

  //[Header("Graph Connections")]
  [SerializeField] [HideInInspector] FusionGraph[] _simGraphs;
  [SerializeField] [HideInInspector] FusionGraph[] _objGraphs;
  [SerializeField] [HideInInspector] FusionGraph[] _netGraphs;
  [NonSerialized] List<IFusionStatsView> _foundViews;
  [NonSerialized] List<FusionGraph> _foundGraphs;

  [SerializeField] [HideInInspector] UI.Text _titleText;

  [SerializeField] [HideInInspector] UI.Text _clearIcon;
  [SerializeField] [HideInInspector] UI.Text _pauseIcon;
  [SerializeField] [HideInInspector] UI.Text _togglIcon;
  [SerializeField] [HideInInspector] UI.Text _closeIcon;
  [SerializeField] [HideInInspector] UI.Text _canvsIcon;

  [SerializeField] [HideInInspector] UI.Text _clearLabel;
  [SerializeField] [HideInInspector] UI.Text _pauseLabel;
  [SerializeField] [HideInInspector] UI.Text _togglLabel;
  [SerializeField] [HideInInspector] UI.Text _closeLabel;
  [SerializeField] [HideInInspector] UI.Text _canvsLabel;
  [SerializeField] [HideInInspector] UI.Text _objectNameText;

  [SerializeField] [HideInInspector] UI.GridLayoutGroup _graphGridLayoutGroup;

  [SerializeField] [HideInInspector] Canvas _canvas;
  [SerializeField] [HideInInspector] RectTransform _canvasRT;
  [SerializeField] [HideInInspector] RectTransform _rootPanelRT;
  [SerializeField] [HideInInspector] RectTransform _guidesRT;
  [SerializeField] [HideInInspector] RectTransform _headerRT;
  [SerializeField] [HideInInspector] RectTransform _statsPanelRT;
  [SerializeField] [HideInInspector] RectTransform _graphsLayoutRT;
  [SerializeField] [HideInInspector] RectTransform _titleRT;
  [SerializeField] [HideInInspector] RectTransform _buttonsRT;
  [SerializeField] [HideInInspector] RectTransform _objectTitlePanelRT;
  [SerializeField] [HideInInspector] RectTransform _objectIdsGroupRT;
  [SerializeField] [HideInInspector] RectTransform _objectMetersPanelRT;
  [SerializeField] [HideInInspector] RectTransform _clientIdPanelRT;
  [SerializeField] [HideInInspector] RectTransform _authorityPanelRT;

  [SerializeField] [HideInInspector] UI.Button _titleButton;
  [SerializeField] [HideInInspector] UI.Button _objctButton;
  [SerializeField] [HideInInspector] UI.Button _clearButton;
  [SerializeField] [HideInInspector] UI.Button _togglButton;
  [SerializeField] [HideInInspector] UI.Button _pauseButton;
  [SerializeField] [HideInInspector] UI.Button _closeButton;
  [SerializeField] [HideInInspector] UI.Button _canvsButton;

  public Rect CurrentRect => _canvasType == StatCanvasTypes.GameObject ? _gameObjectRect : _overlayRect;

  void UpdateTitle() {
    var runnername = _runner ? _runner.name : "Disconnected";
    if (_titleText) {
      _titleText.text = runnername;
    }
  }

  Shader Shader {
    get => Resources.Load<Shader>("FusionGraphShader");
  }

  Font _font;
  bool _hidden;
  bool _paused;
  int _layoutDirty;
  bool _activeDirty;

  double _currentDrawTime;
  double _delayDrawUntil;

  void DirtyLayout(int minimumRefreshes = 1) {
    if (_layoutDirty < minimumRefreshes) {
      _layoutDirty = minimumRefreshes;
    }
  }

#if UNITY_EDITOR
  void OnValidate() {

    if (EnforceSingle && Guid == "") {
      Guid = System.Guid.NewGuid().ToString().Substring(0, 13);
    }
    _activeDirty = true;
    if (_layoutDirty <= 0) {
      _layoutDirty = 2;

      // Some aspects of Layout will throw warnings if run from OnValidate, so defer.
      // Stop deferring when entering play mode, as this will cause null errors (thanks unity).
      if (Application.isPlaying) {
        UnityEditor.EditorApplication.delayCall += CalculateLayout;
      } else {
        UnityEditor.EditorApplication.delayCall -= CalculateLayout;
      }
    }
  }

  void Reset() {
    ResetInternal();
  }

#endif

  void ResetInternal(
    bool? enableObjectStats          = null, 
    Stats.NetStatFlags? netStatsMask = null, 
    Stats.SimStatFlags? simStatsMask = null,
    DefaultLayouts? objectLayout     = null,
    DefaultLayouts? screenLayout     = null
    ) {
    // Destroy existing built graphs
    var canv = GetComponentInChildren<Canvas>();
    if (canv) {
      DestroyImmediate(canv.gameObject);
    }

    if (TryGetComponent<FusionStatsBillboard>(out var _) == false) {
      gameObject.AddComponent<FusionStatsBillboard>().UpdateLookAt();
    }

    bool hasNetworkObject = GetComponentInParent<NetworkObject>();
    // If attached to a NetObject
    if (enableObjectStats.GetValueOrDefault() || (enableObjectStats.GetValueOrDefault(true) && hasNetworkObject)) {
      EnableObjectStats = true;
      _includedObjStats = Stats.ObjStatFlags.Buffer;
      _includedSimStats = simStatsMask.GetValueOrDefault();
      _includedNetStats = netStatsMask.GetValueOrDefault();
      _canvasType = StatCanvasTypes.GameObject;
      EnforceSingle = false;
      GraphColumnCount = 1;
    } 
    else {
      // If not attached to a GameObject (sim only)

      GraphColumnCount = 0;

      if (transform.parent) {
        _canvasType = StatCanvasTypes.GameObject;
        EnforceSingle = false;
      } else {
        _canvasType = StatCanvasTypes.Overlay;
        EnforceSingle = true;
      }
      _includedSimStats = simStatsMask.GetValueOrDefault(DefaultSimStatsMask);
      _includedNetStats = netStatsMask.GetValueOrDefault(
        Stats.NetStatFlags.RoundTripTime | 
        Stats.NetStatFlags.SentPacketSizes | 
        Stats.NetStatFlags.ReceivedPacketSizes);

    }


    ApplyDefaultLayout(objectLayout.GetValueOrDefault(hasNetworkObject ? DefaultLayouts.UpperRight : DefaultLayouts.Full), StatCanvasTypes.GameObject);
    ApplyDefaultLayout(screenLayout.GetValueOrDefault(DefaultLayouts.Right),                                               StatCanvasTypes.Overlay);

    Guid = System.Guid.NewGuid().ToString().Substring(0, 13);
    GenerateGraphs();
  }

  void Awake() {

#if !UNITY_EDITOR
    if (_guidesRT) {
      Destroy(_guidesRT.gameObject);
    }
#endif

    if (Application.isPlaying == false) {
#if UNITY_EDITOR
      if (_canvas) {
        //// Hide canvas for rebuild, Unity makes this ugly.
        if (EditorApplication.isCompiling == false) {
          //_canvas.enabled = false;
          UnityEditor.EditorApplication.delayCall += CalculateLayout;

        }
        _layoutDirty = 2;
        //CalculateLayout();
      }
      return;
#endif

    } else {
      _foundViews = new List<IFusionStatsView>();
      GetComponentsInChildren(true, _foundViews);

    }

    if (Guid == "") {
      Guid = System.Guid.NewGuid().ToString().Substring(0, 13);
    }

    if (EnforceSingle && Guid != null) {
      if (_activeGuids.ContainsKey(Guid)) {
        Destroy(this.gameObject);
        return;
      }
      _activeGuids.Add(Guid, this);
    }

    if (EnforceSingle && transform.parent == null && _canvasType == StatCanvasTypes.Overlay) {
      DontDestroyOnLoad(gameObject);
    }
  }

  void Start() {
    if (Application.isPlaying) {
      Initialize();
      _activeDirty = true;
      _layoutDirty = 2;
      //_canvas.enabled = false;

    }
  }

  void OnDestroy() {
    // Try to unregister this Stats in case it hasn't already.
    DisassociateWithRunner(_runner);

    // If this is the current enforce single instance of this GUID, remove it from the record.
    if (Guid != null) {
      if (_activeGuids.TryGetValue(Guid, out var stats)) {
        if (stats == this) {
          _activeGuids.Remove(Guid);
        }
      }
    }
  }

  [BehaviourButtonAction("Destroy Graphs", conditionMember: nameof(_canvasRT), ConditionFlags = BehaviourActionAttribute.ActionFlags.ShowAtNotRuntime)]
  void DestroyGraphs() {
    if (_canvasRT) {
      DestroyImmediate(_canvasRT.gameObject);
    }
    _canvasRT = null;
  }

  static bool? _newInputSystemFound;
  public static bool NewInputSystemFound {
    
    get {
      if (_newInputSystemFound == null) {

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
          var asmtypes = asm.GetTypes();
          foreach (var type in asmtypes) {
            if (type.Namespace == "UnityEngine.InputSystem") {
              _newInputSystemFound = true;
              return true;
            }
          }
        }
        _newInputSystemFound = false;
        return false;
      }
      return _newInputSystemFound.Value;
    }
  }

  void Initialize() {

    // Only add an event system if no active event systems exist.
    if (Application.isPlaying) {

      if (NewInputSystemFound) {
        // New Input System
      }
      else {
        if (FindObjectOfType<EventSystem>() == null) {
          var eventSystemGO = new GameObject("Event System");
          eventSystemGO.AddComponent<EventSystem>();
          eventSystemGO.AddComponent<StandaloneInputModule>();
          if (Application.isPlaying) {
            DontDestroyOnLoad(eventSystemGO);
          }
        }
      }
    }


    if (_canvasRT == false) {
      GenerateGraphs();
    }

    // Already existed before runtime. (Scene object)
    if (_canvasRT) {
      // Listener connections are not retained with serialization and always need to be connected at startup.
      // Remove listeners in case this is a copy of a runtime generated graph.
      _togglButton?.onClick.RemoveListener(Toggle);
      _canvsButton?.onClick.RemoveListener(ToggleCanvasType);
      _clearButton?.onClick.RemoveListener(Clear);
      _pauseButton?.onClick.RemoveListener(Pause);
      _closeButton?.onClick.RemoveListener(Close);
      _titleButton?.onClick.RemoveListener(PingSelectFusionStats);
      _objctButton?.onClick.RemoveListener(PingSelectObject);

      _togglButton?.onClick.AddListener(Toggle);
      _canvsButton?.onClick.AddListener(ToggleCanvasType);
      _clearButton?.onClick.AddListener(Clear);
      _pauseButton?.onClick.AddListener(Pause);
      _closeButton?.onClick.AddListener(Close);
      _titleButton?.onClick.AddListener(PingSelectFusionStats);
      _objctButton?.onClick.AddListener(PingSelectObject);
      // Run Unity first frame layout failure hack.

      GetComponentsInChildren(true, _foundViews);

      foreach (var g in _foundViews) {
        g.Initialize();
      }

      _layoutDirty = 1;
    }
  }

  bool _graphsAreMissing => _canvasRT == null;
  
  [BehaviourButtonAction("Generate Graphs", conditionMember: nameof(_graphsAreMissing), ConditionFlags = BehaviourActionAttribute.ActionFlags.ShowAtNotRuntime)]
  void GenerateGraphs() {
    var rootRectTr = gameObject.GetComponent<Transform>();
    _canvasRT = rootRectTr.CreateRectTransform("Stats Canvas");
    _canvas = _canvasRT.gameObject.AddComponent<Canvas>();
    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

    // If the runner has already started, the root FusionStats has been added to the VisNodes registration for the runner,
    // But any generated children GOs here will not. Add the generated components to the visibility system.
    if (Runner && Runner.IsRunning) {
      RunnerVisibilityNode.AddVisibilityNodes(_canvasRT.gameObject, Runner);
    }
    var scaler = _canvasRT.gameObject.AddComponent<UI.CanvasScaler>();
    scaler.uiScaleMode = UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(SCREEN_SCALE_W, SCREEN_SCALE_H);
    scaler.matchWidthOrHeight = .4f;

    _canvasRT.gameObject.AddComponent<UI.GraphicRaycaster>();

#if UNITY_EDITOR
    _guidesRT = _canvasRT.MakeGuides();
#endif

    _rootPanelRT = _canvasRT
      .CreateRectTransform("Root Panel");

    _headerRT = _rootPanelRT
      .CreateRectTransform("Header Panel")
      .AddCircleSprite(PanelColor);

    _titleRT = _headerRT
      .CreateRectTransform("Runner Title")
      .SetAnchors(0.0f, 1.0f, 0.75f, 1.0f)
      .SetOffsets(MARGIN, -MARGIN, 0.0f, -MARGIN);

    _titleButton = _titleRT.gameObject.AddComponent<UI.Button>();
    _titleText   = _titleRT.AddText(_runner ? _runner.name : "Disconnected", TextAnchor.UpperCenter, _fontColor);
    _titleText.raycastTarget = true;

    // Buttons
    _buttonsRT = _headerRT
      .CreateRectTransform("Buttons")
      .SetAnchors(0.0f, 1.0f, 0.0f, 0.75f)
      .SetOffsets(MARGIN, -MARGIN, MARGIN, 0);

    var buttonsGrid = _buttonsRT.gameObject.AddComponent<UI.HorizontalLayoutGroup>();
    buttonsGrid.childControlHeight = true;
    buttonsGrid.childControlWidth = true;
    buttonsGrid.spacing = MARGIN;
    _buttonsRT.MakeButton(ref _togglButton, HIDE_ICON, HIDE_TEXT, out _togglIcon, out _togglLabel, Toggle);
    _buttonsRT.MakeButton(ref _canvsButton, CNVS_ICON, CNVS_TEXT, out _canvsIcon, out _canvsLabel, ToggleCanvasType);
    _buttonsRT.MakeButton(ref _pauseButton, PAUS_ICON, PAUS_TEXT, out _pauseIcon, out _pauseLabel, Pause);
    _buttonsRT.MakeButton(ref _clearButton, CLER_ICON, CLER_TEXT, out _clearIcon, out _clearLabel, Clear);
    _buttonsRT.MakeButton(ref _closeButton, CLSE_ICON, CLSE_TEXT, out _closeIcon, out _closeLabel, Close);

    // Minor tweak to foldout arrow icon, since its too tall.
    _togglIcon.rectTransform.anchorMax = new Vector2(1, 0.85f);

    // Stats stack

    _statsPanelRT = _rootPanelRT
      .CreateRectTransform("Stats Panel")
      .AddCircleSprite(PanelColor);

    // Object Name, IDs and Meters

    _objectTitlePanelRT = _statsPanelRT
      .CreateRectTransform("Object Name Panel")
      .ExpandTopAnchor(MARGIN)
      .AddCircleSprite(_objDataBackColor);

    _objctButton = _objectTitlePanelRT.gameObject.AddComponent<UI.Button>();

    var objectTitleRT = _objectTitlePanelRT
      .CreateRectTransform("Object Name")
      .SetAnchors(0.0f, 1.0f, 0.15f, 0.85f)
      .SetOffsets(PAD, -PAD, 0, 0);
    
    _objectNameText = objectTitleRT.AddText("Object Name", TextAnchor.MiddleCenter, _fontColor);
    _objectNameText.alignByGeometry = false;
    _objectNameText.raycastTarget = false;

    _objectIdsGroupRT = FusionStatsObjectIds.Create(_statsPanelRT, this);

    _objectMetersPanelRT = _statsPanelRT
      .CreateRectTransform("Object Meters Layout")
      .ExpandTopAnchor(MARGIN)
      .AddVerticalLayoutGroup(MARGIN);

    FusionStatsMeterBar.Create(_objectMetersPanelRT, this, Stats.StatSourceTypes.NetworkObject, (int)Stats.ObjStats.Bandwidth, 15, 30);
    FusionStatsMeterBar.Create(_objectMetersPanelRT, this, Stats.StatSourceTypes.NetworkObject, (int)Stats.ObjStats.RPC,       3,  6);

    // Graphs
    _graphsLayoutRT = _statsPanelRT
      .CreateRectTransform("Graphs Layout")
      .ExpandAnchor()
      .SetOffsets(MARGIN, 0,0,0);

    //.AddGridlLayoutGroup(MRGN);
    _graphGridLayoutGroup = _graphsLayoutRT.AddGridlLayoutGroup(MARGIN);

    _objGraphs = new FusionGraph[Stats.OBJ_STAT_TYPE_COUNT];
    for (int i = 0; i < Stats.OBJ_STAT_TYPE_COUNT; ++i) {
      if (InitializeAllGraphs == false) {
        var statFlag = (Stats.ObjStatFlags)(1 << i);
        if ((statFlag & _includedObjStats) == 0) {
          continue;
        }
      }
      CreateGraph(Stats.StatSourceTypes.NetworkObject, i, _graphsLayoutRT);
    }

    _netGraphs = new FusionGraph[Stats.NET_STAT_TYPE_COUNT];
    for (int i = 0; i < Stats.NET_STAT_TYPE_COUNT; ++i) {
      if (InitializeAllGraphs == false) {
        var statFlag = (Stats.NetStatFlags)(1 << i);
        if ((statFlag & _includedNetStats) == 0) {
          continue;
        }
      }
      CreateGraph(Stats.StatSourceTypes.NetConnection, i, _graphsLayoutRT);
    }

    _simGraphs = new FusionGraph[Stats.SIM_STAT_TYPE_COUNT];
    for (int i = 0; i < Stats.SIM_STAT_TYPE_COUNT; ++i) {
      if (InitializeAllGraphs == false) {
        var statFlag = (Stats.SimStatFlags)(1 << i);
        if ((statFlag & _includedSimStats) == 0) {
          continue;
        }
      }
      CreateGraph(Stats.StatSourceTypes.Simulation, i, _graphsLayoutRT);
    }

    // Hide canvas for a tick. Unity makes some ugliness on the first update.
    //_canvas.enabled = false;
    _activeDirty = true;

    _layoutDirty = 2;
  }

  void AssociateWithRunner(NetworkRunner runner) {
    if (runner != null) {
      if (_statsForRunnerLookup.TryGetValue(runner, out var runnerStats) == false) {
        _statsForRunnerLookup.Add(runner, new List<FusionStats>() { this });
      } else {
        runnerStats.Add(this);
      }
    }
  }

  void DisassociateWithRunner(NetworkRunner runner) {
    if (runner != null && _statsForRunnerLookup.TryGetValue(runner, out var oldrunnerstats)) {
      if (oldrunnerstats.Contains(this)) {
        oldrunnerstats.Remove(this);
      }
    }
  }

  void Pause() {
    if (_runner && _runner.Simulation != null) {
      _paused = !_paused;

      var icon = _paused ? PLAY_ICON : PAUS_ICON;
      var label = _paused ? PLAY_TEXT : PAUS_TEXT;
      _pauseIcon.text = icon;
      _pauseLabel.text = label;

      // Pause for all SimStats tied to this runner if all related FusionStats are paused.
      if (_statsForRunnerLookup.TryGetValue(_runner, out var stats)) {

        bool statsAreBeingUsed = false;
        foreach (var stat in stats) {
          if (stat._paused == false) {
            statsAreBeingUsed = true;
            break;
          }
        }
        _runner.Simulation.Stats.Pause(statsAreBeingUsed == false);
      }
    }
  }

  void Toggle() {
    _hidden = !_hidden;

    _togglIcon.text  = _hidden ? SHOW_ICON : HIDE_ICON;
    _togglLabel.text = _hidden ? SHOW_TEXT : HIDE_TEXT;

    _statsPanelRT.gameObject.SetActive(!_hidden);

    for (int i = 0; i < _simGraphs.Length; ++i) {
      var graph = _simGraphs[i];
      if (graph) {
        _simGraphs[i].gameObject.SetActive(!_hidden && (1 << i & (int)_includedSimStats) != 0);
      }
    }
    for (int i = 0; i < _objGraphs.Length; ++i) {
      var graph = _objGraphs[i];
      if (graph) {
        _objGraphs[i].gameObject.SetActive(!_hidden && (1 << i & (int)_includedObjStats) != 0);
      }
    }
    for (int i = 0; i < _netGraphs.Length; ++i) {
      var graph = _netGraphs[i];
      if (graph) {
        _netGraphs[i].gameObject.SetActive(!_hidden && (1 << i & (int)_includedNetStats) != 0);
      }
    }
  }

  void Clear() {
    if (_runner && _runner.Simulation != null) {
      _runner.Simulation.Stats.Clear();
    }

    for (int i = 0; i < _simGraphs.Length; ++i) {
      var graph = _simGraphs[i];
      if (graph) {
        _simGraphs[i].Clear();
      }
    }
    for (int i = 0; i < _objGraphs.Length; ++i) {
      var graph = _objGraphs[i];
      if (graph) {
        _objGraphs[i].Clear();
      }
    }
    for (int i = 0; i < _netGraphs.Length; ++i) {
      var graph = _netGraphs[i];
      if (graph) {
        _netGraphs[i].Clear();
      }
    }
  }

  void ToggleCanvasType() {
#if UNITY_EDITOR
    UnityEditor.EditorGUIUtility.PingObject(gameObject);
    if (Selection.activeGameObject == null) {
      Selection.activeGameObject = gameObject;
    }
#endif
    _canvasType = (_canvasType == StatCanvasTypes.GameObject) ? StatCanvasTypes.Overlay : StatCanvasTypes.GameObject;
    //_canvas.enabled = false;
    _layoutDirty = 3;
    CalculateLayout();
  }

  void Close() {
    Destroy(this.gameObject);
  }

  void PingSelectObject() {

#if UNITY_EDITOR
    var obj = Object;
    if (obj) {
      EditorGUIUtility.PingObject(Object.gameObject);
      Selection.activeGameObject = Object.gameObject;
    }
#endif
  }

  void PingSelectFusionStats() {

#if UNITY_EDITOR
      EditorGUIUtility.PingObject(gameObject);
      Selection.activeGameObject = gameObject;
#endif
  }

#if UNITY_EDITOR

  private void OnDrawGizmos() {
    AutoGuideVisibility();
  }

  void AutoGuideVisibility() {
    if (_canvasRT == null) {
      return;
    }

    if (CanvasType == StatCanvasTypes.GameObject) {
      if (_guidesRT == null) {
        _guidesRT = FusionStatsUtilities.MakeGuides(_canvasRT);
      }
      if (Selection.activeGameObject == gameObject) {
        _guidesRT.gameObject.SetActive(true);
        _guidesRT.localRotation = default;

      } else {
        _guidesRT.gameObject.SetActive(false);

      }
    } else {
      if (_guidesRT) {
        DestroyImmediate(_guidesRT.gameObject);
      }
    }
  }

#endif

  void LateUpdate() {

    // Use of the Runner getter here is intentional - this forces a test of the existing Runner having gone null or inactive.
    var runner = Runner;
    bool runnerIsNull = runner == null;

    if (AutoDestroy && runnerIsNull) {
      Destroy(this.gameObject);
      return;
    }

    if (_activeDirty) {
      ReapplyEnabled();
    }

    if (_layoutDirty > 0) {
      CalculateLayout();
    }

    if (Application.isPlaying == false) {
      return;
    }

    // NetConnection stats do not like being polled after shutdown and will throw assert fails.
    if (runnerIsNull || runner.IsShutdown) {
      return;
    }

    if (_paused) {
      return;
    }

    // Cap redraw rate - rate of 0 = disabled.
    if (RedrawInterval > 0) {
      var currentime = Time.timeAsDouble;
      if (currentime > _delayDrawUntil) {
        _currentDrawTime = currentime;
        while (_delayDrawUntil <= currentime) {
          _delayDrawUntil += RedrawInterval;
        }
      }

      if (currentime != _currentDrawTime) {
        return;
      }
    }

    if (EnableObjectStats) {
      RefreshObjectValues();
    }

    foreach (var graph in _foundViews) {
      if (graph != null && graph.isActiveAndEnabled) {
        graph.Refresh();
      }
    }
  }

  string _previousObjectTitle;

  void RefreshObjectValues() {

    var obj = Object;
    if (obj == null) {
      return;
    }

    var objectName = obj.name;
    if (_previousObjectTitle != objectName) {
      _objectNameText.text = objectName;
      _previousObjectTitle = objectName;
    }
  }

  public FusionGraph CreateGraph(Stats.StatSourceTypes type, int statId, RectTransform parentRT) {

    var fg = FusionGraph.Create(this, type, statId, parentRT);

    if (type == Stats.StatSourceTypes.Simulation) {
      _simGraphs[statId] = fg;
      if (((int)_includedSimStats & (1 << statId)) == 0) {
        fg.gameObject.SetActive(false);
      }
    } else if (type == Stats.StatSourceTypes.NetworkObject) {
      _objGraphs[statId] = fg;
      if (((int)_includedObjStats & (1 << statId)) == 0) {
        fg.gameObject.SetActive(false);
      }
    } else {
      _netGraphs[statId] = fg;
      if (((int)_includedNetStats & (1 << statId)) == 0) {
        fg.gameObject.SetActive(false);
      }
    }

    return fg;
  }

  // returns true if a graph has been added.
  void ReapplyEnabled() {

    _activeDirty = false;

    if (_simGraphs == null || _simGraphs.Length < 0) {
      return;
    }

    // This is null if the children were deleted. Stop execution, or new Graphs will be created without a parent.
    if (_graphsLayoutRT == null) {
      return;
    }

    for (int i = 0; i < _simGraphs.Length; ++i) {
      var graph = _simGraphs[i];
      bool enabled = ((Stats.SimStatFlags)(1 << i) & _includedSimStats) != 0;
      if (graph == null) {
        if (enabled) {
          graph = CreateGraph(Stats.StatSourceTypes.Simulation, i, _graphsLayoutRT);
          _simGraphs[i] = graph;
        } else {
          continue;
        }
      }
      graph.gameObject.SetActive(enabled);
    }

    for (int i = 0; i < _objGraphs.Length; ++i) {
      var graph = _objGraphs[i];
      bool enabled = _enableObjectStats && ((Stats.ObjStatFlags)(1 << i) & _includedObjStats) != 0;
      if (graph == null) {
        if (enabled) {
          graph = CreateGraph(Stats.StatSourceTypes.NetworkObject, i, _graphsLayoutRT);
          _objGraphs[i] = graph;
        } else {
          continue;
        }
      }

      if (_objGraphs[i] != null) {
        graph.gameObject.SetActive(enabled);
      }
    }

    for (int i = 0; i < _netGraphs.Length; ++i) {
      var graph = _netGraphs[i];
      bool enabled = ((Stats.NetStatFlags)(1 << i) & _includedNetStats) != 0;
      if (graph == null) {
        if (enabled) {
          graph = CreateGraph(Stats.StatSourceTypes.NetConnection, i, _graphsLayoutRT);
          _netGraphs[i] = graph;
        } else {
          continue;
        }
      }

      if (_netGraphs[i] != null) {
        graph.gameObject.SetActive(enabled);
      }
    }
  }

  float _lastLayoutUpdate;

  void CalculateLayout() {

    if (_rootPanelRT == null || _graphsLayoutRT == null) {
      return;
    }

    if (_foundGraphs == null) {
      _foundGraphs = new List<FusionGraph>(_graphsLayoutRT.GetComponentsInChildren<FusionGraph>(false));
    } else {
      GetComponentsInChildren(false, _foundGraphs);
    }

    // Don't count multiple executions of CalculateLayout in the same Update as reducing the dirty count.
    // _layoutDirty can be set to values greater than 1 to force a recalculate for several consecutive Updates.
    var time = Time.time;

    if (_lastLayoutUpdate < time) {
      _layoutDirty--;
      _lastLayoutUpdate = time;

    }

#if UNITY_EDITOR
    if (Application.isPlaying == false && _layoutDirty > 0) {
      UnityEditor.EditorApplication.delayCall -= CalculateLayout;
      UnityEditor.EditorApplication.delayCall += CalculateLayout;
    }
#endif

    if (_layoutDirty <= 0 && _canvas.enabled == false) {
      //_canvas.enabled = true;
    }

    if (_rootPanelRT) {

#if UNITY_EDITOR
      AutoGuideVisibility();
#endif

      var maxHeaderHeight = Math.Min(_maxHeaderHeight, _rootPanelRT.rect.width / 4);

      if (_canvasType == StatCanvasTypes.GameObject) {
        _canvas.renderMode = RenderMode.WorldSpace;
        var scale = CanvasScale / SCREEN_SCALE_H; //  (1f / SCREEN_SCALE_H) * Scale;
        _canvasRT.localScale = new Vector3(scale, scale, scale);
        _canvasRT.sizeDelta = new Vector2(1024, 1024);
        _canvasRT.localPosition = new Vector3(0, 0, CanvasDistance);
        
        // TODO: Cache this
        if (_canvasRT.GetComponent<FusionStatsBillboard>() == false) {
          _canvasRT.localRotation = default;
        }
      } else {
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      }

      _objectTitlePanelRT.gameObject.SetActive(_enableObjectStats);
      _objectIdsGroupRT.gameObject.SetActive(_enableObjectStats);
      _objectMetersPanelRT.gameObject.SetActive(_enableObjectStats);

      Vector2 icoMinAnchor;

      if (_showButtonLabels) {
        icoMinAnchor = new Vector2(0.0f, FusionStatsUtilities.BTTN_LBL_NORM_HGHT * .5f);
      } else {
        icoMinAnchor = new Vector2(0.0f, 0.0f);
      }

      _togglIcon.rectTransform.anchorMin = icoMinAnchor + new Vector2(0, .15f);
      _canvsIcon.rectTransform.anchorMin = icoMinAnchor;
      _clearIcon.rectTransform.anchorMin = icoMinAnchor;
      _pauseIcon.rectTransform.anchorMin = icoMinAnchor;
      _closeIcon.rectTransform.anchorMin = icoMinAnchor;

      _togglLabel.gameObject.SetActive(_showButtonLabels);
      _canvsLabel.gameObject.SetActive(_showButtonLabels);
      _clearLabel.gameObject.SetActive(_showButtonLabels);
      _pauseLabel.gameObject.SetActive(_showButtonLabels);
      _closeLabel.gameObject.SetActive(_showButtonLabels);

      var rect = CurrentRect;

      _rootPanelRT.anchorMax          = new Vector2(rect.xMax, rect.yMax);
      _rootPanelRT.anchorMin          = new Vector2(rect.xMin, rect.yMin);
      _rootPanelRT.sizeDelta          = new Vector2(0.0f, 0.0f);
      _rootPanelRT.pivot              = new Vector2(0.5f, 0.5f);
      _rootPanelRT.anchoredPosition3D = default;

      _headerRT.anchorMin             = new Vector2(0.0f, 1);
      _headerRT.anchorMax             = new Vector2(1.0f, 1);
      _headerRT.pivot                 = new Vector2(0.5f, 1);
      _headerRT.anchoredPosition3D    = default;
      _headerRT.sizeDelta             = new Vector2(0, /*TITLE_HEIGHT +*/ maxHeaderHeight);

      _objectTitlePanelRT.offsetMax   = new Vector2(-MARGIN, -MARGIN);
      _objectTitlePanelRT.offsetMin   = new Vector2( MARGIN, -(ObjectTitleHeight));
      _objectIdsGroupRT.offsetMax     = new Vector2(-MARGIN, -(ObjectTitleHeight + MARGIN));
      _objectIdsGroupRT.offsetMin     = new Vector2( MARGIN, -(ObjectTitleHeight + ObjectIdsHeight));
      _objectMetersPanelRT.offsetMax  = new Vector2(-MARGIN, -(ObjectTitleHeight + ObjectIdsHeight + MARGIN));
      _objectMetersPanelRT.offsetMin  = new Vector2( MARGIN, -(ObjectTitleHeight + ObjectIdsHeight + ObjectMetersHeight ));

      // Disable object sections that have been minimized to 0
      _objectTitlePanelRT .gameObject.SetActive(EnableObjectStats && ObjectTitleHeight  > 0);
      _objectIdsGroupRT   .gameObject.SetActive(EnableObjectStats && ObjectIdsHeight    > 0);
      _objectMetersPanelRT.gameObject.SetActive(EnableObjectStats && ObjectMetersHeight > 0);

      _statsPanelRT.ExpandAnchor().SetOffsets(0, 0, 0, -(/*TITLE_HEIGHT + */maxHeaderHeight));

      if (_enableObjectStats && _statsPanelRT.rect.height < (ObjectTitleHeight + ObjectIdsHeight + ObjectMetersHeight)) {
        _statsPanelRT.offsetMin = new Vector2(0.0f, _statsPanelRT.rect.height -(ObjectTitleHeight + ObjectIdsHeight + ObjectMetersHeight + MARGIN));
      }

      var graphColCount = GraphColumnCount > 0 ? GraphColumnCount : (int)(_graphsLayoutRT.rect.width / (_graphMaxWidth + MARGIN));
      if (graphColCount < 1) {
        graphColCount = 1;
      }

      var graphRowCount = (int)Math.Ceiling((double)_foundGraphs.Count / graphColCount);
      if (graphRowCount < 1) {
        graphRowCount = 1;
      }

      if (graphRowCount == 1) {
        graphColCount = _foundGraphs.Count;
      }

      _graphGridLayoutGroup.constraint = UI.GridLayoutGroup.Constraint.FixedColumnCount;
      _graphGridLayoutGroup.constraintCount = graphColCount;

      var cellwidth = _graphsLayoutRT.rect.width / graphColCount - MARGIN;
      var cellheight = _graphsLayoutRT.rect.height / graphRowCount - (/*(graphRowCount - 1) **/ MARGIN);
      
      _graphGridLayoutGroup.cellSize = new Vector2(cellwidth, cellheight);
      _graphsLayoutRT.offsetMax = new Vector2(0, _enableObjectStats ? -(ObjectTitleHeight + ObjectIdsHeight + ObjectMetersHeight + MARGIN) : -MARGIN);


      if (_foundViews == null) {
        _foundViews = new List<IFusionStatsView>(GetComponentsInChildren<IFusionStatsView>(false));
      } else {
        GetComponentsInChildren(false, _foundViews);
      }

      if (_objGraphs != null) {
        // enabled/disable any object graphs based on _enabledObjectStats setting
        foreach (var objGraph in _objGraphs) {
          if (objGraph) {
            objGraph.gameObject.SetActive(((int)_includedObjStats & (1 << objGraph.StatId)) != 0 && _enableObjectStats);
          }
        }
      }

      for (int i = 0; i < _foundViews.Count; ++i) {
        var graph = _foundViews[i];
        if (graph == null || graph.isActiveAndEnabled == false) {
          continue;
        }
        graph.CalculateLayout();
        graph.transform.localRotation = default;
        graph.transform.localScale = new Vector3(1, 1, 1);
      }
    }
  }

  void ApplyDefaultLayout(DefaultLayouts defaults, StatCanvasTypes? applyForCanvasType = null) {
    bool applyToGO = applyForCanvasType.HasValue == false || applyForCanvasType.Value == StatCanvasTypes.GameObject;
    bool applyToOL = applyForCanvasType.HasValue == false || applyForCanvasType.Value == StatCanvasTypes.Overlay;

    if (defaults == DefaultLayouts.Custom) {
      return;
    }

    Rect screenrect;
    Rect objectrect;
    bool isTall;
#if UNITY_EDITOR
    var currentRes = UnityEditor.Handles.GetMainGameViewSize();
    isTall = (currentRes.y > currentRes.x);
#else
    isTall = Screen.height > Screen.width;
#endif

    switch (defaults) {
      case DefaultLayouts.Left: {
          objectrect = Rect.MinMaxRect(0.0f, 0.0f, 0.3f, 1.0f);
          screenrect = objectrect;
          break;
        }
      case DefaultLayouts.Right: {
          objectrect = Rect.MinMaxRect(0.7f, 0.0f, 1.0f, 1.0f);
          screenrect = objectrect;
          break;
        }
      case DefaultLayouts.UpperLeft: {
          objectrect =          Rect.MinMaxRect(0.0f, 0.5f, 0.3f, 1.0f);
          screenrect = isTall ? Rect.MinMaxRect(0.0f, 0.7f, 0.3f, 1.0f) : objectrect ;
          break;
        }
      case DefaultLayouts.UpperRight: {
          objectrect =          Rect.MinMaxRect(0.7f, 0.5f, 1.0f, 1.0f);
          screenrect = isTall ? Rect.MinMaxRect(0.7f, 0.7f, 1.0f, 1.0f) : objectrect;
          break;
        }
      case DefaultLayouts.Full: {
          objectrect = Rect.MinMaxRect(0.0f, 0.0f, 1.0f, 1.0f);
          screenrect = objectrect;
          break;
        }
      default: {
          objectrect = Rect.MinMaxRect(0.0f, 0.5f, 0.3f, 1.0f);
          screenrect = objectrect;
          break;
        }
    }

    if (applyToGO) {
      GameObjectRect = objectrect;
    }
    if (applyToOL) {
      OverlayRect = screenrect;
    }
    
    _layoutDirty += 1;
  }
}