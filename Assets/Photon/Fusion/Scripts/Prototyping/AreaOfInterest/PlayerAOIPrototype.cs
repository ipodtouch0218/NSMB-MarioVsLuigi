
using Fusion;
using UnityEngine;

/// <summary>
/// Prototyping component for Fusion. Updates the Player's AOI every tick to be a radius around this object.
/// </summary>
[ScriptHelp(BackColor = EditorHeaderBackColor.Steel)]
public class PlayerAOIPrototype : NetworkBehaviour {

  /// <summary>
  /// Enables the widget which shows the current AOI radius for this object in the scene window.
  /// </summary>
  [InlineHelp]
  [SerializeField]
  [MultiPropertyDrawersFix]
  protected bool DrawAreaOfInterestRadius;

  /// <summary>
  /// Radius around this GameObject that defines the Area Of Interest for the InputAuthority of the object.
  /// The InputAuthority player of this <see cref="NetworkObject"/>, 
  /// will receive updates for any other <see cref="NetworkObject"/> within this radius. 
  /// </summary>
  [InlineHelp]
  public float Radius = 32f;

  public override void FixedUpdateNetwork() {

    if (Runner.Topology == SimulationConfig.Topologies.ClientServer) {
      // Assign this object as an AOI region for the player with input authority.
      if (Object.InputAuthority.IsNone == false && Runner.IsServer) {
        Runner.AddPlayerAreaOfInterest(Object.InputAuthority, position: transform.position, Radius);
      }
    } else {
      // Assign this object as an AOI region for its State Authority player
      if (Object.StateAuthority.IsNone == false && Object.StateAuthority == Runner.LocalPlayer) {
        Runner.AddPlayerAreaOfInterest(Object.StateAuthority, position: transform.position, Radius);
      }
    }
  }

  private void OnDrawGizmos() {
    if (DrawAreaOfInterestRadius) {
      var baseColor = Gizmos.color;
      Gizmos.color = Color.white;
      Gizmos.DrawWireSphere(transform.position, Radius);
      Gizmos.color = baseColor;
    }
  }
}