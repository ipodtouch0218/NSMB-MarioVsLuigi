using Quantum;
using UnityEngine;

namespace NSMB.UI.MainMenu {
    public unsafe class MenuGameStateHandler : MonoBehaviour {

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            gameObject.SetActive(e.NewState == GameState.PreGameRoom);
        }

        private void OnGameResynced(CallbackGameResynced e) {
            gameObject.SetActive(e.Game.Frames.Predicted.Global->GameState == GameState.PreGameRoom);
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            gameObject.SetActive(true);
        }
    }
}
