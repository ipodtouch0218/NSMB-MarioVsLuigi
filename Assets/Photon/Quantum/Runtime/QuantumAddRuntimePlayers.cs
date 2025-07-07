namespace Quantum {
  /// <summary>
  ///   Similar to the CustomCallback script and the <see cref="QuantumRunnerLocalDebug" /> this script will add all players
  ///   inside the <see cref="RuntimePlayer" /> list as local players to Quantum during the game start. The script must <see cref="Awake" /> before starting the
  ///   game and it works for local debug and online matches via a menu.
  ///   Remove this script from your scene when the players are only added inside the menu classes (use the Player list in
  ///   <see cref="QuantumRunnerLocalDebug" />  to start game scenes directly).
  /// </summary>
  public class QuantumAddRuntimePlayers : QuantumMonoBehaviour {
    /// <summary>
    /// These RuntimePlayer represent the players that will be added on game start.
    /// </summary>
    public RuntimePlayer[] Players;

    /// <summary>
    /// Unity Awake event, used to subscribe to the game start event of <see cref="QuantumRunner.Default"/>.
    /// </summary>
    public void Awake() {
      QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game, c.IsResync), game => game == QuantumRunner.Default.Game);
    }

    /// <summary>
    /// Add all players to the game.
    /// </summary>
    /// <param name="game">Quantum game</param>
    /// <param name="isResync">IsResync indicates the client is late-joining or reconnecting to a Quantum game</param>
    public void OnGameStarted(QuantumGame game, bool isResync) {
      for (int i = 0; i < Players.Length; i++) {
        game.AddPlayer(i, Players[i]);
      }
    }
  }
}