namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The asset wraps an instance of <see cref="DeterministicSessionConfig"/> and makes it globally (in Unity) accessible as a default config.
  /// </summary>
  [CreateAssetMenu(menuName = "Quantum/Configurations/SessionConfig", order = EditorDefines.AssetMenuPriorityConfigurations + 2)]
  [QuantumGlobalScriptableObject(DefaultPath)]
  public class QuantumDeterministicSessionConfigAsset : QuantumGlobalScriptableObject<QuantumDeterministicSessionConfigAsset> {
    /// <summary>
    /// The default location of the global QuantumDeterministicSessionConfigAsset asset.
    /// </summary>
    public const string DefaultPath = "Assets/QuantumUser/Resources/SessionConfig.asset";
    
    /// <summary>
    /// The config instance.
    /// </summary>
    [DrawInline]
    public DeterministicSessionConfig Config;

    /// <summary>
    /// Return the default global config instance.
    /// </summary>
    public static DeterministicSessionConfig DefaultConfig => Global.Config;
  }
}