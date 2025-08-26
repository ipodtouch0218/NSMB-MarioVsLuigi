using Photon.Deterministic;

namespace Quantum {
    public class CommandChangePlayerData : DeterministicCommand, ILobbyCommand {

        public Changes EnabledChanges;

        public byte Character;
        public float PrimaryHue;
        public float SecondaryHue;
        public byte HueSettings;
        public byte Team;
        public bool Spectating;

        public override void Serialize(BitStream stream) {
            byte changes = (byte) EnabledChanges;
            stream.Serialize(ref changes);
            EnabledChanges = (Changes) changes;

            stream.Serialize(ref Character);
            stream.Serialize(ref PrimaryHue);
            stream.Serialize(ref SecondaryHue);
            stream.Serialize(ref HueSettings);
            stream.Serialize(ref Team);
            stream.Serialize(ref Spectating);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            bool pregame = f.Global->GameState == GameState.PreGameRoom;
            if (!pregame && !playerData->IsSpectator) {
                return;
            }

            Changes playerChanges = EnabledChanges;

            if (playerChanges.HasFlag(Changes.Character)) {
                playerData->Character = Character;
            }
            if (playerChanges.HasFlag(Changes.PrimaryHue)) {
                playerData->PrimaryHue = FP.FromString(PrimaryHue.ToString());
            }
            if (playerChanges.HasFlag(Changes.SecondaryHue)) {
                playerData->SecondaryHue = FP.FromString(SecondaryHue.ToString());
            }
            if (playerChanges.HasFlag(Changes.HueSettings)) {
                playerData->HueSettings = HueSettings;
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
            PrimaryHue = 1 << 1,
            SecondaryHue = 1 << 2,
            HueSettings = 1 << 3,
            Team = 1 << 4,
            Spectating = 1 << 5,
            All = byte.MaxValue,
        }
    }
}