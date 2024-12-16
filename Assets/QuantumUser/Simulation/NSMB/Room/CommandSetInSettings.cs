using Photon.Deterministic;

namespace Quantum {
    public class CommandSetInSettings : DeterministicCommand, ILobbyCommand {

        public bool InSettings;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref InSettings);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            playerData->IsInSettings = InSettings;
            f.Events.PlayerDataChanged(f, sender);
        }
    }
}