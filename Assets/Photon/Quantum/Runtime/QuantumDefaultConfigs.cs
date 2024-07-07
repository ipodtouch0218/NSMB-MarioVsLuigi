namespace Quantum {
  using UnityEngine;

  /// <summary>
  /// This class represents a collection of Quantum config assets that are used when no explicit simulation config was assigned to a simulation (through RuntimeConfig).
  /// It's also implementing QuantumGlobalScriptableObject to have one instance globally accessible.
  /// </summary>
  [QuantumGlobalScriptableObject(DefaultPath)]

  [CreateAssetMenu(menuName = "Quantum/Configurations/DefaultConfigs", fileName = "QuantumDefaultConfigs", order = EditorDefines.AssetMenuPriorityConfigurations + 4)]
  public class QuantumDefaultConfigs : QuantumGlobalScriptableObject<QuantumDefaultConfigs> {
    /// <summary>
    /// The default location of the global QuantumDefaultConfigs asset.
    /// </summary>
    public const string DefaultPath = "Assets/QuantumUser/Resources/QuantumDefaultConfigs.asset";

    /// <summary>
    /// The simulation config.
    /// </summary>
    public SimulationConfig SimulationConfig;
    /// <summary>
    /// The default physics material assigned inside <see cref="SimulationConfig"/> to use if no explicit physics material was assigned to Quantum colliders.
    /// </summary>
    public PhysicsMaterial PhysicsMaterial;
    /// <summary>
    /// The default KCC2D config assigned inside <see cref="SimulationConfig"/>.
    /// </summary>
    public CharacterController2DConfig CharacterController2DConfig;
    /// <summary>
    /// The default KCC3D config assigned inside <see cref="SimulationConfig"/>.
    /// </summary>
    public CharacterController3DConfig CharacterController3DConfig;
    /// <summary>
    /// The default NavMeshAgent config assigned inside <see cref="SimulationConfig"/>.
    /// </summary>
    public NavMeshAgentConfig NavMeshAgentConfig;
    /// <summary>
    /// The default systems config to be used for <see cref="RuntimeConfig.SystemsConfig"/>.
    /// </summary>
    public SystemsConfig SystemsConfig;
  }
}