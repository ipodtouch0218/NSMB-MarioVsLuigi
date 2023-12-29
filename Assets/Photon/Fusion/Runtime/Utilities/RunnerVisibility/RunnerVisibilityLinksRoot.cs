
namespace Fusion {

  using UnityEngine;

  /// <summary>
  /// Flag component which indicates a NetworkObject has already been factored into a Runner's VisibilityNode list.
  /// </summary>
  [AddComponentMenu("")]
  internal class RunnerVisibilityLinksRoot : MonoBehaviour {
    private void Awake() {
      this.hideFlags = HideFlags.HideInInspector;
    }
  }
}
