namespace Fusion {
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.EventSystems;
  using StatsInternal;
  using UnityEngine.Serialization;

#if UNITY_EDITOR
  using UnityEditor;
#endif

  /// <summary>
  /// Creates and controls a Canvas with one or multiple telemetry graphs. Can be created as a scene object or prefab,
  /// or be created at runtime using the <see cref="Create"/> methods. If created as the child of a <see cref="NetworkObject"/>
  /// then <see cref="EnableObjectStats"/> will automatically be set to true.
  /// </summary>
  [ScriptHelp(BackColor = ScriptHeaderBackColor.Olive)]
  [ExecuteAlways]
  public partial class FusionStats : Fusion.Behaviour {

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

    /// <summary>
    /// Interval (in seconds) between Graph redraws. Higher values (longer intervals) reduce CPU overhead, draw calls and garbage collection. 
    /// </summary>
    [InlineHelp]
    [Unit(Units.Seconds)]//, DecimalPlaces = 2)]
    [Range(0f, 1f)]
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
    public float CanvasScale = 5f;

    /// <summary>
    /// The distance on the Z axis the canvas will be positioned. Allows moving the canvas in front of or behind the parent GameObject.
    /// </summary>
    [InlineHelp]
    [DrawIf(nameof(_canvasType), (long)StatCanvasTypes.GameObject, Hide = true)]
    [Range(-10, 10f)]
    public float CanvasDistance = 0f;

    /// <summary>
    /// The Rect which defines the position of the stats canvas on a GameObject. Sizes are normalized percentages.(ranges of 0f-1f).
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_canvasType), (long)StatCanvasTypes.GameObject, Hide = true)]
    [NormalizedRect(aspectRatio: 1)]
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
    [DrawIf(nameof(_canvasType), (long)StatCanvasTypes.Overlay, Hide = true)]
    [NormalizedRect]
    Rect _overlayRect = new Rect(0.0f, 0.0f, 0.3f, 1.0f);
    public Rect OverlayRect {
      get => _overlayRect;
      set {
        _overlayRect = value;
        DirtyLayout();
      }
    }

    /// <summary>
    /// <see cref="FusionStatsGraph.Layouts"/> value which all child <see cref="FusionStatsGraph"/> components will use if their <see cref="FusionStatsGraph.Layouts"/> value is set to Auto.
    /// </summary>
    [Header("Fusion Graphs Layout")]
    [InlineHelp]
    [SerializeField]
    FusionStatsGraph.Layouts _defaultLayout;
    public FusionStatsGraph.Layouts DefaultLayout {
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
    /// Disables the bar graph in <see cref="FusionStatsGraph"/>, and uses a text only layout.
    /// Enable this if <see cref="FusionStatsGraph"/> is not rendering correctly in VR.
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
    public int GraphColumnCount = 1;

    /// <summary>
    /// If <see cref="GraphColumnCount"/> is set to zero, then columns will automatically be added as needed to limit graphs to this width or less.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(GraphColumnCount), 0)]
    [Range(30, SCREEN_SCALE_W)]
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

    [Header("Network Object Stats")] [SerializeField]
    private int _playerRef;
    public PlayerRef PlayerRef {
      get => PlayerRef.FromIndex(_playerRef);
      set {
        _playerRef = value.AsIndex;
        // TODO: Not needed?
        DirtyLayout();
      }
    }
    
    /// <summary>
    /// Enables/Disables all NetworkObject related elements.
    /// </summary>
    [Header("Network Object Stats")]
    [InlineHelp]
    [SerializeField]
    bool _enableObjectStats;
    public bool EnableObjectStats {
      get => _enableObjectStats;
      set {
        _enableObjectStats = value;
        DirtyLayout();
      }
    }

    /// <summary>
    /// The <see cref="NetworkObject"/> source for any <see cref="Stats.ObjStats"/> specific telemetry.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_enableObjectStats))]
    internal NetworkObject _object;
    
    /// <summary>
    /// Returns the set serialized <see cref="NetworkObject"/> for this stat window. If that is null, returns the static MonitoredNetworkObject,
    /// which can be set using <see cref="SetMonitoredNetworkObject"/>.
    /// </summary>
    public NetworkObject Object {
      get {
        
        if (_object) {
          return _object;
        }
        
        // no local object set - fallback to the global one.
        if (_runner == null) {
          // Will not be ble to lookup a network object without a valid runner. null for now.
          return default;
        }

        if (EnableObjectStats) {
          return _runner.FindObject(MonitoredNetworkObjectId);
        }

        return default;
      }
    }

    /// <summary>
    /// Height of Object title region at top of the stats panel.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_enableObjectStats))]
    [Range(0, 200)]
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
    [DrawIf(nameof(_enableObjectStats))]
    [Range(0, 200)]
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
    [DrawIf(nameof(_enableObjectStats))]
    [Range(0, 200)]
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
    [ReadOnly]
    NetworkRunner _runner;
    public NetworkRunner Runner {
      get {

        if (Application.isPlaying == false) {
          return null;
        }
        
        // Be sure the current runner is the correct runner.
        this.ValidateRunner(_runner);
        return _runner;
      }
    }

    public void SetRunner(NetworkRunner value) {
      if (_runner == value) {
        return;
      }
      // Keep track of which runners have active stats windows - needed so pause/unpause can affect all (since pause affects other panels)
      DisassociateWithRunner(_runner);
      _runner = value;
      AssociateWithRunner(value);
      UpdateTitle();
    }
    
    /// <summary>
    /// Editor-Only. If no <see cref="Object"/> is set, this FusionStats will attempt to connect to the NetworkRunner for the current selected GameObject.
    /// </summary>
    [InlineHelp]
    [SerializeField] 
    public bool RunnerFromSelected;

    /// <summary>
    /// Initializes a <see cref="FusionStatsGraph"/> for all available stats, even if not initially included. 
    /// If disabled, graphs added after initialization will be added to the bottom of the interface stack.
    /// </summary>
    [InlineHelp]
    public bool InitializeAllGraphs;

    /// <summary>
    /// When <see cref="_runner"/> is null and no <see cref="NetworkRunner"/> exists in the current scene, FusionStats will continuously attempt to find and connect to an active <see cref="NetworkRunner"/> which matches these indicated modes.
    /// </summary>
    [InlineHelp]
    [ExpandableEnum(ShowInlineHelp = true)]
    public SimulationModes ConnectTo = SimulationModes.Host | SimulationModes.Server | SimulationModes.Client;


    /// <summary>
    /// Selects which NetworkObject stats should be displayed.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_enableObjectStats))]
    [ExpandableEnum(ShowInlineHelp = true)]
    public FieldsMask<NetworkObjectStats> _includedObjStats = new (typeof(NetworkObjectStats).GetDefaults);

    /// <summary>
    /// Selects which NetConnection stats should be displayed.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [ExpandableEnum(ShowInlineHelp = true)]
    public FieldsMask<SimulationConnectionStats> _includedNetStats = new(typeof(SimulationConnectionStats).GetDefaults);

    /// <summary>
    /// Selects which Simulation stats should be displayed.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [ExpandableEnum(ShowInlineHelp = true)]
    public FieldsMask<SimulationStats> _includedSimStats = new(typeof(SimulationStats).GetDefaults);

    /// <summary>
    /// Automatically destroys this <see cref="FusionStats"/> GameObject if the associated runner is null or inactive.
    /// Otherwise attempts will continuously be made to find an new active runner which is running in <see cref="SimulationModes"/> specified by <see cref="ConnectTo"/>, and connect to that.
    /// </summary>
    [Header("Life-Cycle")]
    [InlineHelp]
    [SerializeField]
    public bool AutoDestroy;

    /// <summary>
    /// Only one instance with the <see cref="Guid"/> can exist if there is no associated <see cref="NetworkObject"/> <see cref="_object"/>. Will destroy any additional instances on Awake.
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
    /// The font to be used for all non-number labels.
    /// </summary>
    [Header("Customization")]
    [SerializeField]
    public Font LabelFont;
    
    /// <summary>
    /// The font to be used for all number labels.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    public Font ValueFont;

    [SerializeField][HideInInspector]
    internal Shader GraphShader;
    
    /// <summary>
    /// Shows/hides controls in the inspector for defining element colors.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    private bool _modifyColors;

    /// <summary>
    /// The color used for the telemetry graph data.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_modifyColors), Hide = true)]
    Color _graphColorGood = new Color(0.1f, 0.5f, 0.1f, 1.0f);

    /// <summary>
    /// The color used for the telemetry graph data.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_modifyColors), Hide = true)]
    Color _graphColorWarn = new Color(0.75f, 0.75f, 0.2f, 1.0f);

    /// <summary>
    /// The color used for the telemetry graph data.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_modifyColors), Hide = true)]
    Color _graphColorBad = new Color(0.9f, 0.2f, 0.2f, 1.0f);

    /// <summary>
    /// The color used for the telemetry graph data.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_modifyColors), Hide = true)]
    Color _graphColorFlag = new Color(0.8f, 0.75f, 0.0f, 1.0f);

    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_modifyColors), Hide = true)]
    Color _fontColor = new Color(1.0f, 1.0f, 1.0f, 1f);

    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_modifyColors), Hide = true)]
    Color PanelColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);

    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_modifyColors), Hide = true)]
    Color _simDataBackColor = new Color(0.1f, 0.08f, 0.08f, 1.0f);

    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_modifyColors), Hide = true)]
    Color _netDataBackColor = new Color(0.15f, 0.14f, 0.09f, 1.0f);

    [InlineHelp]
    [SerializeField]
    [DrawIf(nameof(_modifyColors), Hide = true)]
    Color _objDataBackColor = new Color(0.0f, 0.2f, 0.4f, 1.0f);

    // IFusionStats interface requirements
    public Color FontColor => _fontColor;
    public Color GraphColorGood => _graphColorGood;
    public Color GraphColorWarn => _graphColorWarn;
    public Color GraphColorBad => _graphColorBad;
    public Color GraphColorFlag => _graphColorFlag;
    public Color SimDataBackColor => _simDataBackColor;
    public Color NetDataBackColor => _netDataBackColor;
    public Color ObjDataBackColor => _objDataBackColor;

    public Rect CurrentRect => _canvasType == StatCanvasTypes.GameObject ? _gameObjectRect : _overlayRect;

    Font _font;
    bool _hidden;
    bool _paused;
    int _layoutDirty;
    bool _activeDirty;

    double _currentDrawTime;
    double _delayDrawUntil;


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

    void ResetInternal(bool? enableObjectStats = null, DefaultLayouts? objectLayout = null, DefaultLayouts? screenLayout = null) {
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
        _canvasType = StatCanvasTypes.GameObject;
        EnforceSingle = false;
        GraphColumnCount = 1;
      } else {
        // If not attached to a GameObject (sim only)

        GraphColumnCount = 0;

        if (transform.parent) {
          _canvasType = StatCanvasTypes.GameObject;
          EnforceSingle = false;
        } else {
          _canvasType = StatCanvasTypes.Overlay;
          EnforceSingle = true;
        }
      }

      ApplyDefaultLayout(objectLayout.GetValueOrDefault(hasNetworkObject ? DefaultLayouts.UpperRight : DefaultLayouts.Full), StatCanvasTypes.GameObject);
      ApplyDefaultLayout(screenLayout.GetValueOrDefault(DefaultLayouts.Right), StatCanvasTypes.Overlay);

      Guid = System.Guid.NewGuid().ToString().Substring(0, 13);
      GenerateGraphs();
    }

    void Awake() {
      if (_object == null) {
        if (TryGetComponent<NetworkObject>(out var no)) {
          _object = no;
        } else {
          _object = GetComponentInParent<NetworkObject>(true);
        }
      }

      if (Application.isPlaying == false) {
#if UNITY_EDITOR
        if (_canvas) {
          // Hide canvas for rebuild, Unity makes this ugly.
          if (EditorApplication.isCompiling == false) {
            UnityEditor.EditorApplication.delayCall += CalculateLayout;
          }
          _layoutDirty = 2;
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

      if (EnforceSingle && Object == null && _canvasType == StatCanvasTypes.Overlay) {
        DontDestroyOnLoad(gameObject);
      }
    }

    void Start() {
      if (Application.isPlaying) {
        Initialize();
        _activeDirty = true;
        _layoutDirty = 2;
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

    private bool _graphCanvasExists => _canvasRT != null;
    
    [EditorButton("Destroy Graphs", dirtyObject: true)]
    [DrawIf(nameof(_graphCanvasExists), Hide = true)]
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
        } else {
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
        
        InitializeControls();
        GetComponentsInChildren(true, _foundViews);

        foreach (var g in _foundViews) {
          g.Initialize();
        }

        _layoutDirty = 1;
      }
    }

    void AssociateWithRunner(NetworkRunner runner) {
      if (runner != null) {
        if (_statsForRunnerLookup.TryGetValue(runner, out var runnerStats) == false) {
          _statsForRunnerLookup.Add(runner, new List<FusionStats>() { this });
        } else {
          runnerStats.Add(this);
        }
        // Notify FusionGraphs that they need to reconnect to a new runner.
        if (_foundGraphs != null) {
          foreach (var graph in _foundGraphs) {
            graph.Disconnect();
          }          
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
        _objectNameText.text = "No Object";
        _previousObjectTitle = "No Object";
        return;
      }

      var objectName = obj.name;
      if (_previousObjectTitle != objectName) {
        _objectNameText.text = objectName;
        _previousObjectTitle = objectName;
      }
    }

    // returns true if a graph has been added.
    void ReapplyEnabled() {

      _activeDirty = false;

      if (_simGraphs == null || _simGraphs.Length == 0) {
        return;
      }

      // This is null if the children were deleted. Stop execution, or new Graphs will be created without a parent.
      if (_graphsLayoutRT == null) {
        return;
      }

      for (int i = 0; i < _simGraphs.Length; ++i) {
        var graph = _simGraphs[i];
        bool enabled = (((long)1 << i) & _includedSimStats.Mask) != 0;
        if (graph == null) {
          if (enabled) {
            graph = CreateGraph(StatSourceTypes.Simulation, i, _graphsLayoutRT);
            _simGraphs[i] = graph;
          } else {
            continue;
          }
        }
        graph.gameObject.SetActive(enabled);
      }

      for (int i = 0; i < _objGraphs.Length; ++i) {
        var graph = _objGraphs[i];
        bool enabled = _enableObjectStats && (((long)1 << i) & _includedObjStats.Mask) != 0;
        if (graph == null) {
          if (enabled) {
            graph = CreateGraph(StatSourceTypes.NetworkObject, i, _graphsLayoutRT);
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
        bool enabled = (((long)1 << i) & _includedNetStats.Mask) != 0;
        if (graph == null) {
          if (enabled) {
            graph = CreateGraph(StatSourceTypes.NetConnection, i, _graphsLayoutRT);
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
  }
}
