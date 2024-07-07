namespace Quantum.Menu {
  /// <summary>
  /// Screen plugin are usually a UI features that is shared between multiple screens.
  /// The plugin must be registered at <see cref="QuantumMenuUIScreen.Plugins"/> and receieve Show and Hide callbacks.
  /// </summary>
  public class QuantumMenuScreenPlugin : QuantumMonoBehaviour {
    /// <summary>
    /// The parent screen is shown.
    /// </summary>
    /// <param name="screen">Parent screen</param>
    public virtual void Show(QuantumMenuUIScreen screen) {
    }

    /// <summary>
    /// The parent screen is hidden.
    /// </summary>
    /// <param name="screen">Parent screen</param>
    public virtual void Hide(QuantumMenuUIScreen screen) {
    }
  }
}
