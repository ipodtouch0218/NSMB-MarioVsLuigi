namespace Quantum.Menu {
  using System.Collections.Generic;
  using UnityEngine;

  /// <summary>
  /// Photon menu config file implements <see cref="QuantumMenuConfig"/>.
  /// Stores static options that affect parts of the menu behavior and selectable configurations.
  /// </summary>
  [ScriptHelp(BackColor = ScriptHeaderBackColor.Blue)]
  [CreateAssetMenu(menuName = "Quantum/Menu/Menu Config")]
  public partial class QuantumMenuConfig : QuantumScriptableObject {
    /// <summary>
    /// The maximum player count allowed for all game modes.
    /// </summary>
    [InlineHelp, SerializeField] protected int _maxPlayers = 6;
    /// <summary>
    /// Force 60 FPS during menu animations.
    /// </summary>
    [InlineHelp, SerializeField] protected bool _adaptFramerateForMobilePlatform = true;
    /// <summary>
    /// The available Photon AppVersions to be selectable by the user.
    /// An empty list will hide the related dropdown on the settings screen.
    /// </summary>
    [InlineHelp, SerializeField] protected List<string> _availableAppVersions = new List<string> { "1.0" };
    /// <summary>
    /// Static list of regions available in the settings.
    /// An empty entry symbolizes best region option.
    /// An empty list will hide the related dropdown on the settings screen.
    /// </summary>
    [InlineHelp, SerializeField] protected List<string> _availableRegions = new List<string> { "asia", "eu", "sa", "us" };
    /// <summary>
    /// Static list of scenes available in the scenes menu.
    /// An empty list will hide the related button in the main screen.
    /// PhotonMenuSceneInfo.Name = displayed name
    /// PhotonMenuSceneInfo.ScenePath = the actual Unity scene (must be included in BuildSettings)
    /// PhotonMenuSceneInfo.Preview = a sprite with a preview of the scene (screenshot) that is displayed in the main menu and scene selection screen (can be null)
    /// </summary>
    [InlineHelp, SerializeField] protected List<PhotonMenuSceneInfo> _availableScenes = new List<PhotonMenuSceneInfo>();
    /// <summary>
    /// The <see cref="QuantumMenuMachineId"/> ScriptableObject that stores local ids to use as an option in for AppVersion.
    /// Designed as a convenient development feature.
    /// Can be null.
    /// </summary>
    [InlineHelp, SerializeField] protected QuantumMenuMachineId _machineId;
    /// <summary>
    /// The <see cref="QuantumMenuPartyCodeGenerator"/> ScriptableObject that is required for party code generation.
    /// Also used to create random player names.
    /// </summary>
    [InlineHelp, SerializeField] protected QuantumMenuPartyCodeGenerator _codeGenerator;

    /// <summary>
    /// Return the available app versions.
    /// </summary>
    public List<string> AvailableAppVersions => _availableAppVersions;
    /// <summary>
    /// Returns the available regions.
    /// </summary>
    public List<string> AvailableRegions => _availableRegions;
    /// <summary>
    /// Returns the available scenes.
    /// </summary>
    public List<PhotonMenuSceneInfo> AvailableScenes => _availableScenes;
    /// <summary>
    /// Returns the max player count.
    /// </summary>
    public int MaxPlayerCount => _maxPlayers;
    /// <summary>
    /// Returns an id that should be unique to this machine.
    /// </summary>
    public virtual string MachineId => _machineId?.Id;
    /// <summary>
    /// Returns the code generator.
    /// </summary>
    public QuantumMenuPartyCodeGenerator CodeGenerator => _codeGenerator;
    /// <summary>
    /// Returns true if the framerate should be adapted for mobile platforms to force the menu animations to run at 60 FPS.
    /// </summary>
    public bool AdaptFramerateForMobilePlatform => _adaptFramerateForMobilePlatform;
  }
}
