namespace Quantum {
  using System;
  using System.Collections.Generic;
  using Profiling;
  using UnityEngine;
  using static QuantumUnityExtensions;

  /// <summary>
  /// The Entity View Updater is essential. An instance needs to be present to create Entity Views as the 
  /// simulation view representations based on the <see cref="Quantum.EntityView"/> components.
  /// </summary>
  public unsafe class QuantumEntityViewUpdater : QuantumMonoBehaviour {
    /// <summary>
    /// "Optionally provide a transform that all entity views will be parented under."
    /// </summary>
    [InlineHelp]
    public Transform ViewParentTransform = null;

    /// <summary>
    /// Disable you don't intend to use MapData component.
    /// </summary>
    [InlineHelp]
    public bool AutoFindMapData = true;

    /// <summary>
    /// Configuration of the snapshot interpolation mode of EntityViews.
    /// </summary>
    [InlineHelp]
    public QuantumSnapshotInterpolationTimer SnapshotInterpolation = new QuantumSnapshotInterpolationTimer();

    // current map
    [NonSerialized] QuantumMapData _mapData = null;
    // current set of entities that should be removed
    HashSet<EntityRef> _removeEntities = new HashSet<EntityRef>();
    // current set of active entities
    HashSet<EntityRef> _activeEntities = new HashSet<EntityRef>();
    // current set of active prefabs
    Dictionary<EntityRef, QuantumEntityView> _activeViews = new Dictionary<EntityRef, QuantumEntityView>(256);
    // teleport state variable
    Boolean _teleport;
    // cache the game that is currently observed by this class
    QuantumGame _observedGame = null;
    // Scene view components
    List<IQuantumViewComponent> _viewComponents = new List<IQuantumViewComponent>();
    Queue<IQuantumViewComponent> _viewComponentsToAdd = new Queue<IQuantumViewComponent>();
    Queue<IQuantumViewComponent> _viewComponentsToRemove = new Queue<IQuantumViewComponent>();
    HashSet<IQuantumViewComponent> _viewComponentZombies = new HashSet<IQuantumViewComponent>();
    // the view context for this entity view update
    Dictionary<Type, IQuantumViewContext> _viewContexts;
    // the pool that can optionally be used to create entity views
    IQuantumEntityViewPool _entityViewPool;


    /// <summary>
    /// Provides access to MapData when deriving from this class for example.
    /// </summary>
    protected QuantumMapData MapData => _mapData;
    /// <summary>
    /// Provides access to the active entities. 
    /// </summary>
    protected HashSet<EntityRef> ActiveEntities => _activeEntities;
    /// <summary>
    /// Privates access to the entities that should be removed.
    /// </summary>
    protected HashSet<EntityRef> RemoveEntities => _removeEntities;
    /// <summary>
    /// Provides access to the active views.
    /// </summary>
    protected Dictionary<EntityRef, QuantumEntityView> ActiveViews => _activeViews;
    /// <summary>
    /// Returns <see langword="true"/> when the entities are marked to teleport next update. Set by <see cref="TeleportAllEntities"/>.
    /// </summary>
    protected Boolean Teleport => _teleport;
    /// <summary>
    /// Returns the currently observed game.
    /// </summary>
    public QuantumGame ObservedGame => _observedGame;
    /// <summary>
    /// Get or set the EntityViewPool. If not set, the EntityViewUpdater will use the first found pool in the hierarchy.
    /// </summary>
    public IQuantumEntityViewPool Pool {
      get {
        return _entityViewPool;
      }
      set {
        Assert.Always(_entityViewPool == null, "Cannot change the pool once it was set once.");
        _entityViewPool = value;
      }
    }
    /// <summary>
    /// Access all view contexts that could be loaded in the scene as children of the EntityViewUpdater.
    /// </summary>
    public Dictionary<Type, IQuantumViewContext> Context {
      get {
        return _viewContexts;
      }
    }
    /// <summary>
    /// Obsolete: Use GetView instead
    /// </summary>
    [Obsolete("Use GetView instead")]
    public QuantumEntityView GetPrefab(EntityRef entityRef) => GetView(entityRef);

    /// <summary>
    /// Try to get the view instance for the given Quantum entity reference.
    /// </summary>
    /// <param name="entityRef"></param>
    /// <returns></returns>
    public QuantumEntityView GetView(EntityRef entityRef) {
      _activeViews.TryGetValue(entityRef, out QuantumEntityView root);
      return root;
    }

    /// <summary>
    /// Obsolete: Use TeleportAllEntities() instead.
    /// </summary>
    [Obsolete("Use TeleportAllEntities() instead")]
    public void SetTeleportOnce() => TeleportAllEntities();

    /// <summary>
    /// Set all entity views to teleport on the next update.
    /// </summary>
    public void TeleportAllEntities() {
      _teleport = true;
    }

    /// <summary>
    /// Set the current observed game.
    /// </summary>
    public void SetCurrentGame(QuantumGame game) {
      var gameChanged = _observedGame != null;

      _observedGame = game;

      if (gameChanged) {

        for (int i = 0; i < _viewComponents.Count; i++) {
          _viewComponents[i].GameChanged(_observedGame);
        }
        foreach (var view in _activeViews) {
          view.Value.GameChanged(_observedGame);
        }
      }
    }

    /// <summary>
    /// Unity Awake() callback, register callbacks and load view contexts.
    /// </summary>
    public void Awake() {
      QuantumCallback.Subscribe(this, (CallbackGameInit c) => OnGameInit(c.Game));
      QuantumCallback.Subscribe(this, (CallbackUnitySceneLoadDone c) => OnGameInit(c.Game));
      QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnObservedGameUpdated(c.Game), game => game == _observedGame);
      QuantumCallback.Subscribe(this, (CallbackGameDestroyed c) => OnObservedGameDestroyed(c.Game, true), game => game == _observedGame);
      QuantumCallback.Subscribe(this, (CallbackUnitySceneLoadBegin c) => OnObservedGameDestroyed(c.Game, false), game => game == _observedGame);

      Pool ??= GetComponent<IQuantumEntityViewPool>();

      LoadViewContexts();

      // Load initial view component from the hierarchy underneath EntityViewUpdater
      var initialViewComponents = GetComponentsInChildren<IQuantumViewComponent>();
      for (int i = 0; i < initialViewComponents.Length; i++) {
        if (initialViewComponents[i].IsInitialized == false) {
          initialViewComponents[i].Initialize(_viewContexts);
          _viewComponentsToAdd.Enqueue(initialViewComponents[i]);
        }
      }
    }

    private void LoadViewContexts() {
      if (_viewContexts != null) {
        return;
      }

      // Load view contexts
      _viewContexts = new Dictionary<Type, IQuantumViewContext>();
      var contexts = GetComponentsInChildren<IQuantumViewContext>();
      foreach (var c in contexts) {
        if (_viewContexts.ContainsKey(c.GetType())) {
          Debug.LogError($"The view context type {c.GetType()} already exists. Multiple contexts of the same type are not supported.");
        } else {
          _viewContexts.Add(c.GetType(), c);
        }
      }
    }

    /// <summary>
    /// Attach a non-entity view component that is then updated by the EntityViewUpdater.
    /// The <see cref="QuantumViewComponent{T}.OnActivate"/> will be deferred until the next view update.
    /// <para>Does not add the view component if it already was added (e.g. <see cref="IQuantumViewComponent.IsInitialized"/> returns <see langword="true"/>).</para>
    /// </summary>
    /// <param name="viewComponent">View component instance</param>
    public void AddViewComponent(IQuantumViewComponent viewComponent) {
      LoadViewContexts();
      if (viewComponent.IsInitialized == false) {
        viewComponent.Initialize(_viewContexts);
      }
      _viewComponentsToAdd.Enqueue(viewComponent);
    }

    /// <summary>
    /// Remove a view component from being updated by the EntityViewUpdater.
    /// The removing of the view component is being deferred until the next late update.
    /// </summary>
    /// <param name="viewComponent">View component instance to remove</param>
    public void RemoveViewComponent(IQuantumViewComponent viewComponent) {
      if (viewComponent.IsActive == false) {
        // Defer `Deactivate` to later for components that have not been activated yet.
        // Do not add them to the remove list, as they will never really be added to the active list.
        _viewComponentZombies.Add(viewComponent);
        return;
      }

      viewComponent.Deactivate();
      _viewComponentsToRemove.Enqueue(viewComponent);
    }

    private void OnGameInit(QuantumGame game) {
      if (_observedGame == null) {
        // attach to the first game found
        SetCurrentGame(game);
      }
    }

    private void OnObservedGameDestroyed(QuantumGame game, bool destroyed) {
      Debug.Assert(_observedGame == game);

      if (destroyed) {
        for (int i=0; i< _viewComponents.Count; i++) {
          _viewComponents[i].Deactivate();
        }

        // Game and session are shutdown instantly -> delete the game objects right away and don't wait for a cleanup between the ticks (OnUpdateView).
        // If objects are not destroyed here scripts on them that access QuantumRunner.Default will throw.
        foreach (var view in _activeViews) {
          if (!view.Value)
            continue;

          DestroyEntityView(game, view.Value);
        }

        _activeViews.Clear();
      }

      _observedGame = null;
    }

    private void OnObservedGameUpdated(QuantumGame game) {
      Debug.Assert(_observedGame == game);

      if (isActiveAndEnabled == false) {
        return;
      }

      using var profilerScope = HostProfiler.Start("QuantumEntityView.OnObservedGameUpdated");
      var verifiedFrame = game.Frames.Verified;
      
      if (verifiedFrame != null) {

        // Add view components
        while (_viewComponentsToAdd.Count > 0) {
          var vc = _viewComponentsToAdd.Dequeue();
          vc.Activate(verifiedFrame, game, null);

          // Zombies are components that were deactivated before getting activated in this loop.
          // For consistency we call Deactivate afterwards here instead of during RemoveViewComponent() and never add it to the active list.
          if (_viewComponentZombies.Remove(vc)) {
            vc.Deactivate();
            continue;
          }

          _viewComponents.Add(vc);
        }
        
        SnapshotInterpolation.Advance(verifiedFrame.Number, 1f / game.Session.SessionConfig.UpdateFPS);

        // Update view components
        for (int i = 0; i < _viewComponents.Count; i++) {
          if (_viewComponents[i].IsActiveAndEnabled) {
            _viewComponents[i].UpdateView();
          }
        }
      }

      if (game.Frames.Predicted != null) {
        bool checkPossiblyOrphanedMapEntityViews = false;

        if (_mapData == null && AutoFindMapData) {
          _mapData = FindFirstObjectByType<QuantumMapData>();
          if (_mapData) {
            checkPossiblyOrphanedMapEntityViews = true;
          }
        }

        _activeEntities.Clear();

        // Always use clock aliasing interpolation except during forced teleports.
        var useClockAliasingInterpolation = !_teleport;

        // Use error based interpolation only during multiplayer mode and when not forced teleporting.
        // For local games we want don't want error based interpolation as well as on forced teleports.
        var useErrorCorrection = game.Session.IsPredicted && game.Session.IsInterpolatable && _teleport == false;

        // Go through all verified entities and create new view instances for new entities.
        // Checks information (CreateBehaviour) on each EntityView if it should be created/destroyed during Verified or Predicted frames.
        SyncViews(game, game.Frames.Verified, QuantumEntityViewBindBehaviour.Verified);

        // Go through all entities in the current predicted frame (predicted == verified only during lockstep).
        SyncViews(game, game.Frames.Predicted, QuantumEntityViewBindBehaviour.NonVerified);

        // Sync the active view instances with the active entity list. Find outdated instances.
        _removeEntities.Clear();
        foreach (var key in _activeViews) {
          if (_activeEntities.Contains(key.Key) == false) {
            _removeEntities.Add(key.Key);
          }
        }

        // Destroy outdated view instances.
        foreach (var key in _removeEntities) {
          DestroyEntityView(game, key);
        }

        if (checkPossiblyOrphanedMapEntityViews) {
          Debug.Assert(_mapData);
          foreach (var view in _mapData.MapEntityReferences) {
            if (!view || !view.isActiveAndEnabled)
              continue;
            if (view.EntityRef == EntityRef.None) {
              view.Deactivate();
              DisableMapEntityInstance(view);
            }
          }
        }

        // Run over all view instances and update components using only entities from current frame.
        foreach (var kvp in _activeViews) {
          // grab instance
          var instance = kvp.Value;

          // make sure we do not try to update for an instance which doesn't exist.
          if (!instance) {
            continue;
          }

          // call update (and internally this would be resolved to either 2D or 3D transform)
          instance.UpdateView(useClockAliasingInterpolation, useErrorCorrection);
        }
      }

      // reset teleport to false always
      _teleport = false;
    }

    private void LateUpdate() {
      if (ObservedGame != null) {
        for (int i = 0; i < _viewComponents.Count; i++) {
          if (_viewComponents[i].IsActiveAndEnabled) {
            _viewComponents[i].LateUpdateView();
          }
        }

        foreach (var kvp in _activeViews) {
          kvp.Value.LateUpdateView();
        }
      }

      // Remove view components from the list
      while (_viewComponentsToRemove.Count > 0) { 
        _viewComponents.Remove(_viewComponentsToRemove.Dequeue());
      }

      _viewComponentZombies.Clear();
    }

    private void SyncViews(QuantumGame game, Frame frame, QuantumEntityViewBindBehaviour createBehaviour) {
      // update prefabs
      foreach (var (entity, view) in frame.GetComponentIterator<View>()) {
        CreateViewIfNeeded(game, frame, entity, view, createBehaviour);
      }

      // update map entities
      if (_mapData) {
        var currentMap = _mapData.Asset.Guid;
        if (currentMap == frame.MapAssetRef.Id) {
          BindMapEntities(game, frame, createBehaviour);
        } else if (frame.Map is DynamicMap dynamicMap && dynamicMap.SourceMap.Id == currentMap) {
          BindMapEntities(game, frame, createBehaviour);
        } else {
          // can't update map entities because of map mismatch
        }
      }
    }

    private void BindMapEntities(QuantumGame game, Frame frame, QuantumEntityViewBindBehaviour createBehaviour) {
      foreach (var (entity, mapEntityLink) in frame.GetComponentIterator<MapEntityLink>()) {
        BindMapEntityIfNeeded(game, frame, entity, mapEntityLink, createBehaviour);
      }
    }

    void CreateViewIfNeeded(QuantumGame game, Frame frame, EntityRef handle, View view, QuantumEntityViewBindBehaviour createBehaviour) {
      var entityView = frame.FindAsset<EntityView>(view.Current.Id);

      if (_activeViews.TryGetValue(handle, out var instance)) {
        if (instance.BindBehaviour == createBehaviour) {
          if (entityView == null) {
            // Quantum.View has been revoked for this entity
            DestroyEntityView(game, handle);
          } else {
            var currentGuid = entityView.Guid;
            if (instance.AssetGuid == currentGuid) {
              _activeEntities.Add(handle);
            } else {
              // The Guid changed, recreate the view instance for this entity.
              DestroyEntityView(game, handle);
              if (CreateView(game, frame, handle, entityView, createBehaviour) != null) {
                _activeEntities.Add(handle);
              }
            }
          }
        }
      } else if (entityView != null) {
        // Create a new view instance for this entity.
        if (CreateView(game, frame, handle, entityView, createBehaviour) != null) {
          _activeEntities.Add(handle);
        }
      }
    }

    private void BindMapEntityIfNeeded(QuantumGame game, Frame frame, EntityRef handle, MapEntityLink mapEntity, QuantumEntityViewBindBehaviour createBehaviour) {
      if (_activeViews.TryGetValue(handle, out var instance)) {
        if (instance.AssetGuid.IsValid) {
          // this can happen if a scene prototype has the View property set to an asset
        } else {
          if (instance.BindBehaviour == createBehaviour) {
            _activeEntities.Add(handle);
          }
        }
      } else {
        if (BindMapEntity(game, frame, handle, mapEntity, createBehaviour) != null) {
          _activeEntities.Add(handle);
        }
      }
    }

    QuantumEntityView CreateView(QuantumGame game, Frame frame, EntityRef handle, EntityView view, QuantumEntityViewBindBehaviour createBehaviour) {
      if (view == null) {
        return null;
      }

      if (view.Prefab == null) {
        LoadMissingPrefab(view);
        if (view.Prefab == null) {
          return null;
        }
      }

      // TODO: badfix
      var viewComp = view.Prefab.GetComponent<QuantumEntityView>();
      if (viewComp.BindBehaviour != createBehaviour)
        return null;

      QuantumEntityView instance;
      if (TryGetTransform(frame, handle, out Vector3 position, out Quaternion rotation)) {
        instance = CreateEntityViewInstance(view, position, rotation);
      } else {
        instance = CreateEntityViewInstance(view);
      }

      if (ViewParentTransform != null) {
        instance.transform.SetParent(ViewParentTransform);
      }

      instance.AssetGuid = view.Guid;
      OnEntityViewInstantiated(game, frame, instance, handle);

      // return instance
      return instance;
    }

    QuantumEntityView BindMapEntity(QuantumGame game, Frame frame, EntityRef handle, MapEntityLink mapEntity, QuantumEntityViewBindBehaviour createBehaviour) {
      Debug.Assert(_mapData);

      if (_mapData.MapEntityReferences.Count <= mapEntity.Index) {
        Debug.LogErrorFormat(this,
          "MapData on \"{0}\" does not have a map entity slot with an index {1} (entity: {2}). EntityView will not be assigned. " +
          "Make sure all baked data is up to date.", _mapData.gameObject.scene.path, mapEntity.Index, handle);
        return null;
      }

      var instance = _mapData.MapEntityReferences[mapEntity.Index];

      if (instance?.BindBehaviour != createBehaviour) {
        return null;
      }

      if (instance.EntityRef.IsValid && instance.EntityRef != handle) {
        // possible when a map is restarted
        DestroyEntityView(game, instance.EntityRef);
      }

      if (TryGetTransform(frame, handle, out Vector3 position, out Quaternion rotation)) {
        ActivateMapEntityInstance(instance, position, rotation);
      } else {
        ActivateMapEntityInstance(instance);
      }

      instance.AssetGuid = new AssetGuid();
      OnEntityViewInstantiated(game, frame, instance, handle);
      return instance;
    }

    private void OnEntityViewInstantiated(QuantumGame game, Frame frame, QuantumEntityView instance, EntityRef handle) {
      if ((instance.ViewFlags & QuantumEntityViewFlags.DisableEntityRefNaming) == 0) {
        instance.gameObject.name = handle.ToString();
      }

      instance.EntityRef = handle;

      // add to lookup
      _activeViews.Add(handle, instance);

      instance.Activate(game, frame, Context, this);
      instance.OnEntityInstantiated.Invoke(game);
    }

    void DestroyEntityView(QuantumGame game, EntityRef entityRef) {
      QuantumEntityView view;

      if (_activeViews.TryGetValue(entityRef, out view)) {
        DestroyEntityView(game, view);
      }

      _activeViews.Remove(entityRef);
    }

    /// <summary>
    /// Destroys the entity view instance and removes it from the active views list.
    /// <para>Can be overwritten in derived class.</para>
    /// </summary>
    /// <param name="game">The game reference the entity belongs to.</param>
    /// <param name="view">The entity view object.</param>
    protected virtual void DestroyEntityView(QuantumGame game, QuantumEntityView view) {
      if ((object)view == null) {
        Debug.LogError("Invalid entity view GameObject.");
        return;
      }

      view.OnEntityDestroyed.Invoke(game);

      if (view.ManualDisposal == false) {
        if (view == null) {
          Debug.LogWarning($"Quantum Entity View {view?.EntityRef} was already destroyed");
        } else {
          view.Deactivate();
          if (view.AssetGuid.IsValid) {
            DestroyEntityViewInstance(view);
          } else {
            DisableMapEntityInstance(view);
          }
        }
      }
    }

    void OnDestroy() {
      for (int i = 0; i < _viewComponents.Count; i++) {
        _viewComponents[i].Deactivate();
      }
      _viewComponents.Clear();
      _viewComponentsToAdd.Clear();
      _viewComponentsToRemove.Clear();
      _viewComponentZombies.Clear();

      foreach (var kvp in _activeViews) {
        if (kvp.Value && kvp.Value.gameObject) {
          Destroy(kvp.Value.gameObject);
        }
      }
    }

    /// <summary>
    /// Creates a new entity view instance.
    /// <para>Can be overwritten in derived class.</para>
    /// </summary>
    /// <param name="asset">View asset.</param>
    /// <param name="position">World position.</param>
    /// <param name="rotation">Initial rotation.</param>
    /// <returns>A new Quantum entity view instance.</returns>
    protected virtual QuantumEntityView CreateEntityViewInstance(Quantum.EntityView asset, Vector3? position = null, Quaternion? rotation = null) {
      Debug.Assert(asset.Prefab != null);
      var viewPrefab = asset.Prefab.GetComponent<QuantumEntityView>();

      if (Pool != null) {
        var instance = Pool.Create(viewPrefab);

        if (position.HasValue == true) {
          instance.transform.position = position.Value;
        }

        if (rotation.HasValue == true) {
          instance.transform.rotation = rotation.Value;
        }

        return instance;
      } else {
        var instance = position.HasValue && rotation.HasValue ? Instantiate(viewPrefab, position.Value, rotation.Value) : Instantiate(viewPrefab);
        return instance;
      }
    }

    /// <summary>
    /// Destroys an entity view.
    /// <para>Can be overwritten in derived class.</para>
    /// </summary>
    /// <param name="instance">Instance to destroy.</param>
    protected virtual void DestroyEntityViewInstance(QuantumEntityView instance) {
      if (Pool != null) {
        Pool.Destroy(instance);
      } else {
        Destroy(instance.gameObject);
      }
    }

    /// <summary>
    /// Activates a map entity instance.
    /// <para>Can be overwritten in derived class.</para>
    /// </summary>
    protected virtual void ActivateMapEntityInstance(QuantumEntityView instance, Vector3? position = null, Quaternion? rotation = null) {
      if (position.HasValue)
        instance.transform.position = position.Value;
      if (rotation.HasValue)
        instance.transform.rotation = rotation.Value;
      if (!instance.gameObject.activeSelf) {
        instance.gameObject.SetActive(true);
      }
    }

    /// <summary>
    /// Disabled a map entity instance.
    /// <para>Can be overwritten in derived class.</para>
    /// </summary>
    protected virtual void DisableMapEntityInstance(QuantumEntityView instance) {
      instance.gameObject.SetActive(false);
    }

    /// <summary>
    /// Is triggered when <see cref="EntityView.Prefab"/> is null and expects to be set after this callback.
    /// <para>Can be overwritten in derived class.</para>
    /// </summary>
    /// <param name="viewAsset">View asset to load.</param>
    protected virtual void LoadMissingPrefab(Quantum.EntityView viewAsset) {
    }

    private static bool TryGetTransform(Frame frame, EntityRef handle, out Vector3 position, out Quaternion rotation) {
      if (frame.Has<Transform2D>(handle)) {
        var transform2D = frame.Unsafe.GetPointer<Transform2D>(handle);
        position = transform2D->Position.ToUnityVector3();
        rotation = transform2D->Rotation.ToUnityQuaternion();
        return true;
      } else if (frame.Has<Transform3D>(handle)) {
        var transform3D = frame.Unsafe.GetPointer<Transform3D>(handle);
        position = transform3D->Position.ToUnityVector3();
        rotation = transform3D->Rotation.ToUnityQuaternion();
        return true;
      } else {
        position = default;
        rotation = default;
        return false;
      }
    }
  }
}