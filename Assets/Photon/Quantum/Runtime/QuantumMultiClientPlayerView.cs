namespace Quantum {
  using UnityEngine;
  using UnityEngine.UI;

  public class QuantumMultiClientPlayerView : QuantumMonoBehaviour {
    public Text                  Label;
    public GameObject            ConnectingLabel;
    public Toggle                Input;
    public Toggle                View;
    public Toggle                Gizmos;
    public UnityEngine.UI.Button AddPlayer;
    public UnityEngine.UI.Button Quit;

    public void SetLoading() {
      ConnectingLabel.SetActive(true);
      Input.gameObject.SetActive(false);
      View.gameObject.SetActive(false);
      Gizmos.gameObject.SetActive(false);
      AddPlayer.gameObject.SetActive(false);
      Quit.gameObject.SetActive(false);
    }

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