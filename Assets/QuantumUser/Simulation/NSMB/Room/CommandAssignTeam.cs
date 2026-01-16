using Photon.Deterministic;

namespace Quantum {
    public class CommandAssignTeam : DeterministicCommand, ILobbyCommand {

        public PlayerRef Target;
        public byte Team;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Target);
            stream.Serialize(ref Team);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom
                || !playerData->IsRoomHost
                || sender == Target
                || !f.PlayerIsConnected(Target)) {
                // Can't do this
                return;
            }

            var targetData = QuantumUtils.GetPlayerData(f, Target);
            if (targetData == null) {
                return;
            }

            if (Team != 255) {
                // Set team
                targetData->IsTeamLocked = false;
            } else {
                // Clear
                targetData->RequestedTeam = Team;
                targetData->IsTeamLocked = true;
            }
            f.Events.PlayerTeamChangedByHost(Target, Team);
        }
    }
}