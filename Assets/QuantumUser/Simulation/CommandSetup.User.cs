using System.Collections.Generic;
using Photon.Deterministic;

namespace Quantum {
    public static partial class DeterministicCommandSetup {
        static partial void AddCommandFactoriesUser(ICollection<IDeterministicCommandFactory> factories, RuntimeConfig gameConfig, SimulationConfig simulationConfig) {
            // In game
            factories.Add(new CommandSpawnReserveItem());

            // In room
            factories.Add(new CommandChangePlayerData());
            factories.Add(new CommandSendChatMessage());
            factories.Add(new CommandStartTyping());
            factories.Add(new CommandToggleCountdown());
            factories.Add(new CommandToggleReady());
            factories.Add(new CommandUpdatePing());
        }
    }
}