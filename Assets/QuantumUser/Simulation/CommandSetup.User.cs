using Photon.Deterministic;
using System.Collections.Generic;

namespace Quantum {
    public static partial class DeterministicCommandSetup {
        static partial void AddCommandFactoriesUser(ICollection<IDeterministicCommandFactory> factories, RuntimeConfig gameConfig, SimulationConfig simulationConfig) {
            // --- In game
            factories.Add(new CommandSpawnReserveItem());
            factories.Add(new CommandTaunt());
            factories.Add(new CommandHostEndGame());
            factories.Add(new CommandEndGameContinue());

            // --- In pre-game room
            // Start Game
            factories.Add(new CommandToggleCountdown());
            factories.Add(new CommandToggleReady());
            factories.Add(new CommandPlayerLoaded());

            // Change Data
            factories.Add(new CommandChangePlayerData());
            factories.Add(new CommandChangeRules());
            factories.Add(new CommandToggleRandomStage());
            factories.Add(new CommandChangeCoinItemAdjustment());
            factories.Add(new CommandChangeHost());
            factories.Add(new CommandUpdatePing());
            factories.Add(new CommandSetInSettings());
            factories.Add(new CommandRandomizeAllTeams());

            // Chat
            factories.Add(new CommandSendChatMessage());
            factories.Add(new CommandStartTyping());

            // Moderation
            factories.Add(new CommandBanPlayer());
            factories.Add(new CommandKickPlayer());
            factories.Add(new CommandUnbanPlayer());
            factories.Add(new CommandAssignTeam());
            factories.Add(new CommandMvLDebugCmd());
        }
    }
}