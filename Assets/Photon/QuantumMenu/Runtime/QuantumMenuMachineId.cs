namespace Quantum.Menu {
  /// <summary>
  /// A scriptable object that has an id used by the QuantumMenu as AppVersion.
  /// Mostly a development feature to ensure to only meet compatible clients in the Photon matchmaking.
  /// </summary>
  //[CreateAssetMenu(menuName = "Photon/Menu/MachineId")]
  [ScriptHelp(BackColor = ScriptHeaderBackColor.Blue)]
  public class QuantumMenuMachineId : QuantumScriptableObject {
    /// <summary>
    /// An id that should be unique to this machine, used by the QuantumMenu as AppVersion.
    /// An explicit asset importer is used to create local ids during import (see QuantumMenuMachineIdImporter).
    /// </summary>
    [InlineHelp] public string Id;
  }
}
