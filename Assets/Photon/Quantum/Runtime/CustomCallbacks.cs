namespace Quantum.Demo.Legacy {
  using System;
  using UnityEngine;

  [Obsolete("Use the AddRuntimePlayers script instead")]
  public class CustomCallbacks : QuantumCallbacks {
    public override void OnGameStart(QuantumGame game, bool isResync) {
      game.AddPlayer(0, new RuntimePlayer());

      if (isResync) {
        Debug.Log("Detected Resync. Verified tick: " + game.Frames.Verified.Number);
      }
    }
  }
}

