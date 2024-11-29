using System.Collections.Generic;
using Photon.Deterministic;

namespace Quantum {
    public static partial class DeterministicCommandSetup {
        static partial void AddCommandFactoriesUser(ICollection<IDeterministicCommandFactory> factories, RuntimeConfig gameConfig, SimulationConfig simulationConfig) {
            // In game
            factories.Add(new CommandSpawnReserveItem());
            factories.Add(new CommandHostEndGame());

            // --- In room
            // Start Game
            factories.Add(new CommandToggleCountdown());
            factories.Add(new CommandToggleReady());
            factories.Add(new CommandPlayerLoaded());

            // Change Data
            factories.Add(new CommandChangePlayerData());
            factories.Add(new CommandChangeRules());
            factories.Add(new CommandChangeHost());
            factories.Add(new CommandUpdatePing());
            factories.Add(new CommandSetInSettings());

            // Chat
            factories.Add(new CommandSendChatMessage());
            factories.Add(new CommandStartTyping());

            // Moderation
            factories.Add(new CommandBanPlayer());
            factories.Add(new CommandKickPlayer());
        }
    }
}