using Quantum;
using UnityEngine;

public class MenuGameStateHandler : MonoBehaviour {

    public void Start() {
        QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
        QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
        QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
        QuantumEvent.Subscribe<EventPlayerKickedFromRoom>(this, OnPlayerKickedFromRoom);
    }

    private void OnGameStateChanged(EventGameStateChanged e) {
        if (e.NewState == GameState.PreGameRoom) {
            gameObject.SetActive(true);
        } else if (gameObject.activeSelf) {
            GlobalController.Instance.loadingCanvas.Initialize(e.Game);
            gameObject.SetActive(false);
        }
    }

    private unsafe void OnGameResynced(CallbackGameResynced e) {
        Frame f = e.Game.Frames.Verified;
        if (f.Global->GameState == GameState.PreGameRoom) {
            gameObject.SetActive(true);
        } else if (gameObject.activeSelf) {
            // GlobalController.Instance.loadingCanvas.Initialize(e.Game);
            GlobalController.Instance.loadingCanvas.EndLoading(e.Game);
            gameObject.SetActive(false);
        }
    }

    private void OnGameDestroyed(CallbackGameDestroyed e) {
        GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        gameObject.SetActive(true);
    }

    private void OnPlayerKickedFromRoom(EventPlayerKickedFromRoom e) {
        if (e.Game.PlayerIsLocal(e.Player)) {
            NetworkHandler.Runner.Shutdown(ShutdownCause.SessionError);
        }
    }
}