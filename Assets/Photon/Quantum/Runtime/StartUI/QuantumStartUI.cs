namespace Quantum {
  using UnityEngine;
#if QUANTUM_ENABLE_TEXTMESHPRO
  using Text = TMPro.TMP_Text;
  using InputField = TMPro.TMP_InputField;
  using Dropdown = TMPro.TMP_Dropdown;
#else
  using Text = UnityEngine.UI.Text;
  using InputField = UnityEngine.UI.InputField;
  using Dropdown = UnityEngine.UI.Dropdown;
#endif
  using System;
  using System.Collections.Generic;
  using UnityEngine.SceneManagement;
  using UnityEngine.Serialization;
  using System.Linq;

  /// <summary>
  /// A simple menu to utilize the most common Photon connection and game start modes.
  /// </summary>
  //[RequireComponent(typeof(QuantumStartUIConnectionBase))]
  public class QuantumStartUI : QuantumMonoBehaviour {
    /// <summary>
    /// Cached Hide animation hash.
    /// </summary>
    protected static readonly int AnimationHashHide = Animator.StringToHash("Hide");
    /// <summary>
    /// Cached Show animation hash.
    /// </summary>
    protected static readonly int AnimationHashShow = Animator.StringToHash("Show");

    /// <summary>
    /// Toggle <see cref="UnityEngine.Cursor"/> on and off when enabled and disabling the menu.
    /// </summary>
    [Header("Menu Configuration")]
    [InlineHelp] public bool ToggleUnityCursor;
    /// <summary>
    /// Force enabled <see cref="Application.runInBackground"/> to improve the online stability in background mode.
    /// Works on all platforms, but mobile and WebGL games should consider what behavior is best.
    /// </summary>
    [InlineHelp] public bool ForceRunInBackground = true;
    /// <summary>
    /// Reload this scene after shutting down the game.
    /// </summary>
    [InlineHelp] public bool ReloadSceneAfterShutdown = true;
    /// <summary>
    /// Disable the menu when this scene is loaded from a different scene (e.g. another online menu).
    /// </summary>
    [InlineHelp] public bool DisableAfterLoadingFromDifferentScene;
    /// <summary>
    /// The ping threshold between signal strength icons.
    /// </summary>
    [InlineHelp] public int PingDelta = 81;
    /// <summary>
    /// The UI bindings for the menu.
    /// </summary>
    [Header("UI Bindings")]
    [FormerlySerializedAs("Config")]
    [InlineHelp, SerializeField] protected UIBindings UI;
    /// <summary>
    /// A list of UI elements that are set non-interactable during starting process, populated internally.
    /// </summary>
    protected List<UnityEngine.UI.Selectable> ToggleInputGroup;
    /// <summary>
    /// The connection implementation that handles the connection to the Photon server and game start.
    /// Will be tried to retrieved during <see cref="Awake"/>.
    /// </summary>
    protected QuantumStartUIConnectionBase Connection;
    /// <summary>
    /// The animator controller component. The menu also works without an animator.
    /// </summary>
    protected Animator Animator;
    /// <summary>
    /// Is set during the async shutdown process to prevent multiple shutdown calls.
    /// </summary>
    protected State CurrentState;
    /// <summary>
    /// The currently selected tab.
    /// </summary>
    protected Tab CurrentTab;

    /// <summary>
    /// Simple menu state machine to prevent multiple start and shutdown calls.
    /// </summary>
    protected enum State {
      /// <summary>
      /// Default state, no connection or game started.
      /// </summary>
      Idle,
      /// <summary>
      /// Game is connecting and starting.
      /// </summary>
      Starting,
      /// <summary>
      /// Game is running.
      /// </summary>
      Running,
      /// <summary>
      /// Game is shutting down.
      /// </summary>
      ShuttingDown
    }

    /// <summary>
    /// Available tabs are hard-coded.
    /// </summary>
    protected enum Tab {
      Online,
      Local,
      Settings, // Not used
      Custom    // Not used
    }

    #region UI Bindings Subclass

    /// <summary>
    /// Includes all ui element bindings for the menu.
    /// </summary>
    [Serializable]
    protected class UIBindings {
      [Header("Elements")]
      [FormerlySerializedAs("ButtonToggleMenu")]
      [InlineHelp] public UnityEngine.UI.Button MenuButton;
      [FormerlySerializedAs("ButtonStartGame")]
      [InlineHelp] public UnityEngine.UI.Button StartButton;
      [FormerlySerializedAs("ButtonQuit")]
      [InlineHelp] public UnityEngine.UI.Button QuitButton;
      [FormerlySerializedAs("ButtonDisconnect")]
      [InlineHelp] public UnityEngine.UI.Button DisconnectButton;
      [FormerlySerializedAs("ButtonPopup")]
      [InlineHelp] public UnityEngine.UI.Button PopupButton;
      [FormerlySerializedAs("ButtonCopyRoomName")]
      [InlineHelp] public UnityEngine.UI.Button CopyRoomButton;
      [FormerlySerializedAs("ButtonFooterLinkTemplate")]
      [InlineHelp] public UnityEngine.UI.Button FooterLinkButtonTemplate;
      [FormerlySerializedAs("DropdownRegion")]
      [InlineHelp] public QuantumStartUIRegionDropdown RegionDropdown;
      [FormerlySerializedAs("DropdownCharacter")]
      [InlineHelp] public Dropdown CharacterDropdown;
      [FormerlySerializedAs("TextStatus")]
      [InlineHelp] public Text StatusText;
      [FormerlySerializedAs("TextPopup")]
      [InlineHelp] public Text PopupText;
      [FormerlySerializedAs("TextSignal")]
      [InlineHelp] public Text SignalText;
      [FormerlySerializedAs("InputPlayerName")]
      [InlineHelp] public InputField PlayerNameInput;
      [FormerlySerializedAs("InputRoomName")]
      [InlineHelp] public InputField RoomNameInput;
      [FormerlySerializedAs("InputPrivateRoom")]
      [InlineHelp] public UnityEngine.UI.Toggle PrivateRoomInput;
      [FormerlySerializedAs("InputMute")]
      [InlineHelp] public UnityEngine.UI.Toggle MuteInput;
      /// <summary>
      /// Maximum four tabs are supported (<see cref="Tab"/>. Array must have size of four.
      /// </summary>
      [FormerlySerializedAs("InputTabs")]
      [InlineHelp] public UnityEngine.UI.Toggle[] TabInput;
      [FormerlySerializedAs("InputAppVersion")]
      [InlineHelp] public InputField AppVersionInput;
      /// <summary>
      /// The list of signal strength icons to toggle based on the ping value.
      /// </summary>
      [FormerlySerializedAs("ButtonToggleMenu")]
      [InlineHelp] public List<GameObject> SignalIcons;
      /// <summary>
      /// The menu panel canvas group to toggle and set interactable state.
      /// </summary>
      [Header("Toggles")]
      [FormerlySerializedAs("TogglePanel")]
      [InlineHelp] public CanvasGroup PanelGroup;
      /// <summary>
      /// Toggle the tabs section visibility. 
      /// Set to <see langword="null"/> to never use.
      /// </summary>
      [FormerlySerializedAs("ToggleTabs")]
      [InlineHelp] public GameObject TabGroup;
      /// <summary>
      /// Toggle the footer section visibility.
      /// </summary>
      [FormerlySerializedAs("ToggleFooter")]
      [InlineHelp] public GameObject FooterGroup;
      /// <summary>
      /// The game object to toggle the play buttons visibility.
      /// </summary>
      [FormerlySerializedAs("ToggleStartGameButton")]
      [InlineHelp] public GameObject StartButtonGroup;
      /// <summary>
      /// The game object to toggle the disconnect button visibility.
      /// </summary>
      [FormerlySerializedAs("ToggleDisconnectButton")]
      [InlineHelp] public GameObject DisconnectButtonGroup;
      /// <summary>
      /// The game object to toggle the quit button visibility.
      /// </summary>
      [FormerlySerializedAs("ToggleQuitButton")]
      [InlineHelp] public GameObject QuitButtonGroup;
      /// <summary>
      /// The toggle the menu button visibility.
      /// Set to <see langword="null"/> to never use.
      /// </summary>
      [FormerlySerializedAs("MenuButtonGroup")]
      [InlineHelp] public GameObject MenuButtonGroup;
      /// <summary>
      /// The game object to toggle the popup screen visibility.
      /// </summary>
      [FormerlySerializedAs("TogglePopup")]
      [InlineHelp] public GameObject PopupGroup;
      /// <summary>
      /// The game object to toggle the signal strength and ping display.
      /// Set to <see langword="null"/> to never use.
      /// </summary>
      [FormerlySerializedAs("ToggleSignal")]
      [InlineHelp] public GameObject SignalGroup;
      /// <summary>
      /// The game object to toggle fps display.
      /// </summary>
      [FormerlySerializedAs("ToggleFps")]
      [InlineHelp] public GameObject FpsGroup;
      /// <summary>
      /// Should be null if region dropdown is disabled completely.
      /// Set to <see langword="null"/> to never use.
      /// </summary>
      [FormerlySerializedAs("ToggleRegionDropdown")]
      [InlineHelp] public GameObject RegionDropdownGroup;
      /// <summary>
      /// The game object to toggle the room name input section.
      /// Set to <see langword="null"/> to never use.
      /// </summary>
      [FormerlySerializedAs("ToggleRoomInput")]
      [InlineHelp] public GameObject RoomInputGroup;
      /// <summary>
      /// The game object to toggle the private room checkbox.
      /// Set to <see langword="null"/> to never use.
      /// </summary>
      [FormerlySerializedAs("ToggleRoomPrivateToggle")]
      [InlineHelp] public GameObject RoomPrivateToggleGroup;
      /// <summary>
      /// The game object toggle the app version field visibility.
      /// Set to <see langword="null"/> to never use.
      /// </summary>
      [FormerlySerializedAs("ToggleAppVersion")]
      [InlineHelp] public GameObject AppVersionGroup;
      /// <summary>
      /// Toggle the status text visibility.
      /// Set to <see langword="null"/> to never use.
      /// </summary>
      [FormerlySerializedAs("ToggleStatus")]
      [InlineHelp] public GameObject StatusGroup;
    }

    #endregion

    System.Threading.Tasks.TaskCompletionSource<bool> _popupTaskCompletionSource;

    /// <summary>
    /// Copy the given text to the system clipboard.
    /// Has a different implementation in WebGL builds, where it uses a JavaScript function to copy to clipboard.
    /// </summary>
    /// <param name="text"></param>
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    protected static extern void PhotonStartUICopyToClipboard(string text);
#else
    protected static void PhotonStartUICopyToClipboard(string text) => GUIUtility.systemCopyBuffer = text;
#endif

    /// <summary>
    /// Load and save the name player name in Payer Prefs.
    /// </summary>
    protected string PlayerName {
      get {
        var playerName = PlayerPrefs.GetString("Photon.StartUI.PlayerName", string.Empty);
        if (string.IsNullOrEmpty(playerName)) {
          playerName = $"Player{UnityEngine.Random.Range(1000, 9999)}";
        }
        return playerName;
      }
      set => PlayerPrefs.SetString("Photon.StartUI.PlayerName", value);
    }

    /// <summary>
    /// Load and save the selected region in Player Prefs.
    /// </summary>
    protected string Region {
      get => PlayerPrefs.GetString("Photon.StartUI.RegionName", string.Empty);
      set => PlayerPrefs.SetString("Photon.StartUI.RegionName", value);
    }

    /// <summary>
    /// Load and save the muted state in Player Prefs.
    /// </summary>
    protected bool IsMuted {
      get => PlayerPrefs.GetInt("Photon.StartUI.IsMuted", 0) == 1;
      set => PlayerPrefs.SetInt("Photon.StartUI.IsMuted", value ? 1 : 0);
    }

    /// <summary>
    /// Multiplayer Play Mode command execution.
    /// Allows to start Mppm instances of the menu simultaneously and connect to the same room.
    /// </summary>
    /// <param name="command">Mppm command</param>
    public virtual async System.Threading.Tasks.Task TryExecuteMppmCommand(QuantumMppmCommand command) {
      if (command is QuantumStartUIMppmConnectCommand connectCommand) {
        if (CurrentState == State.Idle) {
          UI.RegionDropdown.SelectValue(connectCommand.Region, addIfNotFound: true);
          UI.RoomNameInput.text = connectCommand.RoomName;
          await StartGameAsync(true, PlayerName + UnityEngine.Random.Range(1000, 9999), connectCommand.RoomName, connectCommand.Region);
        }
      } else if (command is QuantumStartUIMppmDisconnectCommand disconnectCommand) {
        if (CurrentState == State.Idle) {
          await ShutdownGameAsync();
        }
      }
    }

    /// <summary>
    /// Set the visibility of the panel and play show and hide animations.
    /// </summary>
    /// <param name="isShowing">Menu main panel visibility</param>
    /// <param name="normalizedAnimationTime">The time offset between zero and one forwarded to the show animation.</param>
    public virtual void SetPanelVisibility(bool isShowing, float normalizedAnimationTime = 0.0f) {
      if (Animator != null) {
        Animator.Play(isShowing ? AnimationHashShow : AnimationHashHide, 0, normalizedAnimationTime);
      } else {
        UI.PanelGroup.gameObject.SetActive(isShowing);
      }

      if (UI.MenuButtonGroup != null && CurrentState == State.Running) {
        UI.MenuButtonGroup.transform.transform.Find("Open").gameObject.SetActive(isShowing == false);
        UI.MenuButtonGroup.transform.transform.Find("Close").gameObject.SetActive(isShowing);
      }
    }

    /// <summary>
    /// Unity Awake() method to register button listeners and get components.
    /// </summary>
    protected virtual void Awake() {
      Connection = Connection != null ? Connection : GetComponent<QuantumStartUIConnectionBase>();
      Animator = Animator != null ? Animator : GetComponent<Animator>();

      ToggleInputGroup = new List<UnityEngine.UI.Selectable> {
        UI.PlayerNameInput,
        UI.RoomNameInput,
        UI.CharacterDropdown,
        UI.RegionDropdown,
        UI.PrivateRoomInput,
      };
      ToggleInputGroup.AddRange(UI.TabInput.Where(t => t != null));
      
      UI.MenuButton.onClick.AddListener(OnTogglePressed);
      UI.StartButton.onClick.AddListener(() => OnPlayPressed());
      UI.QuitButton.onClick.AddListener(OnQuitPressed);
      UI.DisconnectButton.onClick.AddListener(OnDisconnectPressed);
      UI.PopupButton.onClick.AddListener(OnPopupPressed);
      UI.CopyRoomButton.onClick.AddListener(OnCopyRoomNamePressed);
      UI.MuteInput.onValueChanged.AddListener(OnMuteValueChanged);
      UI.RegionDropdown.OnFetchingStart += () => UI.PanelGroup.interactable = false;
      UI.RegionDropdown.OnFetchingEnd += () => UI.PanelGroup.interactable = true;

      if (UI.TabInput[(int)Tab.Online]) UI.TabInput[(int)Tab.Online].onValueChanged.AddListener(isOn => { if (isOn) OnTabSelected(Tab.Online); });
      if (UI.TabInput[(int)Tab.Local]) UI.TabInput[(int)Tab.Local].onValueChanged.AddListener(isOn => { if (isOn) OnTabSelected(Tab.Local); });
      if (UI.TabInput[(int)Tab.Settings]) UI.TabInput[(int)Tab.Settings].onValueChanged.AddListener(isOn => { if (isOn) OnTabSelected(Tab.Settings); });
      if (UI.TabInput[(int)Tab.Custom]) UI.TabInput[(int)Tab.Custom].onValueChanged.AddListener(isOn => { if (isOn) OnTabSelected(Tab.Custom); });

      OnMuteValueChanged(IsMuted);

      if (DisableAfterLoadingFromDifferentScene && SceneManager.sceneCount > 1) {
        gameObject.SetActive(false);
      }
    }

    /// <summary>
    /// Unity OnEnable() method to initialize the menu UI elements and set the initial state.
    /// </summary>
    protected virtual void OnEnable() {
      UI.PlayerNameInput.text = PlayerName;
      UI.StatusText.text = null;
      if (UI.StatusGroup) UI.StatusGroup.SetActive(false);

      if (UI.TabInput[(int)Tab.Online]) UI.TabInput[(int)Tab.Online].isOn = true;
      if (UI.TabInput[(int)Tab.Local]) UI.TabInput[(int)Tab.Local].isOn = false;
      if (UI.TabInput[(int)Tab.Settings]) UI.TabInput[(int)Tab.Settings].isOn = false;
      if (UI.TabInput[(int)Tab.Custom]) UI.TabInput[(int)Tab.Custom].isOn = false;

      UI.DisconnectButtonGroup.SetActive(false);
      if (UI.SignalGroup != null) UI.SignalGroup.SetActive(false);
      if (UI.FpsGroup != null) UI.FpsGroup.SetActive(false);
      if (UI.MenuButtonGroup != null) UI.MenuButtonGroup.SetActive(false);
      UI.CopyRoomButton.gameObject.SetActive(false);

      if (string.IsNullOrEmpty(Region)) {
        UI.RegionDropdown.SelectValue("Best Region", addIfNotFound: true);
      } else {
        UI.RegionDropdown.SelectValue(Region, addIfNotFound: true);
      }

      SetPanelVisibility(true, 1.0f);

      if (ForceRunInBackground) {
        Application.runInBackground = true;
      }

#if UNITY_EDITOR
      if (UI.QuitButtonGroup) UI.QuitButtonGroup.SetActive(false);
#endif
    }

    /// <summary>
    /// Unity Update() method updates a few things by polling.
    /// </summary>
    protected virtual void Update() {
      switch (CurrentState) {
        case State.Running: {
            // Toggle unity cursor when menu is show and hidden (first person game e.g.)
            if (ToggleUnityCursor) {
              if (UI.PanelGroup.gameObject.activeSelf) {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
              } else {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
              }
            }

            // Toggle the menu when Escape is pressed.
#if ENABLE_INPUT_SYSTEM && QUANTUM_ENABLE_INPUTSYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame ||
                UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.startButton.wasPressedThisFrame) {
              OnTogglePressed();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) {
              OnTogglePressed();
            }
#endif

            // Update connection "signal strength" and ping
            if (UI.SignalGroup != null && UI.SignalGroup.activeSelf) {
              var activeIndex = 0;
              if (Connection.Ping > 0) { 
                activeIndex = Math.Min((Connection.Ping / PingDelta) + 1, UI.SignalIcons.Count - 1);
              }
              for (int i = 0; i < UI.SignalIcons.Count; i++) {
                UI.SignalIcons[i].SetActive(i == activeIndex);
              }

            }
            if (UI.FpsGroup != null && UI.FpsGroup.activeSelf) {
              UI.SignalText.text = $"{Connection.Ping}";
            }
          }
          break;

        case State.Idle: {
            // Show and enable the private room toggle.
            if (UI.RoomPrivateToggleGroup != null && UI.PrivateRoomInput.interactable) {
              if (UI.RoomNameInput.text.Length > 0 && UI.RoomPrivateToggleGroup.activeSelf == false) {
                UI.PrivateRoomInput.isOn = true;
              }
              UI.RoomPrivateToggleGroup.SetActive(UI.RoomNameInput.text.Length > 0);
            }
            break;
          }
      }
    }

    /// <summary>
    /// Handling commencing the connection and game start.
    /// Mostly UI boilerplate, enabling and disabling UI elements, setting status text, etc.
    /// Calls <see cref="QuantumStartUIConnectionBase.ConnectAsync(QuantumStartUIConnectionBase.StartParameter)"/> to start the connection and game.
    /// </summary>
    /// <param name="isOnline">Non-online start does not require a connection.</param>
    /// <param name="playerName">Player name to use for the connection.</param>
    /// <param name="roomName">Room name, can be null.</param>
    /// <param name="region">Region to use.</param>
    protected virtual async System.Threading.Tasks.Task StartGameAsync(bool isOnline, string playerName, string roomName, string region) {
      if (CurrentState != State.Idle) {
        return;
      }

      CurrentState = State.Starting;

      if (UI.StatusGroup) UI.StatusGroup.SetActive(true);
      UI.StatusText.text = isOnline ? "Connecting.." : "Starting..";
      UI.StartButtonGroup.SetActive(false);
      UI.DisconnectButtonGroup.SetActive(true);
      foreach (var s in ToggleInputGroup) {
        s.interactable = false;}

      var isVisible = (UI.PrivateRoomInput.interactable && UI.PrivateRoomInput.isOn) == false;

      try {
        Assert.Always(Connection != null, $"{nameof(QuantumStartUIConnectionBase)} component is missing");
        await Connection.ConnectAsync(new QuantumStartUIConnectionBase.StartParameter {
          IsOnline = isOnline,
          IsVisible = isVisible,
          PlayerName = playerName,
          RoomName = UI.RoomInputGroup.activeInHierarchy ? string.IsNullOrEmpty(roomName) ? null : roomName : null,
          Region = UI.RegionDropdownGroup.activeInHierarchy ? string.IsNullOrEmpty(region) || Region.Equals("Best Region") ? null : region : null,
          CharacterName = UI.CharacterDropdown.gameObject.activeInHierarchy ? UI.CharacterDropdown.options[UI.CharacterDropdown.value].text : null,
          OnConnectionError = OnConnectionError
        });
      } catch (Exception e) {
        // Only process and show errors if the menu is still connecting.
        if (CurrentState == State.Starting) {
          Debug.LogException(e);
#if UNITY_EDITOR
          if (UnityEditor.EditorApplication.isPlaying == false) {
            return;
          }
#endif
          await ShowPopupAsync(e.Message);
          await ShutdownGameAsync();
        }
        return;
      }

      UI.StatusText.text = "";
      UI.RoomNameInput.text = Connection.RoomName;
      UI.AppVersionInput.text = Connection.AppVersion;
      UI.RegionDropdown.SelectValue(Connection.Region, addIfNotFound: false);
      UI.CopyRoomButton.gameObject.SetActive(true);
      if (UI.StatusGroup) UI.StatusGroup.SetActive(false);
      if (UI.MenuButtonGroup) UI.MenuButtonGroup.SetActive(true);
      if (UI.RegionDropdownGroup) UI.RegionDropdownGroup.SetActive(isOnline);
      if (UI.RoomInputGroup) UI.RoomInputGroup.SetActive(isOnline);
      if (UI.RoomPrivateToggleGroup) UI.RoomPrivateToggleGroup.SetActive(false);
      if (UI.AppVersionGroup) UI.AppVersionGroup.SetActive(string.IsNullOrEmpty(Connection.AppVersion) == false);
      if (UI.SignalGroup) UI.SignalGroup.SetActive(isOnline);
      if (UI.TabGroup) UI.TabGroup.SetActive(false);

      CurrentState = State.Running;

      SetPanelVisibility(false);
    }

    /// <summary>
    /// Shutdown the game and disconnect from the server.
    /// Will ignore subsequent calls until the shutdown is complete.
    /// </summary>
    protected virtual async System.Threading.Tasks.Task ShutdownGameAsync() {
      if (CurrentState == State.ShuttingDown) {
        return;
      }

      UI.PanelGroup.interactable = false;
      CurrentState = State.ShuttingDown;
      UI.DisconnectButton.interactable = false;
      UI.StatusText.text = "Disconnecting...";
      if (UI.StatusGroup) UI.StatusGroup.SetActive(true);

      try {
        await Connection.DisconnectAsync();
      } catch (Exception e) {
        Debug.LogException(e);
      }

      CurrentState = State.Idle;

      if (ReloadSceneAfterShutdown) {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
      } else {
        UI.StatusText.text = "";
        UI.RoomNameInput.text = "";
        UI.PanelGroup.interactable = true;
        UI.DisconnectButton.interactable = true;
        foreach (var s in ToggleInputGroup) {
          s.interactable = true;
        }

        UI.RegionDropdown.SelectValue(Region, addIfNotFound: false);
        UI.CopyRoomButton.gameObject.SetActive(false);
        UI.DisconnectButtonGroup.SetActive(false);
        UI.StartButtonGroup.SetActive(true);
        if (UI.MenuButtonGroup) UI.MenuButtonGroup.SetActive(false);
        if (UI.AppVersionGroup) UI.AppVersionGroup.SetActive(false);
        if (UI.RoomInputGroup) UI.RoomInputGroup.SetActive(true);
        if (UI.RegionDropdownGroup) UI.RegionDropdownGroup.SetActive(true);
        if (UI.SignalGroup) UI.SignalGroup.SetActive(false);
        if (UI.TabGroup) UI.TabGroup.SetActive(true);
        if (UI.StatusGroup) UI.StatusGroup.SetActive(false);

        if (UI.TabInput[(int)Tab.Online]) UI.TabInput[(int)Tab.Online].isOn = true;

        SetPanelVisibility(true);
      }
    }

    /// <summary>
    /// Show the popup screen and wait for the user to close it.
    /// If a popup is already shown, it will wait for that one instead and not show the second one.
    /// </summary>
    /// <param name="message">Message to display in the popup</param>
    protected virtual async System.Threading.Tasks.Task ShowPopupAsync(string message) {
      if (_popupTaskCompletionSource != null && _popupTaskCompletionSource.Task.IsCompleted == false) {
        await _popupTaskCompletionSource.Task;
        return;
      }

      _popupTaskCompletionSource?.TrySetResult(true);
      _popupTaskCompletionSource = new System.Threading.Tasks.TaskCompletionSource<bool>();
      UI.PopupText.text = message;
      UI.PopupGroup.SetActive(true);
      await _popupTaskCompletionSource.Task;
      UI.PopupGroup.SetActive(false);
    }

    /// <summary>
    /// A callback from the connection logic to report errors during runtime.
    /// </summary>
    /// <param name="errorMessage">The error message to display to the user</param>
    protected virtual async void OnConnectionError(string errorMessage) {
      await ShowPopupAsync(errorMessage);
      await ShutdownGameAsync();
    }

    /// <summary>
    /// Copy the room name button was pressed.
    /// <see cref="PhotonStartUICopyToClipboard"/> has a different implementation in WebGL.
    /// </summary>
    protected virtual void OnCopyRoomNamePressed() {
      PhotonStartUICopyToClipboard(UI.RoomNameInput.text);
    }

    /// <summary>
    /// The user pressed the disconnect button, shutdown the game and disconnect from the server.
    /// </summary>
    protected virtual async void OnDisconnectPressed() {
      await ShutdownGameAsync();
    }

    /// <summary>
    /// The sound mute button toggle was pressed.
    /// </summary>
    /// <param name="value">Muted state.</param>
    protected virtual void OnMuteValueChanged(bool value) {
      UI.MuteInput.transform.Find("On").gameObject.SetActive(!value);
      UI.MuteInput.transform.Find("Off").gameObject.SetActive(value);
      AudioListener.volume = value ? 0 : 1;
      UI.MuteInput.isOn = value;
      IsMuted = value;
    }

    /// <summary>
    /// Start the game when the user pressed the play button.
    /// </summary>
    protected virtual async void OnPlayPressed() {
      PlayerName = UI.PlayerNameInput.text.Trim();
      Region = UI.RegionDropdown.GetValue();
      await StartGameAsync(CurrentTab == Tab.Online, PlayerName, UI.RoomNameInput.text, Region);
    }

    /// <summary>
    /// The user pressed the popup button, complete the task so <see cref="ShowPopupAsync(string)"/> can complete.
    /// </summary>
    protected virtual void OnPopupPressed() {
      _popupTaskCompletionSource?.TrySetResult(true);
    }

    /// <summary>
    /// Quit the application when the user pressed the quit button.
    /// </summary>
    protected virtual void OnQuitPressed() {
      Application.Quit();
    }

    /// <summary>
    /// Toggle menu button pressed will show or hide the main menu panel.
    /// </summary>
    protected virtual void OnTogglePressed() {
      SetPanelVisibility(!UI.PanelGroup.gameObject.activeSelf);
    }

    /// <summary>
    /// Change visibility of elements based on the selected tab.
    /// </summary>
    /// <param name="tab">Selected tab.</param>
    protected virtual void OnTabSelected(Tab tab) {
      CurrentTab = tab;

      if (UI.TabInput[(int)Tab.Online]) UI.TabInput[(int)Tab.Online].transform.Find("On").gameObject.SetActive(tab == Tab.Online);
      if (UI.TabInput[(int)Tab.Local]) UI.TabInput[(int)Tab.Local].transform.Find("On").gameObject.SetActive(tab == Tab.Local);
      if (UI.TabInput[(int)Tab.Settings]) UI.TabInput[(int)Tab.Settings].transform.Find("On").gameObject.SetActive(tab == Tab.Settings);
      if (UI.TabInput[(int)Tab.Custom]) UI.TabInput[(int)Tab.Custom].transform.Find("On").gameObject.SetActive(tab == Tab.Custom);

      if (UI.RoomInputGroup != null) UI.RoomInputGroup.SetActive(tab == Tab.Online);
      if (UI.RegionDropdownGroup != null) UI.RegionDropdownGroup.SetActive(tab == Tab.Online);
    }
  }
}
