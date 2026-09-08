using Photon.Deterministic;

namespace Quantum {
    public class CommandAssignTeam : DeterministicCommand, ILobbyCommand {

        public PlayerRef Target;
        public byte Team;
        public bool Clear;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Target);
            stream.Serialize(ref Team);
            stream.Serialize(ref Clear);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom
                || !playerData->IsRoomHost(f)
                || sender == Target) {
                // Can't do this
                return;
            }

            var targetData = QuantumUtils.GetPlayerData(f, Target);
            if (targetData == null) {
                return;
            }
            
            if (Clear) {
                // Clear
                targetData->IsTeamLocked = false;
            } else {
                // Set team
                targetData->RequestedTeam = Team;
                targetData->IsTeamLocked = true;
            }
            f.Events.PlayerTeamChangedByHost(Target, Team, Clear);
        }
    }
}