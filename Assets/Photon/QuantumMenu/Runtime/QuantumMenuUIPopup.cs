namespace Quantum.Menu {
  using System.Threading.Tasks;
#if QUANTUM_ENABLE_TEXTMESHPRO
  using Text = TMPro.TMP_Text;
  using InputField = TMPro.TMP_InputField;
#else 
  using Text = UnityEngine.UI.Text;
  using InputField = UnityEngine.UI.InputField;
#endif
  using UnityEngine;
  using UnityEngine.UI;

  /// <summary>
  /// The popup screen handles notificaction.
  /// The screen has be <see cref="QuantumMenuUIScreen.IsModal"/> true.
  /// </summary>
  public partial class QuantumMenuUIPopup : QuantumMenuUIScreen {
    /// <summary>
    /// The text field for the message.
    /// </summary>
    [InlineHelp, SerializeField] protected Text _text;
    /// <summary>
    /// The text field for the header.
    /// </summary>
    [InlineHelp, SerializeField] protected Text _header;
    /// <summary>
    /// The okay button.
    /// </summary>
    [InlineHelp, SerializeField] protected Button _button;

    /// <summary>
    /// The completion source will be triggered when the screen has been hidden.
    /// </summary>
    protected TaskCompletionSource<bool> _taskCompletionSource;

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

      // Free the _taskCompletionSource before releasing the old one.
      var completionSource = _taskCompletionSource;
      _taskCompletionSource = null;
      completionSource?.TrySetResult(true);
    }

    /// <summary>
    /// Open the screen in overlay mode
    /// </summary>
    /// <param name="msg">Message</param>
    /// <param name="header">Header, can be null</param>
    public virtual void OpenPopup(string msg, string header) {
      _header.text = header;
      _text.text = msg;

      Show();
    }

    /// <summary>
    /// Open the screen and wait for it being hidden
    /// </summary>
    /// <param name="msg">Message</param>
    /// <param name="header">Header, can be null</param>
    /// <returns>When the screen is hidden.</returns>
    public virtual Task OpenPopupAsync(string msg, string header) {
      _taskCompletionSource?.TrySetResult(true);
      _taskCompletionSource = new TaskCompletionSource<bool>();

      OpenPopup(msg, header);

      return _taskCompletionSource.Task;
    }
  }
}
