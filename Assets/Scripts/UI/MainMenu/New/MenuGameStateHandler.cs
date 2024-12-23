using Quantum;
using UnityEngine;

public class MenuGameStateHandler : MonoBehaviour {

    public void Start() {
        QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
        QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
    }

    private void OnGameStateChanged(EventGameStateChanged e) {
        if (e.NewState == GameState.PreGameRoom) {
            gameObject.SetActive(true);
        } else if (gameObject.activeSelf) {
            GlobalController.Instance.loadingCanvas.Initialize(e.Game);
            gameObject.SetActive(false);
        }
    }

    private void OnGameDestroyed(CallbackGameDestroyed e) {
        GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        gameObject.SetActive(true);
    }
}