
using System.Collections.Generic;
using Fusion.Analyzer;
using UnityEngine;

namespace Fusion {
  
  #if UNITY_EDITOR
  using UnityEditor;
  #endif


  /// <summary>
  /// Automatically adds a <see cref="RunnerVisibilityLink"/> for each indicated component. 
  /// These indicated components will be limited to no more than one enabled instance when running in Multi-Peer mode.
  /// </summary>
  [AddComponentMenu("Fusion/Enable On Single Runner")]
  public class EnableOnSingleRunner : Fusion.Behaviour {

    /// <summary>
    /// If more than one runner instance is visible, this indicates which peer's clone of this entity should be visible.
    /// </summary>
    [InlineHelp]
    [SerializeField]
#pragma warning disable IDE0044 // Add readonly modifier
    public RunnerVisibilityLink.PreferredRunners PreferredRunner;
#pragma warning restore IDE0044 // Add readonly modifier

    /// <summary>
    /// Collection of components that will be marked for Multi-Peer mode as objects that should only have one enabled instance.
    /// </summary>
    [InlineHelp]
    public UnityEngine.Component[] Components = new Component[0];

    /// <summary>
    /// Prefix for the GUIDs of <see cref="RunnerVisibilityLink"/> components which are added at runtime.
    /// </summary>
    [HideInInspector]
    [SerializeField]
    private string _guid = System.Guid.NewGuid().ToString().Substring(0, 19);

    /// <summary>
    /// At runtime startup, this adds a <see cref="RunnerVisibilityLink"/> for each component reference to this GameObject.
    /// </summary>
    internal void AddNodes() {
      for(int i = 0, cnt = Components.Length; i <  cnt; ++i) {
        var node = gameObject.AddComponent<RunnerVisibilityLink>();
        node.Guid = _guid + i;
        node.Component = Components[i];
        node.PreferredRunner = PreferredRunner;
      }
    }

    /// <summary>
    /// Finds visual/audio components on this GameObject, and adds them to the Components collection.
    /// </summary>
    [EditorButton("Find on GameObject", EditorButtonVisibility.EditMode, dirtyObject: true)]
    public void FindRecognizedTypes() {
      Components = FindRecognizedComponentsOnGameObject(gameObject);
    }

    /// <summary>
    /// Finds visual/audio nested components on this GameObject and its children, and adds them to the Components collection.
    /// </summary>
    [EditorButton("Find in Nested Children", EditorButtonVisibility.EditMode, dirtyObject: true)]
    public void FindNestedRecognizedTypes() {
      Components = FindRecognizedNestedComponents(gameObject);
    }

    [StaticField(StaticFieldResetMode.None)]
    private static readonly List<Component> reusableComponentsList = new List<UnityEngine.Component>();
    [StaticField(StaticFieldResetMode.None)]
    private static readonly List<Component> reusableComponentsList2 = new List<UnityEngine.Component>();

    private static Component[] FindRecognizedComponentsOnGameObject(GameObject go) {
      try {
        go.GetComponents(reusableComponentsList);
        reusableComponentsList2.Clear();
        foreach (var comp in reusableComponentsList) {
          var type = comp.GetType();
          if (type.IsRecognizedByRunnerVisibility()) {
            reusableComponentsList2.Add(comp);
          }
        }
        return reusableComponentsList2.ToArray();
      } finally {
        reusableComponentsList.Clear();
        reusableComponentsList2.Clear();
      }
    }

    private static Component[] FindRecognizedNestedComponents(GameObject go) {
      try {
        go.transform.GetNestedComponentsInChildren<UnityEngine.Component, NetworkObject>(reusableComponentsList, true);
        reusableComponentsList2.Clear();
        foreach (var comp in reusableComponentsList) {
          var type = comp.GetType();
          if (type.IsRecognizedByRunnerVisibility()) {
            reusableComponentsList2.Add(comp);
          }
        }
        return reusableComponentsList2.ToArray();
      } finally {
        reusableComponentsList.Clear();
        reusableComponentsList2.Clear();
      }
    }
  }
}

