using Photon.Deterministic;

namespace Quantum {
    public class CommandUpdatePing : DeterministicCommand, ILobbyCommand {

        public int PingMs;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref PingMs);
        }
        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            playerData->Ping = PingMs;
            f.Events.PlayerDataChanged(f, sender);
        }
    }
}