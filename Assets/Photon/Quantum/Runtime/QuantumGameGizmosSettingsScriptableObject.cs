namespace Quantum {
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// A scriptable object to store the Quantum game gizmos settings <see cref="QuantumGameGizmosSettings"/>.
  /// Global configs are marked with "QuantumDefaultGlobal" asset label and there should be only one instance of this asset tagged as such.
  /// </summary>
  [CreateAssetMenu(menuName = "Quantum/Configurations/GameGizmoSettings", fileName = "QuantumGameGizmoSettings",
    order = EditorDefines.AssetMenuPriorityConfigurations + 31)]
  [QuantumGlobalScriptableObject(DefaultPath)]
  public class QuantumGameGizmosSettingsScriptableObject : 
#if UNITY_EDITOR
    QuantumGlobalScriptableObject<QuantumGameGizmosSettingsScriptableObject>
#else
    QuantumScriptableObject
#endif
  {
    /// <summary>
    /// The default location where the initial asset is being created.
    /// </summary>
    public const string DefaultPath = "Assets/QuantumUser/Editor/QuantumGameGizmosSettings.asset";

    /// <summary>
    /// The global and default settings for Quantum gizmos.
    /// </summary>
    [DrawInline] public QuantumGameGizmosSettings Settings;

    /// <summary>
    /// Open the overlay for the Quantum gizmos.
    /// </summary>
    [EditorButton]
    public void OpenOverlay() {
#if UNITY_EDITOR
      // get scene view 
      var sceneView = SceneView.lastActiveSceneView;
      if (sceneView == null) {
        sceneView = SceneView.sceneViews.Count > 0 ? (SceneView) SceneView.sceneViews[0] : null;
      }
      
      if (sceneView == null) {
        Debug.LogError("No scene view found.");
        return;
      }
      
      // get overlay
      if(sceneView.TryGetOverlay(QuantumGameGizmosSettings.ID, out var overlay)) {
        overlay.Undock();
        overlay.displayed = true;
        overlay.collapsed = false;
      } else {
        Debug.LogError("No overlay found.");
      }
#endif
    }
  }
}