
namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  public class QuantumDebugInput : MonoBehaviour {

        private void OnEnable() {
            QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));
        }

        /// <summary>
        /// Set an empty input when polled by the simulation.
        /// </summary>
        /// <param name="callback"></param>
        public void PollInput(CallbackPollInput callback) {
          Quantum.Input i = new Quantum.Input();
          callback.SetInput(i, DeterministicInputFlags.Repeatable);
        }
  }
}
