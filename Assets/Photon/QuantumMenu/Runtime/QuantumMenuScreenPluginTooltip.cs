namespace Quantum.Menu {
  using UnityEngine;
  using UnityEngine.UI;

  /// <summary>
  /// The tooltip plugin can be used to add tooltip text to a button.
  /// The <see cref="QuantumMenuUIPopup"/> screen will be shown.
  /// </summary>
  [RequireComponent(typeof(Button))]
  public class QuantumMenuScreenPluginTooltip : QuantumMenuScreenPlugin {
    /// <summary>
    /// The header text of the tooltip popup. Can be null.
    /// </summary>
    [InlineHelp, SerializeField] protected string _header;
    /// <summary>
    /// The tooltip text.
    /// </summary>
    [InlineHelp, SerializeField, TextArea] protected string _tooltip;
    /// <summary>
    /// The button that activates the tooltip popup.
    /// </summary>
    [InlineHelp, SerializeField] protected Button _button;

    private QuantumMenuUIController _controller;

    /// <summary>
    /// Unity awake method to add the tooltip listener to the button.
    /// </summary>
    public virtual void Awake() {
      _button.onClick.AddListener(() => _controller.Popup(_tooltip, _header));
    }

    /// <summary>
    /// The parent screen is shown. Cache the UI controller.
    /// </summary>
    /// <param name="screen">The parent screen</param>
    public override void Show(QuantumMenuUIScreen screen) {
      base.Show(screen);

      _controller = screen.Controller;
    }

    /// <summary>
    /// The parent screen is hidden. Clear the cached controller.
    /// </summary>
    /// <param name="screen">Parent screen</param>
    public override void Hide(QuantumMenuUIScreen screen) {
      base.Hide(screen);

      _controller = null;
    }
  }
}
