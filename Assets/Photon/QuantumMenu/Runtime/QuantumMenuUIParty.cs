namespace Quantum.Menu {
  using System;
  using System.Collections.Generic;
  using System.Threading.Tasks;
#if QUANTUM_ENABLE_TEXTMESHPRO
  using InputField = TMPro.TMP_InputField;
#else 
  using InputField = UnityEngine.UI.InputField;
#endif
  using UnityEngine;
  using UnityEngine.UI;

  /// <summary>
  /// The party screen shows two modes. Creating a new game or joining a game with a party code.
  /// After creating a game the session party code can be optained via the ingame menu.
  /// One speciality is that a region list is requested from the connection when entering the screen in order to create a matching session codes.
  /// </summary>
  public partial class QuantumMenuUIParty : QuantumMenuUIScreen {
    /// <summary>
    /// The session code input field.
    /// </summary>
    [InlineHelp, SerializeField] protected InputField _sessionCodeField;
    /// <summary>
    /// The create game button.
    /// </summary>
    [InlineHelp, SerializeField] protected Button _createButton;
    /// <summary>
    /// The join game button.
    /// </summary>
    [InlineHelp, SerializeField] protected Button _joinButton;
    /// <summary>
    /// The back button.
    /// </summary>
    [InlineHelp, SerializeField] protected Button _backButton;

    /// <summary>
    /// The task of requesting the regions.
    /// </summary>
    protected Task<List<QuantumMenuOnlineRegion>> _regionRequest;

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
    /// When entering this screen an async request to retrieve the available regions is started.
    /// </summary>
    public override void Show() {
      base.Show();

      if (Config.CodeGenerator == null) {
        Debug.LogError("Add a CodeGenerator to the QuantumMenuConfig");
      }

      _sessionCodeField.SetTextWithoutNotify("".PadLeft(Config.CodeGenerator.Length, '-'));
      _sessionCodeField.characterLimit = Config.CodeGenerator.Length;

      if (_regionRequest == null || _regionRequest.IsFaulted) {
        // Request the regions already when entering the party menu
        _regionRequest = Connection.RequestAvailableOnlineRegionsAsync(ConnectionArgs);
      }

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
    /// Is called when the <see cref="_createButton"/> is pressed using SendMessage() from the UI object.
    /// </summary>
    protected virtual async void OnCreateButtonPressed() {
      await ConnectAsync(true);
    }

    /// <summary>
    /// Is called when the <see cref="_joinButton"/> is pressed using SendMessage() from the UI object.
    /// </summary>
    protected virtual async void OnJoinButtonPressed() {
      await ConnectAsync(false);
    }

    /// <summary>
    /// Is called when the <see cref="_backButton"/> is pressed using SendMessage() from the UI object.
    /// </summary>
    public virtual void OnBackButtonPressed() {
      Controller.Show<QuantumMenuUIMain>();
    }

    /// <summary>
    /// The connect method to handle create and join.
    /// Internally the region request is awaited.
    /// </summary>
    /// <param name="creating">Create or join</param>
    /// <returns></returns>
    protected virtual async Task ConnectAsync(bool creating) {
      // Test for input errors before switching screen
      var inputRegionCode = _sessionCodeField.text.ToUpper();
      if (creating == false && Config.CodeGenerator.IsValid(inputRegionCode) == false) {
        await Controller.PopupAsync($"The session code '{inputRegionCode}' is not a valid session code. Please enter {Config.CodeGenerator.Length} characters or digits.", "Invalid Session Code");
        return;
      }

      if (_regionRequest.IsCompleted == false) {
        // Goto loading screen
        Controller.Show<QuantumMenuUILoading>();
        Controller.Get<QuantumMenuUILoading>().SetStatusText("Fetching Regions");

        try {
          // TODO: Disconnect button not usable during this time
          await _regionRequest;
        } catch (Exception e) {
          Debug.LogException(e);
          // Error is handled in next section
        }
      }

      if (_regionRequest.IsCompletedSuccessfully == false && _regionRequest.Result.Count == 0) {
        await Controller.PopupAsync($"Failed to request regions.", "Connection Failed");
        Controller.Show<QuantumMenuUIMain>();
        return;
      }

      if (creating) {
        var regionIndex = -1;
        if (string.IsNullOrEmpty(ConnectionArgs.PreferredRegion)) {
          // Select a best region now
          regionIndex = FindBestAvailableOnlineRegionIndex(_regionRequest.Result);
        } else {
          regionIndex = _regionRequest.Result.FindIndex(r => r.Code == ConnectionArgs.PreferredRegion);
        }

        if (regionIndex == -1) {
          await Controller.PopupAsync($"Selected region is not available.", "Connection Failed");
          Controller.Show<QuantumMenuUIMain>();
          return;
        }

        ConnectionArgs.Session = Config.CodeGenerator.EncodeRegion(Config.CodeGenerator.Create(), regionIndex);
        ConnectionArgs.Region = _regionRequest.Result[regionIndex].Code;
      } else {
        var regionIndex = Config.CodeGenerator.DecodeRegion(inputRegionCode);
        if (regionIndex < 0 || regionIndex > Config.AvailableRegions.Count) {
          await Controller.PopupAsync($"The session code '{inputRegionCode}' is not a valid session code (cannot decode the region).", "Invalid Session Code");
          return;
        }

        ConnectionArgs.Session = _sessionCodeField.text.ToUpper(); ;
        ConnectionArgs.Region = Config.AvailableRegions[regionIndex];
      }

      ConnectionArgs.Creating = creating;

      Controller.Show<QuantumMenuUILoading>();

      var result = await Connection.ConnectAsync(ConnectionArgs);

      await Controller.HandleConnectionResult(result, this.Controller);
    }

    /// <summary>
    /// Find the region with the lowest ping.
    /// </summary>
    /// <param name="regions">Region list</param>
    /// <returns>The index of the region with the lowest ping</returns>
    protected static int FindBestAvailableOnlineRegionIndex(List<QuantumMenuOnlineRegion> regions) {
      var lowestPing = int.MaxValue;
      var index = -1;
      for (int i = 0; regions != null && i < regions.Count; i++) {
        if (regions[i].Ping < lowestPing) {
          lowestPing = regions[i].Ping;
          index = i;
        }
      }

      return index;
    }

  }
}
