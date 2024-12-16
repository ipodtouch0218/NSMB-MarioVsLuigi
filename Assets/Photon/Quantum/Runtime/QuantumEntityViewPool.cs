namespace Quantum {
  using System;
  using System.Collections.Generic;
  using UnityEngine;

  /// <summary>
  /// An implementation of a EntityView pool to be used with the <see cref="QuantumEntityViewUpdater.Pool"/>.
  /// Add this behaviour to the same game object that the <see cref="QuantumEntityViewUpdater"/> behaviour is on.
  /// Using QuantumCallback.Subscribe() with pooled objects as listener needs to use the onlyIfActiveAndEnabled option to not be callbacks from disabled pooled objects.
  /// </summary>
  public class QuantumEntityViewPool : QuantumMonoBehaviour, IQuantumEntityViewPool {
    /// <summary>
    /// Returns how many items are inside the pool in total.
    /// </summary>
    public int PooledCount { get { return _all.Count; } }
    /// <summary>
    /// Returns how many pooled items are currently in use.
    /// </summary>
    public int BorrowedCount { get { return _borrowed.Count; } }

#pragma warning disable CS0414 // The private field is assigned but its value is never used (#if UNITY_EDITOR)
    [SerializeField] private bool _hidePooledObjectsInHierarchy = true;
#pragma warning restore CS0414 // The private field is assigned but its value is never used
    [SerializeField] private bool _resetGameObjectScale = false;
    [SerializeField] private List<PooledObject> _precacheObjects;
    private readonly Dictionary<GameObject, Stack<GameObject>> _cached = new(128);
    private readonly Dictionary<GameObject, GameObject> _borrowed = new(1024);
    private readonly List<DelayedDestroy> _deferred = new(128);
    private readonly Stack<DelayedDestroy> _pool = new(128);
    private readonly List<GameObject> _all = new(1024);

    /// <summary>
    /// Create a pooled game object and return the component of chose type.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="prefab">Prefab to instantiate</param>
    /// <param name="activate">Call SetActive() on the game object</param>
    /// <param name="createIfEmpty">Create a new entity if there is no suitable one found in the pool</param>
    /// <returns>Component on the created prefab instance, can be null</returns>
    public virtual T Create<T>(T prefab, bool activate = true, bool createIfEmpty = true) where T : Component {
      return Create(prefab, null, activate, createIfEmpty);
    }

    /// <summary>
    /// Create a pooled game object.
    /// </summary>
    /// <param name="prefab">Prefab to instantiate</param>
    /// <param name="activate">Call SetActive() on the game object</param>
    /// <param name="createIfEmpty">Create a new entity if there is no suitable one found in the pool</param>
    /// <returns>An instance of the prefab</returns>
    public virtual GameObject Create(GameObject prefab, bool activate = true, bool createIfEmpty = true) {
      return Create(prefab, null, activate, createIfEmpty);
    }

    /// <summary>
    /// Create a pooled game object and return the component of chose type.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="prefab">Prefab to instantiate</param>
    /// <param name="parent">Calls SetParent(parent) on the new game object transform when set</param>
    /// <param name="activate">Call SetActive() on the game object</param>
    /// <param name="createIfEmpty">Create a new entity if there is no suitable one found in the pool</param>
    /// <returns>Component on the created prefab instance, can be null</returns>
    public virtual T Create<T>(T prefab, Transform parent, bool activate = true, bool createIfEmpty = true) where T : Component {
      GameObject instance = Create(prefab.gameObject, parent, activate, createIfEmpty);
      return instance != null ? instance.GetComponent<T>() : null;
    }

    /// <summary>
    /// Create a pooled game object.
    /// </summary>
    /// <param name="prefab">Prefab to instantiate</param>
    /// <param name="parent">Calls SetParent(parent) on the new game object transform when set</param>
    /// <param name="activate">Call SetActive() on the game object</param>
    /// <param name="createIfEmpty">Create a new entity if there is no suitable one found in the pool</param>
    /// <returns>An instance of the prefab</returns>
    public virtual GameObject Create(GameObject prefab, Transform parent, bool activate = true, bool createIfEmpty = true) {
      if (_cached.TryGetValue(prefab, out Stack<GameObject> stack) == false) {
        stack = new Stack<GameObject>();
        _cached[prefab] = stack;
      }

      if (stack.Count == 0) {
        if (createIfEmpty == true) {
          CreateInstance(prefab);
        } else {
          Debug.LogWarningFormat("Prefab {0} not available in cache, returning NULL", prefab.name);
          return null;
        }
      }

      GameObject instance = stack.Pop();

      _borrowed[instance] = prefab;

      Transform instanceTransform = instance.transform;

      if (parent != null) {
        instanceTransform.SetParent(parent, false);
      }

      instanceTransform.localPosition = Vector3.zero;
      instanceTransform.localRotation = Quaternion.identity;
      if (_resetGameObjectScale) {
        instanceTransform.localScale = Vector3.one;
      }

      if (activate == true) {
        instance.SetActive(true);
      }

#if UNITY_EDITOR
      if (_hidePooledObjectsInHierarchy == true) {
        instance.hideFlags &= ~HideFlags.HideInHierarchy;
      }
#endif
      return instance;
    }

    /// <summary>
    /// Destroy or return the pooled game object that the component is attached to.
    /// </summary>
    /// <param name="component">Component that belongs to the pooled game object.</param>
    /// <param name="deactivate">Call SetActive(false) on the pooled game object before returning it to the pool</param>
    public virtual void Destroy(Component component, bool deactivate = true) {
      Destroy(component.gameObject, deactivate);
    }

    /// <summary>
    /// Destroy or return the pooled game object.
    /// </summary>
    /// <param name="instance">Poole game object</param>
    /// <param name="deactivate">Call SetActive(false) on the pooled game object before returning it to the pool</param>
    public virtual void Destroy(GameObject instance, bool deactivate = true) {
      if (deactivate == true) {
        instance.SetActive(false);
      }

      instance.transform.SetParent(null, false);

      _cached[_borrowed[instance]].Push(instance);
      _borrowed.Remove(instance);

#if UNITY_EDITOR
      if (_hidePooledObjectsInHierarchy == true) {
        instance.hideFlags |= HideFlags.HideInHierarchy;
      }
#endif
    }

    /// <summary>
    /// Destroy or return the pooled game object after a delay.
    /// </summary>
    /// <param name="instance">Poole game object</param>
    /// <param name="delay">Delay in seconds to complete returning it to the pool</param>
    public virtual void Destroy(GameObject instance, float delay) {
      DelayedDestroy toReturn = _pool.Count > 0 ? _pool.Pop() : new DelayedDestroy();
      toReturn.GameObject = instance;
      toReturn.Delay = delay;

      _deferred.Add(toReturn);
    }

    /// <summary>
    /// Create prefab instances and fill the pool.
    /// </summary>
    /// <param name="prefab">Prefab to created pooled instances</param>
    /// <param name="desiredCount">The number of instances to create and add to the pool</param>
    public virtual void Prepare(GameObject prefab, int desiredCount) {
      if (_cached.TryGetValue(prefab, out Stack<GameObject> stack) == false) {
        stack = new Stack<GameObject>();
        _cached[prefab] = stack;
      }

      while (stack.Count < desiredCount) {
        CreateInstance(prefab);
      }
    }

    /// <summary>
    /// Create pre cached pooled game objects during Awake().
    /// </summary>
    public void Awake() {
      foreach (PooledObject cacheObject in _precacheObjects) {
        _cached[cacheObject.GameObject] = new Stack<GameObject>();

        for (int idx = 0; idx < cacheObject.Count; ++idx) {
          CreateInstance(cacheObject.GameObject);
        }
      }
    }

    /// <summary>
    /// Shutdown the pool.
    /// </summary>
    public void OnDestroy() {
      _deferred.Clear();
      _borrowed.Clear();
      _cached.Clear();

      foreach (GameObject instance in _all) {
        GameObject.Destroy(instance);
      }

      _all.Clear();
    }

    /// <summary>
    /// Update is used to track deferred pooled game object destroy requests.
    /// </summary>
    public void Update() {
      for (int idx = _deferred.Count; idx-- > 0;) {
        DelayedDestroy deferred = _deferred[idx];

        deferred.Delay -= Time.deltaTime;
        if (deferred.Delay > 0.0f)
          continue;

        //	_deferred.RemoveAtWithSwap(idx);
        _deferred.RemoveAt(idx);
        Destroy(deferred.GameObject, true);

        deferred.Reset();
        _pool.Push(deferred);
      }
    }

    private void CreateInstance(GameObject prefab) {
      GameObject instance = GameObject.Instantiate(prefab, null, false);
      instance.name = prefab.name;

      instance.SetActive(false);
      _cached[prefab].Push(instance);
      _all.Add(instance);

#if UNITY_EDITOR
      if (_hidePooledObjectsInHierarchy == true) {
        instance.hideFlags |= HideFlags.HideInHierarchy;
      }
#endif
    }

    [Serializable]
    private sealed class PooledObject {
#pragma warning disable CS0649 // The fields are set by Unity inspector.
      /// <summary>
      /// The number of game objects instances to pre allocate.
      /// </summary>
      public int Count;
      /// <summary>
      /// The prefab to instantiate pooled game objects from.
      /// </summary>
      public GameObject GameObject;
#pragma warning restore CS0649
    }

    private sealed class DelayedDestroy {
      public GameObject GameObject;
      public float Delay;

      public void Reset() {
        GameObject = null;
        Delay = 0.0f;
      }
    }
  }
}
