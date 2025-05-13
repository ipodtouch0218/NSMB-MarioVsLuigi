namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Collections.ObjectModel;
  using System.Linq;
  using Photon.Deterministic;
  using Quantum.Core;
  using UnityEditor;
  using UnityEditor.IMGUI.Controls;
  using UnityEngine;
  using static QuantumUnityExtensions;

  /// <summary>
  /// An editor class that renders the Quantum state inspector used to reveal internal state of Quantum entities during run-time.
  /// Open the window by using Tools > Quantum > Window > State Inspectors
  /// </summary>
  public unsafe partial class QuantumStateInspector : EditorWindow {
    private static Lazy<Skin> _skin = new Lazy<Skin>(() => new Skin());

    private QuantumSimulationObjectInspector _inspector = new QuantumSimulationObjectInspector();
    private Model _model = new Model();
    private Vector2 _inspectorScroll;
    private bool _syncWithActiveGameObject;
    private bool _showCulledEntityState = true;
    private bool _showDisabledSystems = true;
    private bool _sortSystemsAlphabetically;
    private bool _useVerifiedFrame;

    private List<ComponentTypeRef> _prohibitedComponents = new List<ComponentTypeRef>();
    private List<ComponentTypeRef> _requiredComponents = new List<ComponentTypeRef>();
    
    private TreeViewState _runnersTreeViewState        = new TreeViewState();
    private UnityInternal.SplitterState _splitterState = UnityInternal.SplitterState.FromRelative(new float[] { 0.5f, 0.5f }, new int[] { 100, 100 });
    
    [NonSerialized] private QuantumRunner _currentRunner;
    [NonSerialized] private bool _needsReload;
    [NonSerialized] private bool _needsSelectionSync;
    [NonSerialized] private string _buildEntityRunnerId;
    [NonSerialized] private List<EntityRef> _entityRefBuffer = new List<EntityRef>();
    [NonSerialized] private CommandQueue _pendingDebugCommands = new CommandQueue();
    [NonSerialized] private List<Tuple<string, EntityRef>> _pendingSelection = new List<Tuple<string, EntityRef>>();
    [NonSerialized] private RunnersTreeView _runnersTreeView;
    [NonSerialized] private SerializedProperty _componentsBeingAdded;

    private static Skin skin => _skin.Value;

    private Frame GetFrame(QuantumRunner runner) => UseVerifiedFrame(runner) ? runner.Game.Frames.Verified : runner.Game.Frames.Predicted;

    // Always use verified frame for game modes other than multiplayer
    private bool UseVerifiedFrame(QuantumRunner runner) => _useVerifiedFrame || runner.Game.Session.GameMode != DeterministicGameMode.Multiplayer;

    public static QuantumStateInspector ShowWindow(bool newWindow = false) {
      QuantumStateInspector result;
      if (newWindow) {
        result = CreateInstance<QuantumStateInspector>();
      } else {
        result = GetWindow<QuantumStateInspector>();
      }
      result.Show();
      return result;
    }

    public void SelectEntity(QuantumEntityView view) {
      UpdateState();
      SelectCorrespondingEntityNode(Enumerable.Repeat(view, 1));
    }

    #region Unity Messages

    private void OnDisable() {
      EditorApplication.hierarchyChanged -= DeferredSyncWithSelection;
      Selection.selectionChanged         -= DeferredSyncWithSelection;
      DebugCommand.CommandExecuted       -= CommandExecuted;
    }

    private void OnEnable() {
      _runnersTreeView = new RunnersTreeView(_runnersTreeViewState, _model);
      _runnersTreeView.TreeSelectionChanged += (sender, selection) => {
        _componentsBeingAdded = null;
        _buildEntityRunnerId = null;
        _runnersTreeView.SetForceShowItems(null);
        if (_syncWithActiveGameObject) {
          SelectCorrespondingGameObjects(GetEntitiesByTreeId(selection));
        }
      };

      // remove missing component types
      _requiredComponents.RemoveAll(x => QuantumEditorUtility.TryGetComponentType(x, out _) == false);
      _prohibitedComponents.RemoveAll(x => QuantumEditorUtility.TryGetComponentType(x, out _) == false);

      _runnersTreeView.WithComponents = _requiredComponents;
      _runnersTreeView.WithoutComponents = _prohibitedComponents;
      _runnersTreeView.Reload();
      
      DebugCommand.CommandExecuted       -= CommandExecuted;
      Selection.selectionChanged         -= DeferredSyncWithSelection;
      Selection.selectionChanged         += DeferredSyncWithSelection;
      EditorApplication.hierarchyChanged -= DeferredSyncWithSelection;
      EditorApplication.hierarchyChanged += DeferredSyncWithSelection;

      {
        _inspector.EntityMenu = QuantumEditorGUI.BuildMenu<EntityRef>()
          .AddItem("Select View", entity => SelectCorrespondingGameObjects(QTuple.Create(_currentRunner, entity)), _ => _currentRunner)
          .AddItem("Destroy Entity", entity => CommandEnqueue(_currentRunner, DebugCommand.CreateDestroyPayload(entity)), _ => _currentRunner && DebugCommand.IsEnabled);

        _inspector.ComponentMenu = QuantumEditorGUI.BuildMenu<EntityRef, string>()
          .AddItem("Remove Component", (entity, typeName) => CommandEnqueue(_currentRunner, DebugCommand.CreateRemoveComponentPayload(entity, QuantumEditorUtility.GetComponentType(typeName))), (_, __) => _currentRunner && DebugCommand.IsEnabled);
      }

      {
        Func<RunnersTreeViewItemBase, bool> runnerExists = (n) => FindRunnerById(n.RunnerModel.Id);
        Func<RunnersTreeViewItemBase, bool> runnerExistsDebug = (n) => DebugCommand.IsEnabled && FindRunnerById(n.RunnerModel.Id);

        _runnersTreeView.RunnerContextMenu = QuantumEditorGUI.BuildMenu<RunnerTreeViewItem>()
          .AddItem("Create Entity...", (node) => _buildEntityRunnerId = node.State.Id, runnerExistsDebug)
          .AddItem("Save Dump (Small)", (node) => DumpFrame(FindRunnerById(node.State.Id), true), runnerExists)
          .AddItem("Save Dump (Full)", (node) => DumpFrame(FindRunnerById(node.State.Id), false), runnerExists)
          .AddItem("Remove", (node) => {
            var state = node.State;
            _model.Runners.RemoveAll(x => x == state);
            _needsReload = true;
          })
          .AddItem("Remove Other Runners", (node) => {
            var state = node.State;
            _model.Runners.RemoveAll(x => x != state);
            _needsReload = true;
          }, _ => _model.Runners.Count > 1);

        _runnersTreeView.EntityContextMenu = QuantumEditorGUI.BuildMenu<EntityTreeViewItem>()
          .AddItem("Select View", _ => {
            SelectCorrespondingGameObjects(GetSelectedEntities().ToArray());
          }, runnerExists)
          .AddItem("Destroy Entity", _ => {
            foreach (var group in GetSelectedEntities().GroupBy(x => x.Item0)) {
              var payload = group.Select(x => DebugCommand.CreateDestroyPayload(x.Item1));
              CommandEnqueue(group.Key, payload.ToArray());
            }
          }, runnerExistsDebug)
          .AddGenerator(adder => {
            var commonSet = new ComponentSet();
            var selectedEntities = GetEntityNodes(_runnersTreeView.GetSelection());
            foreach (var node in selectedEntities) {
              commonSet.UnionWith(node.State.EntityComponents);
            }

            foreach (var typeName in _model.GetComponentNames(commonSet)) {
              adder($"Remove Component/{typeName}", (_) => {
                foreach (var group in GetSelectedEntities().GroupBy(x => x.Item0)) {
                  var payload = group.Select(x => DebugCommand.CreateRemoveComponentPayload(x.Item1, QuantumEditorUtility.GetComponentType(typeName)));
                  CommandEnqueue(group.Key, payload.ToArray());
                }
              }, runnerExistsDebug);
            }
          });

        _runnersTreeView.SystemContextMenu = QuantumEditorGUI.BuildMenu<SystemTreeViewItem>()
          .AddItem("Toggle System", _ => {
            foreach (var group in GetSelectedSystems().GroupBy(x => x.Item0)) {
              var payload = group.Select(x => DebugCommand.CreateToggleSystemPayload(x.Item2));
              CommandEnqueue(group.Key, payload.ToArray());
            }
          }, runnerExistsDebug)
          .AddItem("Toggle System Type", _ => {
            foreach (var group in GetSelectedSystems().GroupBy(x => x.Item0)) {
              var payload = group.Select(x => DebugCommand.CreateToggleSystemPayload(x.Item1));
              CommandEnqueue(group.Key, payload.ToArray());
            }
          }, runnerExistsDebug);
      }
    }

    private void OnGUI() {
      //var rect = new Rect(0, 0, position.width, position.height);

      titleContent = skin.titleContent;

      var toolbarRect = position.ZeroXY().SetHeight(skin.toolbarHeight);
      using (new GUILayout.AreaScope(toolbarRect)) {
        using (new GUILayout.HorizontalScope(EditorStyles.toolbar)) {
          _useVerifiedFrame = EditorGUILayout.Popup(_useVerifiedFrame ? 0 : 1, skin.frameTypeOptions, EditorStyles.toolbarPopup, GUILayout.Width(80)) != 0;
          if (GUILayout.Button("Clear", EditorStyles.toolbarButton)) {
            _model.Runners.Clear();
            _pendingDebugCommands.Clear();
            _needsReload = true;
          }
          DrawEntitiesDropdown();
          DrawSystemDropdown(); DrawComponentDropdown(_requiredComponents, "Entities With");
          DrawComponentDropdown(_prohibitedComponents, "Entities Without");
          GUILayout.FlexibleSpace();
        }
      }

      // alloc space for inspector and the tree
      try {
        UnityInternal.SplitterGUILayout.BeginHorizontalSplit(_splitterState, GUIStyle.none);
        using (new EditorGUILayout.VerticalScope()) {
          GUILayout.FlexibleSpace();
        }
        using (new EditorGUILayout.VerticalScope()) {
          GUILayout.FlexibleSpace();
        }
      } finally {
        UnityInternal.SplitterGUILayout.EndHorizontalSplit();
      }

      _runnersTreeView.WithComponents = _requiredComponents;
      _runnersTreeView.WithoutComponents = _prohibitedComponents;
      
      if (_needsReload) {
        _runnersTreeView.Reload();
        _needsReload = false;
      }

      var treeViewRect = new Rect(0, skin.toolbarHeight, _splitterState.realSizes[0], position.height - skin.toolbarHeight);
      _runnersTreeView.OnGUI(treeViewRect);

      var inspectorRect = new Rect(treeViewRect.xMax, skin.toolbarHeight, _splitterState.realSizes[1], treeViewRect.height);

      QuantumSimulationObjectInspectorState currentState = null;

      var firstSelectedNode = _runnersTreeView.GetSelection()
        .Select(x => _runnersTreeView.FindById(x))
        .FirstOrDefault(x => x?.GetInspectorState() != null);

      if (firstSelectedNode != null) {
        currentState = firstSelectedNode.GetInspectorState();
      }

      _currentRunner = null;
      using (new GUILayout.AreaScope(inspectorRect)) {
        // we want to get rid of the top border here, hence the weird -1 space;
        // seems to do the trick
        GUILayout.Space(-1);
        using (new GUILayout.HorizontalScope(skin.inspectorBackground))
        using (var scroll = new EditorGUILayout.ScrollViewScope(_inspectorScroll)) {
          _inspectorScroll = scroll.scrollPosition;
          if (!string.IsNullOrEmpty(_buildEntityRunnerId)) {
            DrawBuildEntityGUI();
          } else if (currentState == null) {
            if (_runnersTreeView.GetSelection().Any()) {
              using (EditorStyles.wordWrappedLabel.FontStyleScope(italic: true)) {
                EditorGUILayout.LabelField("Not available. An active runner is required.", EditorStyles.wordWrappedLabel);
              }
            }
          } else {
            _currentRunner = FindRunnerById(firstSelectedNode.RunnerModel.Id);
            _inspector.DoGUILayout(currentState, useScrollView: false);

            if (_currentRunner) {
              if (firstSelectedNode is EntityTreeViewItem entityNode) {
                EditorGUILayout.Space();
                EditorGUILayout.Separator();
                DrawAddComponentGUI(_currentRunner, entityNode.Entity);
              }
            }
          }
        }
      }
    }

    private void Update() {
      UpdateState();

      if (_syncWithActiveGameObject && _needsSelectionSync) {
        _needsSelectionSync = false;

        var selectedValidViews = Selection.gameObjects
          .Where(x => x.scene.IsValid())
          .Select(x => x.GetComponent<QuantumEntityView>())
          .Where(x => x?.EntityRef.IsValid == true)
          .ToList();

        if (selectedValidViews.Any()) {
          SelectCorrespondingEntityNode(selectedValidViews);
        }
      }

      if (_pendingSelection.Any()) {
        try {
          List<int> selectionBuffer = new List<int>();
          foreach (var p in _pendingSelection) {
            AppendSelectionBuffer(p.Item1, p.Item2, selectionBuffer);
          }
          if (selectionBuffer.Any()) {
            ApplySelection(selectionBuffer);
          }
        } finally {
          _pendingSelection.Clear();
        }
      }
    }

    private void OnInspectorUpdate() {
      if (_needsReload) {
        Repaint();
      }
    }

    #endregion Unity Messages

    private static QuantumRunner FindRunnerById(string id) {
      return QuantumRunner.FindRunner(id);
    }

    private static long GenerateCommandId() => AssetGuid.NewGuid().Value;

    [MenuItem("Window/Quantum/State Inspector")]
    [MenuItem("Tools/Quantum/Window/State Inspector %T", false, (int)QuantumEditorMenuPriority.Window + 14)]
    private static void ShowWindowMenuItem() => ShowWindow(false);

    private bool AppendSelectionBuffer(string runnerId, EntityRef entityRef, List<int> selectionBuffer) {
      var runnerModel = _model.FindRunner(runnerId);
      if (runnerModel == null) {
        return false;
      }

      foreach (var entity in runnerModel.Entities) {

        if (entity.Entity != entityRef) {
          continue;
        }

        // get the tree item id
        _model.GetFixedTreeNodeIds(runnerId, out var runnerNodeId, out _, out var entitiesNodeId, out _, out _, out _);

        // because of how tree view works, nodes need to be expanded, otherwise children do not exist :(
        if (_runnersTreeView.SetExpanded(runnerNodeId, true)) {
          _runnersTreeView.Reload();
        }
        if (_runnersTreeView.SetExpanded(entitiesNodeId, true)) {
          _runnersTreeView.Reload();
        }

        var entityId = _model.GetTreeNodeIdForEntity(entitiesNodeId, entityRef);
        selectionBuffer.Add(entityId);
        return true;
      }

      return false;
    }

    private void ApplySelection(List<int> selection) {
      if (selection.Any()) {
        try {
          _runnersTreeView.SetForceShowItems(null);
          _runnersTreeView.SetSelection(selection, TreeViewSelectionOptions.RevealAndFrame);
        } catch (ArgumentException) {
          // this error may come from the fact that some entities are hidden due to component filters
          _runnersTreeView.SetForceShowItems(selection);
          _runnersTreeView.Reload();
          _runnersTreeView.SetSelection(selection, TreeViewSelectionOptions.RevealAndFrame);
        }
      }
    }

    private void CommandEnqueue(QuantumRunner runner, params DebugCommand.Payload[] payload) {
      Debug.Assert(DebugCommand.IsEnabled);
      for (int i = 0; i < payload.Length; ++i) {
        payload[i].Id = GenerateCommandId();
        _pendingDebugCommands.Add(Tuple.Create(runner.Id, payload[i]));
      }

      if (!DebugCommand.Send(runner.Game, payload)) {
        for (int i = 0; i < payload.Length; ++i) {
          _pendingDebugCommands.RemoveReturn(payload[i].Id);
        }
      }
    }

    private void CommandExecuted(DebugCommand.Payload payload, Exception error) {
      var cmd = _pendingDebugCommands.RemoveReturn(payload.Id);
      if (cmd != null && payload.Type == DebugCommandType.Create && payload.Entity.IsValid && !cmd.Item2.Entity.IsValid) {
        // make sure the created entity is selected
        _pendingSelection.Add(Tuple.Create(cmd.Item1, payload.Entity));
      }
    }

    private void CommandPending(RunnerState runnerState, DebugCommand.Payload payload) {
      if (payload.Type == DebugCommandType.Create) {
        Func<EntityState, EntityState> processEntity = e => {
          if (e.InspectorId > 0) {
            var inspectorState = runnerState.GetInspector(e.InspectorId);
            foreach (var name in _model.GetComponentNames(payload.Components)) {
              inspectorState.AddComponentPlaceholder(name);
            }
          }
          var components = e.EntityComponents;
          components.UnionWith(payload.Components);
          e.EntityComponents = components;
          return e;
        };

        if (!payload.Entity.IsValid) {
          runnerState.AddPendingEntity(processEntity);
        } else {
          runnerState.UpdateEntityState(payload.Entity, processEntity);
        }
      } else if (payload.Type == DebugCommandType.Destroy) {
        if (payload.Components.IsEmpty) {
          // destroy entity
          runnerState.RemoveEntityState(payload.Entity);
        } else {
          runnerState.UpdateEntityState(payload.Entity, e => {
            if (e.InspectorId > 0) {
              var inspectorState = runnerState.GetInspector(e.InspectorId);
              foreach (var name in _model.GetComponentNames(payload.Components)) {
                inspectorState.Remove($"/{name}");
              }
            }
            // remove thumbnail
            var components = e.EntityComponents;
            components.Remove(payload.Components);
            e.EntityComponents = components;
            return e;
          });
        }
      }
    }

    private void DeferredSyncWithSelection() {
      _needsSelectionSync = true;
    }

    private void DrawAddComponentGUI(QuantumRunner runner, EntityRef entity) {
      if (_componentsBeingAdded?.serializedObject.targetObject == null) {
        _componentsBeingAdded = null;
      }
      if (_componentsBeingAdded == null) {
        using (new GUILayout.HorizontalScope()) {
          GUILayout.FlexibleSpace();
          if (EditorGUILayout.DropdownButton(skin.addComponentContent, FocusType.Passive, UnityInternal.Styles.AddComponentButton)) {
            _componentsBeingAdded = QuantumEditorUtility.GetPendingEntityPrototypeRoot(clear: true);
          }
          GUILayout.FlexibleSpace();
        }
      } else {
        using (new QuantumEditorGUI.HierarchyModeScope(true))
        using (new QuantumEditorGUI.BoxScope(null)) {
          EditorGUILayout.PropertyField(_componentsBeingAdded);
          using (new GUILayout.HorizontalScope()) {
            using (new EditorGUI.DisabledScope(runner == null)) {
              if (GUILayout.Button("OK")) {
                CommandEnqueue(runner, DebugCommand.CreateMaterializePayload(entity, QuantumEditorUtility.FinishPendingEntityPrototype(), runner.Game.AssetSerializer));
                _componentsBeingAdded = null;
              }
            }
            if (GUILayout.Button("Cancel")) {
              _componentsBeingAdded = null;
            }
          }
        }
      }
    }

    private void DrawBuildEntityGUI() {
      var runner = FindRunnerById(_buildEntityRunnerId);
      if (runner == null) {
        _buildEntityRunnerId = null;
        Repaint();
      } else {
        using (new QuantumEditorGUI.HierarchyModeScope(true))
        using (new QuantumEditorGUI.BoxScope(null)) {
          QuantumEditorGUI.Inspector(QuantumEditorUtility.GetPendingEntityPrototypeRoot(), skipRoot: true);
          using (new GUILayout.HorizontalScope()) {
            using (new EditorGUI.DisabledScope(runner == null)) {
              if (GUILayout.Button("OK")) {
                CommandEnqueue(runner, DebugCommand.CreateMaterializePayload(EntityRef.None, QuantumEditorUtility.FinishPendingEntityPrototype(), runner.Game.AssetSerializer));
                _buildEntityRunnerId = null;
              }
            }
            if (GUILayout.Button("Cancel")) {
              _buildEntityRunnerId = null;
            }
          }
        }
      }
    }

    private void DrawEntitiesDropdown() {
      var dropdownRect = EditorGUILayout.GetControlRect(false, 16, EditorStyles.toolbarPopup, GUILayout.MaxWidth(110));
      if (EditorGUI.DropdownButton(dropdownRect, new GUIContent("Entities Options"), FocusType.Keyboard, EditorStyles.toolbarPopup)) {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Sync Selection"), _syncWithActiveGameObject, () => { _syncWithActiveGameObject = !_syncWithActiveGameObject; });
        menu.AddItem(new GUIContent("Show Culled State", "Show prediction culling state in entity hierarchy"), _showCulledEntityState, () => { _showCulledEntityState = !_showCulledEntityState; });
        menu.DropDown(dropdownRect);
      }
    }

    private void DrawSystemDropdown() {
      var dropdownRect = EditorGUILayout.GetControlRect(false, 16, EditorStyles.toolbarPopup, GUILayout.MaxWidth(110));
      if (EditorGUI.DropdownButton(dropdownRect, new GUIContent("System Options"), FocusType.Keyboard, EditorStyles.toolbarPopup)) {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Show Disabled"), _showDisabledSystems, () => { _showDisabledSystems = !_showDisabledSystems; });
        menu.AddItem(new GUIContent("Sort Alphabetically"), _sortSystemsAlphabetically, () => { _sortSystemsAlphabetically = !_sortSystemsAlphabetically; });
        menu.DropDown(dropdownRect);
      }
    }

    private void DrawComponentDropdown(List<ComponentTypeRef> selector, string prefix) {
      GUIContent guiContent;
      float additionalWidth = 0;

      if (selector.Count == 0) {
        guiContent = new GUIContent($"{prefix}: None");
      } else {
        guiContent = new GUIContent($"{prefix}: ");
        additionalWidth = QuantumEditorGUI.CalcThumbnailsWidth(selector.Count);
      }

      var buttonSize = EditorStyles.toolbarDropDown.CalcSize(guiContent);
      var originalButtonSize = buttonSize;
      buttonSize.x += additionalWidth;
      buttonSize.x = Math.Max(50, buttonSize.x);

      var buttonRect = GUILayoutUtility.GetRect(buttonSize.x, buttonSize.y);
      bool pressed = GUI.Button(buttonRect, guiContent, EditorStyles.toolbarDropDown);

      var thumbnailRect = buttonRect.AddX(originalButtonSize.x - skin.toolbarDropdownButtonWidth);
      foreach (var typeRef in selector) {
        if (!QuantumEditorUtility.TryGetComponentType(typeRef, out var type)) {
          continue;
        }
        thumbnailRect = QuantumEditorGUI.ComponentThumbnailPrefix(thumbnailRect, type.Name, addSpacing: true);
      }

      if (pressed) {
        QuantumEditorGUI.ShowComponentTypesPicker(buttonRect, 
          t => selector.Any(x => x.Is(t)),
          (t, selected) => {
            if (selected) {
              selector.Add(ComponentTypeRef.FromType(t));
            } else {
              selector.RemoveAll(x => x.Is(t));
            }
            _needsReload = true;
            Repaint();
          });
      }
    }

    private void DumpFrame(QuantumRunner runner, bool partial = false) {
      var frame = GetFrame(runner);
      var path = EditorUtility.SaveFilePanel("Save Frame Dump", string.Empty, $"frame_{frame.Number}.txt", "txt");
      if (!string.IsNullOrEmpty(path)) {
        System.IO.File.WriteAllText(path, frame.DumpFrame((partial ? Frame.DumpFlag_NoHeap : 0)));
      }
    }

    private QTuple<QuantumRunner, EntityRef>[] GetEntitiesByTreeId(IList<int> ids) {
      return GetEntityNodes(ids)
        .Select(n => QTuple.Create(FindRunnerById(n.RunnerModel.Id), n.Entity))
        .Where(n => n.Item0 != null)
        .ToArray();
    }

    private IEnumerable<EntityTreeViewItem> GetEntityNodes(IList<int> ids) {
      return ids
        .Select(id => _runnersTreeView.FindById(id))
        .OfType<EntityTreeViewItem>();
    }

    private QTuple<QuantumRunner, EntityRef>[] GetSelectedEntities() {
      return GetEntitiesByTreeId(_runnersTreeView.GetSelection());
    }

    private QTuple<QuantumRunner, Type, int>[] GetSystemsByTreeId(IList<int> ids) {

      return GetSystemNodes(ids)
        .Select(n => QTuple.Create(FindRunnerById(n.RunnerModel.Id), n.Type.Value, n.RuntimeIndex))
        .Where(n => n.Item0 != null)
        .ToArray();
    }

    private IEnumerable<SystemTreeViewItem> GetSystemNodes(IList<int> ids) {
      var foo = ids
        .Select(id => _runnersTreeView.FindById(id))
        .OfType<SystemTreeViewItem>();

      return ids
        .Select(id => _runnersTreeView.FindById(id))
        .OfType<SystemTreeViewItem>();
    }

    private QTuple<QuantumRunner, Type, int>[] GetSelectedSystems() {
      var foo = _runnersTreeView.GetSelection();


      return GetSystemsByTreeId(_runnersTreeView.GetSelection());
    }

    private void SelectCorrespondingEntityNode(IEnumerable<QuantumEntityView> views) {
      var newSelection = new List<int>();

      var updaters = FindObjectsByType<QuantumEntityViewUpdater>(FindObjectsSortMode.None)
        .Select(x => new { Updater = x, RunnerId = QuantumRunner.FindRunner(x.ObservedGame)?.Id })
        .Where(x => !string.IsNullOrEmpty(x.RunnerId))
        .ToList();

      foreach (var view in views) {
        if (view?.EntityRef.IsValid != true) {
          continue;
        }

        var entityRef = view.EntityRef;

        // which updater does this belong to?
        foreach (var updater in updaters) {
          if (updater.Updater.GetView(entityRef) != view) {
            continue;
          }

          AppendSelectionBuffer(updater.RunnerId, entityRef, newSelection);
        }
      }

      ApplySelection(newSelection);
    }

    private void SelectCorrespondingGameObjects(params QTuple<QuantumRunner, EntityRef>[] entities) {
      var updaters = FindObjectsByType<QuantumEntityViewUpdater>(FindObjectsSortMode.None);

      var newSelection = new List<UnityEngine.Object>();

      foreach (var item in entities) {
        var entityRef = item.Item1;
        Debug.Assert(entityRef.IsValid);

        foreach (var updater in updaters) {
          var runner = QuantumRunner.FindRunner(updater.ObservedGame);
          if (runner != item.Item0) {
            continue;
          }

          var view = updater.GetView(entityRef);
          if (!view) {
            continue;
          }

          newSelection.Add(view.gameObject);
        }
      }

      if (newSelection.Any()) {
        Selection.objects = newSelection.ToArray();
      }
    }

    private bool UpdateRunnersState(Model model, List<int> selectedNodes) {
      bool needsReload = false;

      if (ComponentTypeId.Type?.Length > 0) {
        Array.Resize(ref model.ComponentShortNames, ComponentTypeId.Type.Length);
        for (int i = 0; i < ComponentTypeId.Type.Length; ++i) {
          model.ComponentShortNames[i] = ComponentTypeId.Type[i]?.Name;
        }
      }

      foreach (var runnerState in model.Runners) {
        runnerState.IsActive = false;
        runnerState.IsDefault = false;

        // remove all pending entities
        runnerState.RemovePendingEntities();
      }

      // match actual runners against models
      foreach (var runner in QuantumRunner.ActiveRunners) {
        var frame = GetFrame(runner);
        if (frame == null) {
          continue;
        }

        var runnerState = model.Runners.FirstOrDefault(x => x.Id == runner.Id);
        if (runnerState == null) {
          runnerState = new RunnerState() {
            Id = runner.Id
          };
          model.Runners.Add(runnerState);
          needsReload = true;
        } else if ( runnerState.Tick != frame.Number ) {
          needsReload = true;
        }

        runnerState.Tick = frame.Number;
        runnerState.Entities.Clear();
        runnerState.DynamicDB.Clear();
        runnerState.Systems.Clear();
        runnerState.Players.Clear();
        runnerState.IsActive = true;
        runnerState.IsDefault = (runner == QuantumRunner.Default);
        runnerState.UsePredictionCulling = frame.Context.Culling != null;
        runnerState.ShowCulledEntityState = _showCulledEntityState;
        runnerState.ShowDisabledSystems = _showDisabledSystems;
        runnerState.SortSystemsAlphabetically = _sortSystemsAlphabetically;
        runnerState.UseVerifiedFrame = UseVerifiedFrame(runner);

        // always inspect runner and globals
        runnerState.RunnerInspectorState.FromRunner(runner, runner.Session, frame);
        runnerState.GlobalsInspectorState.FromStruct(frame, "Globals", frame.Global, typeof(_globals_));

        int dynamicInspectorState = 0;
        model.GetFixedTreeNodeIds(runner.Id, out _, out _, out var entitiesNodeId, out var dynamicDBNodeId, out var playersNodeId, out var systemsNodeId);

        frame.GetAllEntityRefs(_entityRefBuffer);
        foreach (var entity in _entityRefBuffer) {
          var entityState = EntityState.FromEntity(frame, entity);
          var entityNodeId = model.GetTreeNodeIdForEntity(entitiesNodeId, entityState);
          if (selectedNodes.Contains(entityNodeId)) {
            runnerState.AcquireInspectorState(ref dynamicInspectorState, out entityState.InspectorId).FromEntity(frame, entity);
          }
          runnerState.Entities.Add(entityState);
        }

        var dynamicDB = frame.DynamicAssetDB;
        foreach (var asset in dynamicDB.Assets) {
          var assetState = new DynamicAssetState() {
            Guid = asset.Guid,
            Path = asset.Path,
            Type = asset.GetType().AssemblyQualifiedName,
          };
          var assetNodeId = model.GetTreeNodeIdForDynamicAsset(dynamicDBNodeId, asset.Guid);
          if (selectedNodes.Contains(assetNodeId)) {
            runnerState.AcquireInspectorState(ref dynamicInspectorState, out assetState.InspectorId).FromAsset(asset);
          }

          runnerState.DynamicDB.Add(assetState);
        }

        for (int i = 0; i < frame.MaxPlayerCount; i++) {
          var playerState = new PlayerState { Player = i, InputFlags = frame.GetPlayerInputFlags(i) };

          var playerNodeId = model.GetTreeNodeForPlayer(playersNodeId, i);
          if (selectedNodes.Contains(playerNodeId)) {
            runnerState.AcquireInspectorState(ref dynamicInspectorState, out playerState.InspectorId).FromPlayer(i, runner.Session, frame);
          }

          runnerState.Players.Add(playerState);
        }

        var systems = new List<SystemBase>(frame.SystemsRoot);
        if (runnerState.SortSystemsAlphabetically) {
          systems.Sort((a, b) => string.Compare(a.GetType().Name, b.GetType().Name, StringComparison.Ordinal));
        }
        foreach (var system in systems) {
          SystemState.Create(system, frame, ref runnerState.Systems, runnerState.ShowDisabledSystems, 0, (system) => {
            var systemNodeId = model.GetTreeNodeForSystem(systemsNodeId, system.RuntimeIndex);
            if (selectedNodes.Contains(systemNodeId)) {
              runnerState.AcquireInspectorState(ref dynamicInspectorState, out var inspectorId).FromSystem(system, frame);
              return inspectorId;
            }
            return 0;
          });
        }
      }
      
      foreach (var pair in _pendingDebugCommands) {
        var runner = _model.FindRunner(pair.Item1);
        if (runner == null) {
          continue;
        }
        CommandPending(runner, pair.Item2);
      }

      return needsReload;
    }

    private void UpdateState() {
      _needsReload |= UpdateRunnersState(_model, _runnersTreeViewState.selectedIDs);
    }

    #region Model

    [Serializable]
    private struct DynamicAssetState {
      public AssetGuid Guid;
      public int InspectorId;
      public string Path;
      public string Type;
    }

    [Serializable]
    private unsafe struct SystemState {
      public int InspectorId; 
      public int RuntimeIndex;
      [SerializableType(WarnIfNoPreserveAttribute = true)]
      public SerializableType<SystemBase> Type;
      public bool IsEnabled;
      public int Depth;

      /// <summary>
      /// Recursion but without hierarchical child list, which causes serialization depth issues with Unity.
      /// </summary>
      public static void Create(SystemBase system, Frame frame, ref List<SystemState> result, bool showDisabledSystems = true, int depth = 0, Func<SystemBase, int> createInspectorIdCallback = null) {
        var isEnabled = frame.SystemIsEnabledInHierarchy(system);
        if (showDisabledSystems == false && isEnabled == false) {
          return;
        }

        var state = new SystemState {
          RuntimeIndex = system.RuntimeIndex,
          Type = system.GetType(),
          IsEnabled = isEnabled,
          Depth = depth,
          InspectorId = createInspectorIdCallback?.Invoke(system) ?? 0
        };

        result.Add(state);

        foreach (var child in system.ChildSystems) {
          Create(child, frame, ref result, showDisabledSystems, depth + 1, createInspectorIdCallback);
        }
      }
    }

    [Serializable]
    private unsafe struct PlayerState {
      public int InspectorId;
      public PlayerRef Player;
      public DeterministicInputFlags InputFlags;
    }

    [Serializable]
    private unsafe struct EntityState {
      public ComponentSet EntityComponents;
      public int EntityRefIndex;
      public int EntityRefVersion;
      public int InspectorId;
      public bool? IsCulled;

      public EntityRef Entity => new EntityRef() { Index = EntityRefIndex, Version = EntityRefVersion };

      public bool IsPending => EntityRefIndex < 0;

      public static EntityState FromEntity(FrameBase frame, EntityRef entity) {
        ComponentSet componentSet = frame.GetComponentSet(entity);
        long* lp = (long*)&componentSet;
        bool? isCulled = frame.IsCullable(entity) ? frame.IsCulled(entity) : null;
        return new EntityState() {
          EntityRefIndex = entity.Index,
          EntityRefVersion = entity.Version,
          EntityComponents = componentSet,
          IsCulled = isCulled
        };
      }
    }
    
    [Serializable]
    private class Model {
      public string[] ComponentShortNames = { };
      public List<RunnerState> Runners = new List<RunnerState>();

      private const int TotalRunnerIdCount   = ReservedIdCount + EntitiesIdCount + DynamicAssetIdCount + SystemsIdCount;
      private const int ReservedIdCount      = 10;
      private const int EntitiesIdCount      = 9000000;
      private const int DynamicAssetIdCount  = 10000;
      private const int SystemsIdCount       = 89500;
      private const int PlayersCount         = 500;

      public RunnerState FindRunner(string id) {
        return Runners.FirstOrDefault(x => x.Id == id);
      }

      public string GetComponentName(int typeId) {
        return ComponentShortNames[typeId];
      }

      public IEnumerable<string> GetComponentNames() {
        return ComponentShortNames.Skip(1);
      }

      public IEnumerable<string> GetComponentNames(ComponentSet set) {
        if (ComponentShortNames?.Length <= 0) {
          yield break;
        }

        for (int i = 1; i < ComponentShortNames.Length; ++i) {
          if (!set.IsSet(i))
            continue;

          yield return ComponentShortNames[i];
        }
      }

      public IEnumerable<int> GetComponentTypeIds() {
        if (ComponentShortNames?.Length > 0) {
          return Enumerable.Range(1, ComponentShortNames.Length - 1);
        } else {
          return Array.Empty<int>();
        }
      }

      public void GetFixedTreeNodeIds(string runnerId, out int runnerNodeId, out int globalsNodeId, out int entitiesNodeId, out int dynamicDBNodeId, out int playersNodeId, out int systemsNodeId) {
        int index = Runners.FindIndex(x => x.Id == runnerId);
        if (index < 0) {
          throw new InvalidOperationException();
        }

        runnerNodeId    = 1 + index * TotalRunnerIdCount;
        globalsNodeId   = 1 + runnerNodeId;
        entitiesNodeId  = 1 + globalsNodeId;
        dynamicDBNodeId = 1 + entitiesNodeId + EntitiesIdCount;
        systemsNodeId   = 1 + dynamicDBNodeId + DynamicAssetIdCount;
        playersNodeId   = 1 + systemsNodeId + PlayersCount;
      }

      public int GetTreeNodeIdForDynamicAsset(int dynamicDBNodeId, AssetGuid guid) {
        Debug.Assert(guid.IsDynamic);
        var relativeId = (int)((guid.Value & ~AssetGuid.ReservedBits) % DynamicAssetIdCount);
        
        if (guid.Type == AssetGuidType.DynamicExplicit) {
          // invert it
          relativeId = DynamicAssetIdCount - relativeId - 1;
        }

        return dynamicDBNodeId + 1 + relativeId;
      }

      public int GetTreeNodeIdForEntity(int entitiesNodeId, EntityRef entity) {
        Debug.Assert(entity.Index <= EntitiesIdCount);
        return entitiesNodeId + 1 + (entity.Index % EntitiesIdCount);
      }

      public int GetTreeNodeIdForEntity(int entitiesNodeId, in EntityState entity) {
        if (entity.IsPending) {
          Debug.Assert(entity.EntityRefIndex < 0);
          return entitiesNodeId + 1 + EntitiesIdCount + entity.EntityRefIndex;
        } else {
          Debug.Assert(entity.EntityRefIndex >= 0 && entity.EntityRefIndex <= EntitiesIdCount);
          return entitiesNodeId + 1 + (entity.EntityRefIndex % EntitiesIdCount);
        }
      }

      public int GetTreeNodeForSystem(int systemsNodeId, int runtimeIndex) {
        Debug.Assert(runtimeIndex < SystemsIdCount);
        return systemsNodeId + 1 + runtimeIndex;
      }

      public int GetTreeNodeForPlayer(int playersNodeId, PlayerRef player) {
        return playersNodeId + 1 + player;
      }
    }

    [Serializable]
    private class RunnerState {

      public List<EntityState> Entities        = new List<EntityState>();
      public List<DynamicAssetState> DynamicDB = new List<DynamicAssetState>();
      public List<SystemState> Systems         = new List<SystemState>();
      public List<PlayerState> Players         = new List<PlayerState>();

      public string Id;
      public bool IsActive;
      public bool IsDefault;
      public int Tick;
      public bool ShowCulledEntityState;
      public bool UsePredictionCulling;
      public bool ShowDisabledSystems;
      public bool SortSystemsAlphabetically;
      public bool UseVerifiedFrame;

      public QuantumSimulationObjectInspectorState DynamicDBInspectorState     = new QuantumSimulationObjectInspectorState();
      public QuantumSimulationObjectInspectorState EntitiesInspectorState      = new QuantumSimulationObjectInspectorState();
      public QuantumSimulationObjectInspectorState GlobalsInspectorState       = new QuantumSimulationObjectInspectorState();
      public QuantumSimulationObjectInspectorState RunnerInspectorState        = new QuantumSimulationObjectInspectorState();
      public List<QuantumSimulationObjectInspectorState> DynamicInspectorState = new List<QuantumSimulationObjectInspectorState>();
      public QuantumSimulationObjectInspectorState SystemsInspectorState       = new QuantumSimulationObjectInspectorState();
      public QuantumSimulationObjectInspectorState PlayersInspectorState       = new QuantumSimulationObjectInspectorState();

      public QuantumSimulationObjectInspectorState AcquireInspectorState(ref int state, out int id) {
        var index = state++;
        id = state;
        if (DynamicInspectorState.Count > index) {
          return DynamicInspectorState[index];
        } else {
          var result = new QuantumSimulationObjectInspectorState();
          DynamicInspectorState.Add(result);
          return result;
        }
      }

      public void AddPendingEntity(Func<EntityState, EntityState> func) {
        var entity = new EntityState();

        int pendingCount = 0;
        for (int i = Entities.Count - 1; i >= 0 && Entities[i].EntityRefIndex < 0; --i) {
          ++pendingCount;
        }

        entity.EntityRefIndex = -1 - pendingCount;
        entity.EntityRefVersion = int.MinValue;

        Entities.Add(func(entity));
      }

      public QuantumSimulationObjectInspectorState GetDynamicAssetInspector(int assetStateId) {
        var state = DynamicDB[assetStateId];
        if (state.InspectorId == 0) {
          return null;
        } else {
          return GetInspector(state.InspectorId);
        }
      }

      public QuantumSimulationObjectInspectorState GetEntityInspector(int entityStateId) {
        var state = Entities[entityStateId];
        if (state.InspectorId == 0) {
          return null;
        } else {
          return GetInspector(state.InspectorId);
        }
      }

      public QuantumSimulationObjectInspectorState GetSystemInspector(int systemStateId) {
        if (systemStateId >= Systems.Count) {
          return null;
        }

        var state = Systems[systemStateId];
        if (state.InspectorId == 0) {
          return null;
        } else {
          return GetInspector(state.InspectorId);
        }
      }

      public QuantumSimulationObjectInspectorState GetPlayerInspector(PlayerRef player) {
        var state = Players[player];
        if (state.InspectorId == 0) {
          return null;
        } else {
          return GetInspector(state.InspectorId);
        }
      }

      public QuantumSimulationObjectInspectorState GetInspector(int inspectorId) {
        return DynamicInspectorState[inspectorId - 1];
      }

      public bool RemoveEntityState(EntityRef entityRef) {
        var entityStateIndex = Entities.FindIndex(x => x.Entity == entityRef);
        if (entityStateIndex < 0) {
          return false;
        }
        Entities.RemoveAt(entityStateIndex);
        return true;
      }

      public bool UpdateEntityState(EntityRef entityRef, Func<EntityState, EntityState> func) {
        var entityStateIndex = Entities.FindIndex(x => x.Entity == entityRef);
        if (entityStateIndex < 0) {
          return false;
        }

        var entityState = Entities[entityStateIndex];
        var newState = func(entityState);
        Entities[entityStateIndex] = newState;
        return true;
      }

      internal void RemovePendingEntities() {
        int pendingCount = 0;
        for (int i = Entities.Count - 1; i >= 0 && Entities[i].IsPending; --i) {
          ++pendingCount;
        }
        if (pendingCount > 0) {
          Entities.RemoveRange(Entities.Count - pendingCount, pendingCount);
        }
      }
    }

    #endregion Model

    #region Tree

    private sealed class DynamicAssetTreeViewItem : RunnersTreeViewItemBase {
      public int StateIndex;

      public DynamicAssetTreeViewItem(int id, int depth, int stateId) : base(id, depth, string.Empty) {
        this.StateIndex = stateId;
      }

      protected override QuantumSimulationObjectInspectorState GetInspectorStateInternal(RunnerState runner) {
        return runner.GetDynamicAssetInspector(StateIndex);
      }
    }

    private sealed class DynamicDBTreeViewItem : RunnersTreeViewItemBase {

      public DynamicDBTreeViewItem(int id, int depth) : base(id, depth, "DynamicDB") {
      }

      protected override QuantumSimulationObjectInspectorState GetInspectorStateInternal(RunnerState runner) {
        return runner.DynamicDBInspectorState;
      }
    }

    private sealed class EntitiesTreeViewItem : RunnersTreeViewItemBase {

      public EntitiesTreeViewItem(int id, int depth) : base(id, depth, "Entities") {
      }

      protected override QuantumSimulationObjectInspectorState GetInspectorStateInternal(RunnerState runner) {
        return runner.EntitiesInspectorState;
      }
    }

    private sealed class EntityTreeViewItem : RunnersTreeViewItemBase {
      public bool ShouldBeHidden;
      public int StateIndex;

      public EntityTreeViewItem(int id, int depth, int stateIndex) : base(id, depth, string.Empty) {
        StateIndex = stateIndex;
      }

      public EntityRef Entity => State.Entity;
      public EntityState State => this.Runner.State.Entities[StateIndex];
      protected override QuantumSimulationObjectInspectorState GetInspectorStateInternal(RunnerState runner) {
        return runner.GetEntityInspector(StateIndex);
      }
    }

    private sealed class GlobalsTreeViewItem : RunnersTreeViewItemBase {

      public GlobalsTreeViewItem(int id, int depth) : base(id, depth, "Globals") {
      }

      protected override QuantumSimulationObjectInspectorState GetInspectorStateInternal(RunnerState runner) {
        return runner.GlobalsInspectorState;
      }
    }

    private sealed class SystemsTreeViewItem : RunnersTreeViewItemBase {

      public SystemsTreeViewItem(int id, int depth) : base(id, depth, "Systems") {
      }

      protected override QuantumSimulationObjectInspectorState GetInspectorStateInternal(RunnerState runner) {
        return runner.SystemsInspectorState;
      }
    }

    private sealed class SystemTreeViewItem : RunnersTreeViewItemBase {
      public int StateIndex;
      [SerializableType(WarnIfNoPreserveAttribute = true)]
      public SerializableType<SystemBase> Type;
      public int RuntimeIndex;
      public bool IsEnabled;

      public SystemTreeViewItem(int id, int depth, int stateIndex) : base(id, depth, string.Empty) {
        StateIndex = stateIndex;
      }

      protected override QuantumSimulationObjectInspectorState GetInspectorStateInternal(RunnerState runner) {
        return runner.GetSystemInspector(StateIndex);
      }
    }

    private sealed class PlayersTreeViewItem : RunnersTreeViewItemBase {

      public PlayersTreeViewItem(int id, int depth) : base(id, depth, "Players") {
      }

      protected override QuantumSimulationObjectInspectorState GetInspectorStateInternal(RunnerState runner) {
        return runner.PlayersInspectorState;
      }
    }

    private sealed class PlayerTreeViewItem : RunnersTreeViewItemBase {
      public int StateIndex;

      public PlayerState State => this.Runner.State.Players[StateIndex];
      public PlayerRef Player => State.Player;

      public PlayerTreeViewItem(int id, int depth, int stateIndex) : base(id, depth, string.Empty) {
        StateIndex = stateIndex;
      }

      protected override QuantumSimulationObjectInspectorState GetInspectorStateInternal(RunnerState runner) {
        return runner.GetPlayerInspector(StateIndex);
      }
    }

    private unsafe sealed class RunnersTreeView : TreeView {
      public Action<Rect, EntityTreeViewItem> EntityContextMenu;
      public Action<Rect, SystemTreeViewItem> SystemContextMenu;
      public Action<Rect, RunnerTreeViewItem> RunnerContextMenu;
      private List<int> _forceShowIds = null;
      private Model _model;

      private List<TreeViewItem> _rowsBuffer = new List<TreeViewItem>();

      private GUIContent tempContent = new GUIContent();

      public RunnersTreeView(TreeViewState state, Model model) : base(state) {
        _model = model;
      }

      public event Action<RunnersTreeView, IList<int>> TreeSelectionChanged;
      
      public List<ComponentTypeRef> WithComponents { get; set; }
      public List<ComponentTypeRef> WithoutComponents { get; set; }

      public RunnersTreeViewItemBase FindById(int id) {
        return (RunnersTreeViewItemBase)this.FindItem(id, rootItem);
      }

      internal void SetForceShowItems(List<int> ids) {
        _forceShowIds = ids;
      }

      protected override TreeViewItem BuildRoot() {
        return new TreeViewItem() {
          id = 0,
          depth = -1,
          displayName = "Root"
        };
      }

      protected override IList<TreeViewItem> BuildRows(TreeViewItem root) {
        _rowsBuffer.Clear();
        

        ComponentSet withComponents = new ComponentSet();
        ComponentSet withoutComponents = new ComponentSet();

        if (WithComponents != null) {
          withComponents = ComponentSet.Create(WithComponents.ToArray());
        }
        if (WithoutComponents != null) {
          withoutComponents = ComponentSet.Create(WithoutComponents.ToArray());
        }

        foreach (var runner in _model.Runners) {
          _model.GetFixedTreeNodeIds(runner.Id, out var runnerNodeId, out var globalsNodeId, out var entitiesNodeId, out var dynamicDBNodeId, out var playersNodeId, out var systemsNodeId);

          string runnerLabel = string.Empty;
          if (runner.IsActive) {
            if (runner.IsDefault) {
              runnerLabel = $"{runner.Id} @ {runner.Tick} [{(runner.UseVerifiedFrame ? "verified" : "predicted")}, default]";
            } else {
              runnerLabel = $"{runner.Id} @ {runner.Tick} [{(runner.UseVerifiedFrame ? "verified" : "predicted")}]";
            }
          } else {
            runnerLabel = $"{runner.Id} @ {runner.Tick} [inactive]";
          }

          var runnerRoot = new RunnerTreeViewItem(runnerNodeId, 0, runnerLabel, runner);
          _rowsBuffer.Add(runnerRoot);

          if (IsExpanded(runnerNodeId)) {
            _rowsBuffer.Add(new GlobalsTreeViewItem(globalsNodeId, 1));

            var entities = new EntitiesTreeViewItem(entitiesNodeId, 1);
            _rowsBuffer.Add(entities);

            if (IsExpanded(entitiesNodeId)) {
              int entityStateIndex = -1;
              foreach (var entity in runner.Entities) {
                ++entityStateIndex;

                var entityId = _model.GetTreeNodeIdForEntity(entitiesNodeId, entity);

                bool forcedShow = _forceShowIds?.Contains(entityId) == true;
                bool hiddenDueToComponentFilters = false;

                var componentSet = entity.EntityComponents;

                if (!withComponents.IsEmpty) {
                  if (!componentSet.IsSupersetOf(withComponents)) {
                    hiddenDueToComponentFilters = true;
                  }
                }

                if (!withoutComponents.IsEmpty) {
                  if (componentSet.Overlaps(withoutComponents)) {
                    hiddenDueToComponentFilters = true;
                  }
                }

                if (hiddenDueToComponentFilters && !forcedShow) {
                  continue;
                }

                var entityNode = new EntityTreeViewItem(entityId, 2, entityStateIndex) {
                  ShouldBeHidden = hiddenDueToComponentFilters
                };
                _rowsBuffer.Add(entityNode);
              }
            } else if (runner.Entities.Any()) {
              entities.children = CreateChildListForCollapsedParent();
            }

            var players = new PlayersTreeViewItem(playersNodeId, 1);
            _rowsBuffer.Add(players);
            if (IsExpanded(playersNodeId)) {
              var playerStateIndex = 0;
              foreach (var player in runner.Players) {
                var playerNodeId = _model.GetTreeNodeForPlayer(playersNodeId, player.Player);
                _rowsBuffer.Add(new PlayerTreeViewItem(playerNodeId, 2, playerStateIndex++));
              }
            } else {
              players.children = CreateChildListForCollapsedParent();
            }

            var systems = new SystemsTreeViewItem(systemsNodeId, 1);
            _rowsBuffer.Add(systems);

            if (IsExpanded(systemsNodeId)) {
              CreateSystemTree(runner.Systems, ref _rowsBuffer, systemsNodeId);
            } else if (runner.Systems.Any()) {
              systems.children = CreateChildListForCollapsedParent();
            }

            var dynamicDB = new DynamicDBTreeViewItem(dynamicDBNodeId, 1);
            _rowsBuffer.Add(dynamicDB);

            if (IsExpanded(dynamicDBNodeId)) {
              int dynamicAssetId = 0;
              foreach (var dynamicAsset in runner.DynamicDB) {
                _rowsBuffer.Add(new DynamicAssetTreeViewItem(_model.GetTreeNodeIdForDynamicAsset(dynamicDBNodeId, dynamicAsset.Guid), 2, dynamicAssetId++));
              }
            } else if (runner.DynamicDB.Any()) {
              dynamicDB.children = CreateChildListForCollapsedParent();
            }
          } else {
            runnerRoot.children = CreateChildListForCollapsedParent();
          }
        }
        SetupParentsAndChildrenFromDepths(root, _rowsBuffer);

        return _rowsBuffer;
      }

      private void CreateSystemTree(List<SystemState> systems, ref List<TreeViewItem> tree,  int systemsNodeId) {
        var systemStateIndex = 0;
        var currentDepth = 0;
        var parentStack = new Stack<TreeViewItem>();

        parentStack.Push(tree.Last());

        foreach (var system in systems) {
          if (system.Depth > currentDepth) {
            parentStack.Push(tree.Last());
          }

          while (currentDepth > system.Depth) {
            currentDepth--;
            parentStack.Pop();
          }

          // Use the parent stack to find out the expanded state. Any other ways to do this?
          var isExpanded = true;
          if (parentStack.TryPeek(out var parent)) {
            isExpanded = IsExpanded(parent.id);
          }

          currentDepth = system.Depth;

          if (isExpanded) {
            var systemNodeId = _model.GetTreeNodeForSystem(systemsNodeId, system.RuntimeIndex);
            tree.Add(new SystemTreeViewItem(systemNodeId, system.Depth + 2, systemStateIndex) {
              Type = system.Type,
              RuntimeIndex = system.RuntimeIndex,
              IsEnabled = system.IsEnabled
            });
          }
          else {
            parent.children = CreateChildListForCollapsedParent();
          }

          systemStateIndex++;
        }
      }

      protected override float GetCustomRowHeight(int row, TreeViewItem item) {
        return 16.0f;
      }

      protected override void RowGUI(RowGUIArgs args) {
        var item = (RunnersTreeViewItemBase)args.item;
        var rect = args.rowRect;

        RunnerTreeViewItem root = item.Runner;

        if (root == item && !args.selected) {
          GUI.Label(rect, GUIContent.none, UnityInternal.Styles.HierarchyTreeViewSceneBackground);
        }

        Debug.Assert(root != null);

        bool enabledState = GUI.enabled;

        using (new EditorGUI.DisabledScope(false)) {
          var r = rect;
          r.xMin += GetContentIndent(item);

          GUIStyle style = UnityInternal.Styles.HierarchyTreeViewLine;
          bool shouldBeHidden = false;
          bool isEnabled = true;

          if (Event.current.type == EventType.Repaint) {
            if (item is EntityTreeViewItem entity) {
              if (string.IsNullOrEmpty(entity.displayName)) {
                var state = root.State.Entities[entity.StateIndex];
                if (state.IsPending) {
                  entity.displayName = $"<pending>";
                } else {
                  entity.displayName = state.Entity.ToString();
                }
              }

              shouldBeHidden = entity.ShouldBeHidden;

              if (root.State.UsePredictionCulling && root.State.ShowCulledEntityState) {
                var culledRect = rect;
                culledRect.xMin += GetContentIndent(item) - QuantumEditorGUI.ThumbnailWidth;
                QuantumEditorGUI.ShowCulledState(culledRect, entity.State.IsCulled);
              }

            } else if (item is DynamicAssetTreeViewItem asset) {
              if (string.IsNullOrEmpty(asset.displayName)) {
                var state = root.State.DynamicDB[asset.StateIndex];
                asset.displayName = state.Guid.ToString();
              }
            }
            else if (item is SystemTreeViewItem system) {
              if (string.IsNullOrEmpty(system.displayName)) {
                if (system.Type.IsValid) {
                  system.displayName = system.Type.Value.Name; 
                } else {
                  system.displayName = "Unknown";
                }
              }
              isEnabled = system.IsEnabled;
            }
            else if (item is PlayerTreeViewItem player) {
              player.displayName = player.Player.ToString();
              isEnabled = (player.State.InputFlags & DeterministicInputFlags.PlayerNotPresent) == 0;
            }

            tempContent.text = item.displayName;

            var hasState = item.GetInspectorState(root.State) != null;

            using (style.FontStyleScope(italic: shouldBeHidden)) {
              GUI.enabled = (root.RunnerModel.IsActive || hasState) && isEnabled;
              style.Draw(r, tempContent, false, false, args.selected, args.focused);
              GUI.enabled = enabledState;
            }
          }

          var size = style.CalcSize(tempContent);

          if (item is EntityTreeViewItem) {
            // make sure the width is a multiple of thumbnail width; that way they'll align nicely
            size.x = Mathf.Ceil(size.x / QuantumEditorGUI.ThumbnailWidth) * QuantumEditorGUI.ThumbnailWidth;
          }

          r.xMin += size.x;
          rect = r;

          if (item is EntityTreeViewItem e) {
            var state = root.State.Entities[e.StateIndex];
            foreach (var componentName in _model.GetComponentNames(state.EntityComponents)) {
              rect = QuantumEditorGUI.ComponentThumbnailPrefix(rect, componentName);
            }
          } else if ( item is DynamicAssetTreeViewItem asset ) {
            var state = root.State.DynamicDB[asset.StateIndex];
            QuantumEditorGUI.AssetThumbnailPrefix(rect, state.Type);
          }
        }

        if (item is RunnerTreeViewItem runner) {
          QuantumEditorGUI.HandleContextMenu(args.rowRect, runner, RunnerContextMenu);
        } else if (item is EntityTreeViewItem entity) {
          QuantumEditorGUI.HandleContextMenu(args.rowRect, entity, EntityContextMenu, showButton: false);
        } else if (item is SystemTreeViewItem system) {
          QuantumEditorGUI.HandleContextMenu(args.rowRect, system, SystemContextMenu, showButton: false);
        }
      }

      protected override void SelectionChanged(IList<int> selectedIds) {
        TreeSelectionChanged?.Invoke(this, selectedIds);
      }
    }

    private abstract class RunnersTreeViewItemBase : TreeViewItem {

      public RunnersTreeViewItemBase(int id, int depth, string name) : base(id, depth, name) {
      }

      public RunnerTreeViewItem Runner {
        get {
          for (TreeViewItem i = this; i != null; i = i.parent) {
            if (i is RunnerTreeViewItem r) {
              return r;
            }
          }
          throw new InvalidOperationException();
        }
      }

      public RunnerState RunnerModel => Runner.State;

      public QuantumSimulationObjectInspectorState GetInspectorState(RunnerState state = null) {
        return GetInspectorStateInternal(state ?? Runner.State);
      }

      protected abstract QuantumSimulationObjectInspectorState GetInspectorStateInternal(RunnerState runner);
    }

    private sealed class RunnerTreeViewItem : RunnersTreeViewItemBase {
      public RunnerState State;

      public RunnerTreeViewItem(int id, int depth, string name, RunnerState runner) : base(id, depth, name) {
        this.State = runner;
      }

      protected override QuantumSimulationObjectInspectorState GetInspectorStateInternal(RunnerState runner) {
        return runner.RunnerInspectorState;
      }
    }

    #endregion

    private sealed class CommandQueue : KeyedCollection<long, Tuple<string, DebugCommand.Payload>> {

      public Tuple<string, DebugCommand.Payload> RemoveReturn(long key) {
        if (!Contains(key)) {
          return null;
        } else {
          var result = this[key];
          this.Remove(key);
          return result;
        }
      }

      protected override long GetKeyForItem(Tuple<string, DebugCommand.Payload> item) {
        return item.Item2.Id;
      }
    }

    private sealed class Skin {
      public readonly GUIStyle inspectorBackground = new GUIStyle(UnityInternal.Styles.BoxWithBorders);

      public readonly GUIStyle inspectorPaddingStyle = new GUIStyle() {
        padding = new RectOffset(1, 0, 0, 0)
      };
      
      public readonly GUIContent titleContent = new GUIContent("Quantum State Inspector");
      public readonly GUIContent addComponentContent = new GUIContent("Add/Override Components");


      public GUIContent[] frameTypeOptions = new[] {
        new GUIContent("Predicted"),
        new GUIContent("Verified")
      };

      public Skin() {
        ++inspectorBackground.padding.left;
      }

      public float errorHeight => 40;
      public float toolbarDropdownButtonWidth => 15;
      public float toolbarHeight => 21;
    }
  }
}