using Quantum;
using UnityEngine;

public class MenuGameStateHandler : MonoBehaviour {

    public void Start() {
        QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
        QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
    }

    private void OnGameStateChanged(EventGameStateChanged e) {
        switch (e.NewState) {
        case GameState.WaitingForPlayers:
            GlobalController.Instance.loadingCanvas.Initialize(e.Game);
            gameObject.SetActive(false);
            break;
        case GameState.PreGameRoom:
            gameObject.SetActive(true);
            break;
        }
    }

    private void OnGameDestroyed(CallbackGameDestroyed e) {
        GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        gameObject.SetActive(true);
    }
}