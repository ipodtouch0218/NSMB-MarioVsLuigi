using Quantum;
using UnityEngine;

namespace NSMB.UI.MainMenu {
    public unsafe class MenuGameStateHandler : MonoBehaviour {

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
        }

        private void OnGameStarted(CallbackGameStarted e) {
            if (e.Game.Frames.Predicted.Global->GameState > GameState.PreGameRoom) {
                GlobalController.Instance.loadingCanvas.Initialize(null);
            }
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.PreGameRoom) {
                gameObject.SetActive(true);
            } else {
                if (gameObject.activeSelf) {
                    GlobalController.Instance.loadingCanvas.Initialize(e.Game);
                    gameObject.SetActive(false);
                }
            }
        }

        private void OnGameResynced(CallbackGameResynced e) {
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
    }
}
