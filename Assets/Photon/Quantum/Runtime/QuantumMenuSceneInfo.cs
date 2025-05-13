namespace Quantum {
  using System.IO;
  using UnityEngine;

  /// <summary>
  /// Info asset for creating configurable selectable scenes in the Photon menu.
  /// This type was moved to the SDK to simplify adding scenes automatically (e.g. when installing another unity package).
  /// </summary>
  [CreateAssetMenu(menuName = "Quantum/Menu/Menu Scene Info")]
  public partial class QuantumMenuSceneInfo : QuantumScriptableObject {
    /// <summary>
    /// Displayed scene name.
    /// </summary>
    public string Name;
    /// <summary>
    /// Returns either <see cref="Name"/> or <see cref="SceneName"/>.
    /// </summary>
    public string NameOrSceneName => string.IsNullOrEmpty(Name) ? SceneName : Name;
    /// <summary>
    /// The path to the scene asset.
    /// </summary>
    [ScenePath] public string ScenePath;
    /// <summary>
    /// Gets the filename of the ScenePath to set as Unity scene to load during connection sequence.
    /// </summary>
    public string SceneName => ScenePath == null ? null : Path.GetFileNameWithoutExtension(ScenePath);
    /// <summary>
    /// The sprite displayed as scene preview in the scene selection UI.
    /// </summary>
    public Sprite Preview;
    /// <summary>
    /// Don't use this scene.
    /// </summary>
    public bool IsHidden;
  }
}
