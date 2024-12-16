namespace Quantum.Menu {
  using System;
  using System.Collections.Generic;
  using System.Threading.Tasks;
  using UnityEngine;
  using UnityEngine.Serialization;

  /// <summary>
  /// All screens are registed by this controller in the <see cref="_screens"/> list.
  /// Every screen gets a reference of this controller, the assigned <see cref="_config"/> and <see cref="Connection"/> wrapper.
  /// The first screen in the <see cref="_screens"/> list is the screen that is shown on app start.
  /// Controller is used to progress from one screen to another <see cref="Show{S}()"/>.
  /// E.g. Show&lt;QuantumMenuUILoading&gt;().
  /// When deriving a screen the base type will still be functionally to use for Get() and Show(). But only the derived type or the base are useable.
  /// </summary>
  public class QuantumMenuUIController : QuantumMonoBehaviour {
    /// <summary>
    /// The menu config.
    /// </summary>
    [InlineHelp, SerializeField] protected QuantumMenuConfig _config;
    /// <summary>
    /// The connection wrapper.
    /// </summary>
    [FormerlySerializedAs("_connection"), InlineHelp, SerializeField] public QuantumMenuConnectionBehaviour Connection;
    /// <summary>
    /// The list of screens. The first one is the default screen shown on start.
    /// </summary>
    [InlineHelp, SerializeField] protected QuantumMenuUIScreen[] _screens;

    /// <summary>
    /// A type to screen lookup to support <see cref="Get{S}()"/>
    /// </summary>
    protected Dictionary<Type, QuantumMenuUIScreen> _screenLookup;
    /// <summary>
    /// The popup handler is automatically set if present based on the interface <see cref="QuantumMenuUIPopup"/>.
    /// </summary>
    protected QuantumMenuUIPopup _popupHandler;
    /// <summary>
    /// The current active screen.
    /// </summary>
    protected QuantumMenuUIScreen _activeScreen;

    /// <summary>
    /// Current connection args. Shared by all screens.
    /// </summary>
    public QuantumMenuConnectArgs ConnectArgs;

    /// <summary>
    /// Unity awake method. Populates internal structures based on the <see cref="_screens"/> list.
    /// </summary>
    protected virtual void Awake() {
      _screenLookup = new Dictionary<Type, QuantumMenuUIScreen>();

      foreach (var screen in _screens) {
        screen.Config = _config;
        screen.Connection = Connection;
        screen.ConnectionArgs = ConnectArgs;
        screen.Controller = this;

        var t = screen.GetType();
        while (true) {
          _screenLookup.Add(t, screen);
          if (t.BaseType == null || typeof(QuantumMenuUIScreen).IsAssignableFrom(t) == false || t.BaseType == typeof(QuantumMenuUIScreen)) {
            break;
          }

          t = t.BaseType;
        }

        if (screen is QuantumMenuUIPopup popupHandler) {
          _popupHandler = popupHandler;
        }
      }

      foreach (var screen in _screens) {
        screen.Init();
      }
    }

    /// <summary>
    /// The Unity start method to enable the default screen.
    /// </summary>
    protected virtual void Start() {
      if (_screens != null && _screens.Length > 0) {
        // First screen is displayed by default
        _screens[0].Show();
        _activeScreen = _screens[0];
      }
    }

    /// <summary>
    /// Show a screen will automatically disable the current active screen and call animations.
    /// </summary>
    /// <typeparam name="S">Screen type</typeparam>
    public virtual void Show<S>() where S : QuantumMenuUIScreen {
      if (_screenLookup.TryGetValue(typeof(S), out var result)) {
        if (result.IsModal == false && _activeScreen != result && _activeScreen) {
          _activeScreen.Hide();
        }
        if (_activeScreen != result) {
          result.Show();
        }
        if (result.IsModal == false) {
          _activeScreen = result;
        }
      } else {
        Debug.LogError($"Show() - Screen type '{typeof(S).Name}' not found");
      }
    }

    /// <summary>
    /// Get a screen based on type.
    /// </summary>
    /// <typeparam name="S">Screen type</typeparam>
    /// <returns>Screen object</returns>
    public virtual S Get<S>() where S : QuantumMenuUIScreen {
      if (_screenLookup.TryGetValue(typeof(S), out var result)) {
        return result as S;
      } else {
        Debug.LogError($"Show() - Screen type '{typeof(S).Name}' not found");
        return null;
      }
    }

    /// <summary>
    /// Show the popup/notification.
    /// </summary>
    /// <param name="msg">Popup message</param>
    /// <param name="header">Popup header</param>
    public void Popup(string msg, string header = default) {
      if (_popupHandler == null) {
        Debug.LogError("Popup() - no popup handler found");
      } else {
        _popupHandler.OpenPopup(msg, header);
      }
    }

    /// <summary>
    /// Show the popup but wait until it hides.
    /// </summary>
    /// <param name="msg">Popup message</param>
    /// <param name="header">Popup header</param>
    /// <returns>When the user clicked okay.</returns>
    public Task PopupAsync(string msg, string header = default) {
      if (_popupHandler == null) {
        Debug.LogError("Popup() - no popup handler found");
        return Task.CompletedTask;
      } else {
        return _popupHandler.OpenPopupAsync(msg, header);
      }
    }
    
    /// <summary>
    /// Default connection error handling is reused in a couple places.
    /// </summary>
    /// <param name="result">Connect result</param>
    /// <param name="controller">UI Controller</param>
    /// <returns>When handling is completed</returns>
    public virtual async Task HandleConnectionResult(ConnectResult result, QuantumMenuUIController controller) {
      if (result.CustomResultHandling) {
        return;
      } 
      
      if (result.Success) {
        controller.Show<QuantumMenuUIGameplay>();
      } else if (result.FailReason != ConnectFailReason.ApplicationQuit) {
        var popup = controller.PopupAsync(result.DebugMessage, "Connection Failed");
        if (result.WaitForCleanup != null) {
          await Task.WhenAll(result.WaitForCleanup, popup);
        } else {
          await popup;
        }
        controller.Show<QuantumMenuUIMain>();
      }
    }
  }
}
