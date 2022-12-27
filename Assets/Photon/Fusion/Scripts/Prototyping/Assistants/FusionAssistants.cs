#if UNITY_EDITOR

using UnityEngine;
using System;
using UnityEditor;

namespace Fusion.Assistants {
  public static class FusionAssistants {
    public const int PRIORITY = 0;
    public const int PRIORITY_LOW = 1000;

    //// Default Material
    //private const string _prototypeMaterialGUID = "38fbbd3db9e06c54e8a11016e2381469";
    //private static Material _prototypeMaterial;
    //public static Material PrototypeMaterial {
    //  get {
    //    if (_prototypeMaterial != null)
    //      return _prototypeMaterial;

    //    var path = AssetDatabase.GUIDToAssetPath(_prototypeMaterialGUID);
    //    if (path != null && path != "")
    //      _prototypeMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
    //    return _prototypeMaterial;
    //  }
    //}

    //// Black Material
    //private const string _prototypeMaterialBlackGUID = "5d1b896bc311a1d438c929c45b0c5fbc";
    //private static Material _prototypeMaterialBlack;
    //public static Material PrototypeMaterialBlack {
    //  get {
    //    if (_prototypeMaterialBlack != null)
    //      return _prototypeMaterialBlack;

    //    var path = AssetDatabase.GUIDToAssetPath(_prototypeMaterialBlackGUID);
    //    if (path != null && path != "")
    //      _prototypeMaterialBlack = AssetDatabase.LoadAssetAtPath<Material>(path);
    //    return _prototypeMaterialBlack;
    //  }
    //}


    //// Floor Material
    //private const string _prototypeMaterialFloorGUID = "002db72054162e04b917e86a395e0a0f";
    //private static Material _prototypeMaterialFloor;
    //public static Material PrototypeMaterialFloor {
    //  get {
    //    if (_prototypeMaterialFloor != null)
    //      return _prototypeMaterialFloor;

    //    var path = AssetDatabase.GUIDToAssetPath(_prototypeMaterialFloorGUID);
    //    if (path != null && path != "")
    //      _prototypeMaterialFloor = AssetDatabase.LoadAssetAtPath<Material>(path);
    //    return _prototypeMaterialFloor;
    //  }
    //}

    //// Box Material
    //private const string _prototypeMaterialBoxGUID = "2544ff8e0cb0b4649ad11a93d3259ffa";
    //private static Material _prototypeMaterialBox;
    //public static Material PrototypeMaterialBox {
    //  get {
    //    if (_prototypeMaterialBox != null)
    //      return _prototypeMaterialBox;

    //    var path = AssetDatabase.GUIDToAssetPath(_prototypeMaterialBoxGUID);
    //    if (path != null && path != "")
    //      _prototypeMaterialBox = AssetDatabase.LoadAssetAtPath<Material>(path);
    //    return _prototypeMaterialBox;
    //  }
    //}


    /// <summary>
    /// Ensure GameObject has component T. Will create as needed and return the found/created component.
    /// </summary>
    public static T EnsureComponentExists<T>(this GameObject go) where T : Component {
      if (go.TryGetComponent<T>(out var t))
        return t;

      else
        return go.AddComponent<T>();
    }

    public static GameObject EnsureComponentsExistInScene(string preferredGameObjectName, params Type[] components) {

      GameObject go = null;

      foreach(var c in components) {
        var found = UnityEngine.Object.FindObjectOfType(c);
        if (found)
          continue;

        if (go == null)
          go = new GameObject(preferredGameObjectName);

        go.AddComponent(c);
      }

      return go;
    }

    public static T EnsureExistsInScene<T>(string preferredGameObjectName = null, GameObject onThisObject = null, params Type[] otherRequiredComponents) where T : Component {

      if (preferredGameObjectName == null)
        preferredGameObjectName = typeof(T).Name;

      T comp;
      comp = UnityEngine.Object.FindObjectOfType<T>();
      if (comp == null) {
        // T was not found in scene, create a new gameobject and add T, as well as other required components
        if (onThisObject == null)
          onThisObject = new GameObject(preferredGameObjectName);
        comp = onThisObject.AddComponent<T>();
        foreach (var add in otherRequiredComponents) {
          onThisObject.AddComponent(add);
        }
      } else {
        // Make sure existing found T has the indicated extra components as well.
        foreach (var add in otherRequiredComponents) {
          if (comp.GetComponent(add) == false)
            comp.gameObject.AddComponent(add);
        }
      }
      return comp;
    }

    /// <summary>
    /// Create a scene object with all of the supplied arguments and parameters applied.
    /// </summary>
    public static GameObject CreatePrimitive(
      PrimitiveType? primitive,
      string name,
      Vector3? position,
      Quaternion? rotation,
      Vector3? scale,
      Transform parent,
      Material material,
      params Type[] addComponents) {

      GameObject go;
      if (primitive.HasValue) {
        go = GameObject.CreatePrimitive(primitive.Value);

        go.name = name;

        if (material != null)
          go.GetComponent<Renderer>().material = material;

        foreach (var type in addComponents) {
          go.AddComponent(type);
        }

      } else {
        go = new GameObject(name, addComponents);
      }

      if (position.HasValue)
        go.transform.position = position.Value;

      if (rotation.HasValue)
        go.transform.rotation = rotation.Value;

      if (scale.HasValue)
        go.transform.localScale = scale.Value;

      if (parent)
        go.transform.parent = parent;

      return go;
    }

    internal static RunnerVisibilityNodes EnsureComponentHasVisibilityNode(this Component component) {
      var allExistingNodes = component.GetComponents<RunnerVisibilityNodes>();
      foreach (var existingNodes in allExistingNodes) {
        foreach (var comp in existingNodes.Components) {
          if (comp == component) {
            return existingNodes;
          }
        }
      }

      // Component is not represented yet. If there is a VisNodes already, use it. Otherwise make one.
      RunnerVisibilityNodes targetNodes = component.GetComponent<RunnerVisibilityNodes>();
      if (targetNodes == null) {
        targetNodes = component.gameObject.AddComponent<RunnerVisibilityNodes>();
      }

      // Add this component to the collection.
      int newArrayPos = targetNodes.Components.Length;
      Array.Resize(ref targetNodes.Components, newArrayPos + 1);
      targetNodes.Components[newArrayPos] = component;
      return targetNodes;
    }
  }
}

#endif
