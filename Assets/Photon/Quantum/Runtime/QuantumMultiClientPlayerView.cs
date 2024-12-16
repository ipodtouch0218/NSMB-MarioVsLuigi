namespace Quantum {
  using UnityEngine;
  using UnityEngine.UI;

  /// <summary>
  /// The multi client UI view of one player.
  /// </summary>
  public class QuantumMultiClientPlayerView : QuantumMonoBehaviour {
    /// <summary>
    /// The name of the player.
    /// </summary>
    public Text Label;
    /// <summary>
    /// The "is connecting" label.
    /// </summary>
    public GameObject ConnectingLabel;
    /// <summary>
    /// The button to toggle the input.
    /// </summary>
    public Toggle Input;
    /// <summary>
    /// The button to toggle the EntityViewUpdater.
    /// </summary>
    public Toggle View;
    /// <summary>
    /// The button to toggle the game gizmo rendering.
    /// </summary>
    public Toggle Gizmos;
    /// <summary>
    /// The button to add a player using the same client.
    /// </summary>
    public UnityEngine.UI.Button AddPlayer;
    /// <summary>
    /// The button to quit the player.
    /// </summary>
    public UnityEngine.UI.Button Quit;

    /// <summary>
    /// Configure the view for loading status.
    /// </summary>
    public void SetLoading() {
      ConnectingLabel.SetActive(true);
      Input.gameObject.SetActive(false);
      View.gameObject.SetActive(false);
      Gizmos.gameObject.SetActive(false);
      AddPlayer.gameObject.SetActive(false);
      Quit.gameObject.SetActive(false);
    }

    /// <summary>
    /// Configure the view for running status.
    /// </summary>
    /// <param name="isAddPlayerEnabled">It <see langword="true"/> the add player button is enabled.</param>
    public void SetRunning(bool isAddPlayerEnabled) {
      ConnectingLabel.SetActive(false);
      Input.gameObject.SetActive(true);
      View.gameObject.SetActive(true);
      Gizmos.gameObject.SetActive(true);
      AddPlayer.gameObject.SetActive(true);
      AddPlayer.gameObject.GetComponentInChildren<UnityEngine.UI.Button>().interactable = isAddPlayerEnabled;
      Quit.gameObject.SetActive(true);
    }
  }
}