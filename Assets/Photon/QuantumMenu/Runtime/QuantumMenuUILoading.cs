namespace Quantum.Menu {
#if QUANTUM_ENABLE_TEXTMESHPRO
  using Text = TMPro.TMP_Text;
#else 
  using Text = UnityEngine.UI.Text;
#endif
  using UnityEngine;

  /// <summary>
  /// The loading screen.
  /// </summary>
  public partial class QuantumMenuUILoading : QuantumMenuUIScreen {
    /// <summary>
    /// The disconnect button.
    /// </summary>
    [SerializeField] protected UnityEngine.UI.Button _disconnectButton;
    /// <summary>
    /// The loading screen status text.
    /// </summary>
    [SerializeField] protected Text _text;

    partial void AwakeUser();
    partial void InitUser();
    partial void ShowUser();
    partial void HideUser();

    /// <summary>
    /// The Unity awake method. Calls partial method <see cref="AwakeUser"/> to be implemented on the SDK side.
    /// </summary>
    public override void Awake() {
      base.Awake();
      AwakeUser();
    }

    /// <summary>
    /// The screen init method. Calls partial method <see cref="InitUser"/> to be implemented on the SDK side.
    /// </summary>
    public override void Init() {
      base.Init();
      InitUser();
    }

    /// <summary>
    /// The screen show method. Calls partial method <see cref="ShowUser"/> to be implemented on the SDK side.
    /// </summary>
    public override void Show() {
      base.Show();
      ShowUser();
    }

    /// <summary>
    /// The screen hide method. Calls partial method <see cref="HideUser"/> to be implemented on the SDK side.
    /// </summary>
    public override void Hide() {
      base.Hide();
      HideUser();
    }

    /// <summary>
    /// Update the text of the loading screen.
    /// </summary>
    /// <param name="text">Text</param>
    public void SetStatusText(string text) {
      if (_text != null) {
        _text.text = text;
      }
    }

    /// <summary>
    /// Is called when the <see cref="_disconnectButton"/> is pressed using SendMessage() from the UI object.
    /// </summary>
    protected virtual async void OnDisconnectPressed() {
      await Connection.DisconnectAsync(ConnectFailReason.UserRequest);
    }
  }
}
