using Quantum;
using UnityEngine;

public class MenuGameStateHandler : MonoBehaviour {

    public void Start() {
        QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
        QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
        QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
    }

    private void OnGameStateChanged(EventGameStateChanged e) {
        if (e.NewState == GameState.PreGameRoom) {
            gameObject.SetActive(true);
        } else {
            GlobalController.Instance.loadingCanvas.Initialize(e.Game);
            gameObject.SetActive(false);
        }
    }

    private unsafe void OnGameStarted(CallbackGameStarted e) {
        Frame f = e.Game.Frames.Predicted;
        OnGameStateChanged(new EventGameStateChanged { 
            Game = e.Game,
            Frame = f,
            NewState = f.Global->GameState,
            Tick = f.Number,
        });
    }

    private void OnGameDestroyed(CallbackGameDestroyed e) {
        GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        gameObject.SetActive(true);
    }
}