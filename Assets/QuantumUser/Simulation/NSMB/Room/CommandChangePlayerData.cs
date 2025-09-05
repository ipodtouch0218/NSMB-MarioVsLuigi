using Photon.Deterministic;

namespace Quantum {
    public class CommandChangePlayerData : DeterministicCommand, ILobbyCommand {

        public Changes EnabledChanges;

        public byte Character;
        public byte Palette;
        public byte Team;
        public bool Spectating;

        public override void Serialize(BitStream stream) {
            byte changes = (byte) EnabledChanges;
            stream.Serialize(ref changes);
            EnabledChanges = (Changes) changes;

            stream.Serialize(ref Character);
            stream.Serialize(ref Palette);
            stream.Serialize(ref Team);
            stream.Serialize(ref Spectating);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            bool pregame = f.Global->GameState == GameState.PreGameRoom;
            if (!pregame && !playerData->IsSpectator) {
                return;
            }

            Changes playerChanges = EnabledChanges;

            if (f.Global->GameStartFrames > 0) {
                // Cannot change team when game is in countdown
                playerChanges &= ~Changes.Team;
            }

            if (playerChanges == 0) {
                return;
            }

            if (playerChanges.HasFlag(Changes.Character)) {
                playerData->Character = Character;
            }
            if (playerChanges.HasFlag(Changes.Palette)) {
                playerData->Palette = Palette;
            }
            if (playerChanges.HasFlag(Changes.Team)) {
                playerData->RequestedTeam = Team;
            }
            if (playerChanges.HasFlag(Changes.Spectating) && pregame) {
                playerData->ManualSpectator = Spectating;
                playerData->IsSpectator = Spectating;
            }

            if (f.Global->GameStartFrames > 0 && !QuantumUtils.IsGameStartable(f)) {
                GameLogicSystem.StopCountdown(f);
            }

            f.Events.PlayerDataChanged(playerData->PlayerRef);
        }

        public enum Changes : byte {
            Character = 1 << 0,
            Palette = 1 << 1,
            Team = 1 << 2,
            Spectating = 1 << 3,
            All = byte.MaxValue,
        }
    }
}