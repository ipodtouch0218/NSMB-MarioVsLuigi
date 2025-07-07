namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEngine;

  /// <summary>
  /// This class keeps track of individual Photon connections and Quantum simulations (QuantumRunner). 
  /// </summary>
  public class QuantumMultiClientPlayer : QuantumMonoBehaviour {
    /// <summary>
    /// The QuantumRunner that this player belongs to.
    /// </summary>
    public QuantumRunner Runner { get; set; }
    /// <summary>
    /// Access the runner id of it's runner or null if it does not exist.
    /// </summary>
    public string RunnerId => Runner?.Id;
    /// <summary>
    /// The Input object to toggle on or off.
    /// </summary>
    public GameObject Input => _input;
    /// <summary>
    /// The shutdown handler used to keep track of disconnect events from the QuantumRunner. Requires a reference to clean up during destruction.
    /// </summary>
    public IDisposable ShutdownHandler { get; set; }
    /// <summary>
    /// The callback to notify the MultiClientRunner class of pressing exit.
    /// </summary>
    public Action<QuantumMultiClientPlayer> DestroyPlayerCallback { get; set; }
    [Obsolete("Use DestroyPlayerCallback")]
    public Action<QuantumMultiClientPlayer> DetroyPlayerCallback { get { return DestroyPlayerCallback; } set { DestroyPlayerCallback = value; } }
    /// <summary>
    /// The local player slot this player uses. Only used when AddAsLocalPlayers is enabled.
    /// </summary>
    public int PlayerSlot { get; set; }
    /// <summary>
    /// If this player is using the connection and QuantumRunner of another player (only used when AddAsLocalPlayers is enabled).
    /// </summary>
    public QuantumMultiClientPlayer MainPlayer { get; set; }
    /// <summary>
    /// Other local players running on this players connection and QuantumRunner.
    /// </summary>
    public List<QuantumMultiClientPlayer> LocalPlayers { get; set; }
    /// <summary>
    /// Access the view object.
    /// </summary>
    public QuantumMultiClientPlayerView View => _ui;

    QuantumMultiClientPlayerView _ui;
    GameObject _input;
    QuantumEntityViewUpdater _evu;

    /// <summary>
    /// Constructor.
    /// </summary>
    public QuantumMultiClientPlayer() {
      LocalPlayers = new List<QuantumMultiClientPlayer>();
    }

    /// <summary>
    /// Tries to allocate a free player slot for this player. Returns a valid player slot or -1 if no slot is available.
    /// </summary>
    public int CreateFreeClientPlayerSlot(int maxLocalPlayers) {
      for (int i = 1; i < maxLocalPlayers; i++) {
        if (LocalPlayers.Any(p => p.PlayerSlot == i) == false) {
          return i;
        }
      }
      return -1;
    }

    /// <summary>
    /// Gets the highest sibling index of this player and all local players used to correctly name GameObjects.
    /// </summary>
    /// <returns></returns>
    public int GetHighestSiblingIndex() {
      var siblingIndex = View.transform.GetSiblingIndex();
      foreach (var p in LocalPlayers) {
        siblingIndex = Math.Max(siblingIndex, p.View.transform.GetSiblingIndex());
      }

      return siblingIndex;
    }

    /// <summary>
    /// Instantiates an input script from a template.
    /// </summary>
    public void CreateInput(GameObject playerInputTemplate) {
      if (playerInputTemplate != null) {
        _input = Instantiate(playerInputTemplate);
        _input.transform.SetParent(gameObject.transform);
        _input.SetActive(true);
      }
    }

    /// <summary>
    /// Instantiates an entity view updater script from a template.
    /// </summary>
    public void CreateEntityViewUpdater(QuantumEntityViewUpdater entityViewUpdaterTemplate, QuantumGame game) {
      if (entityViewUpdaterTemplate != null) {
        // Use EVU template from parent
        _evu = Instantiate(entityViewUpdaterTemplate);
        _evu.gameObject.name = $"QuantumEntityViewUpdater {name}";
        _evu.gameObject.SetActive(true);
      } else {
        // Create and add our EVU script
        var go = new GameObject($"QuantumEntityViewUpdater {name}");
        _evu = go.AddComponent<QuantumEntityViewUpdater>();
      }

      _evu.ViewParentTransform = _evu.transform;
      _evu.SetCurrentGame(game);
      _evu.transform.SetParent(gameObject.transform);
    }

    /// <summary>
    /// Binds a player view to this player.
    /// </summary>
    public void BindView(QuantumMultiClientPlayerView view, bool isFirstPlayer, bool isAddPlayerEnabled) {
      _ui = view;
      _ui.SetRunning(isAddPlayerEnabled);

      _ui.Input.onValueChanged.AddListener(OnInputToggle);
      _ui.View.onValueChanged.AddListener(OnViewToggle);
      _ui.Gizmos.onValueChanged.AddListener(OnGizmoToggle);
      _ui.Quit.onClick.AddListener(OnQuitPressed);

      _ui.Input.isOn = isFirstPlayer;
      _ui.View.isOn = isFirstPlayer;
      _ui.Gizmos.isOn = false;
    }

    /// <summary>
    /// Unity destroy callback shuts down the player (ui, input and evu).
    /// </summary>
    public void Destroy() {
      ShutdownHandler?.Dispose();
      ShutdownHandler = null;

      MainPlayer?.LocalPlayers.Remove(this);
      MainPlayer = null;

      if (_ui != null) {
        Destroy(_ui.gameObject);
        _ui = null;
      }

      if (_input != null) {
        Destroy(_input);
        _input = null;
      }

      if (_evu != null) {
        Destroy(_evu);
        _evu = null;
      }

      Destroy(gameObject);
    }

    private void OnInputToggle(bool isEnabled) {
      if (_input != null) {
        _input.SetActive(isEnabled);
      }
    }

    private void OnViewToggle(bool isEnabled) {
      if (_evu != null) {
        _evu.gameObject.SetActive(isEnabled);
      }
    }

    private void OnGizmoToggle(bool isEnabled) {
      if (Runner != null) {
        Runner.HideGizmos = !isEnabled;
      }
    }

    private void OnQuitPressed() {
      DestroyPlayerCallback?.Invoke(this);
    }
  }
}