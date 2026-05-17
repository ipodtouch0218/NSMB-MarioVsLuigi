using Photon.Deterministic;

namespace Quantum {
    public unsafe class CommandToggleRandomStage : DeterministicCommand, ILobbyCommand {

        public AssetRef<Map> Stage;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Stage);
        }

        public void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom
                || !playerData->IsRoomHost) {
                // Can't let you do that, Star Fox
                return;
            }

            if (!f.TryResolveHashSet(f.Global->Rules.RandomDisabledStages, out var disabledStages)) {
                f.Global->Rules.RandomDisabledStages = disabledStages = f.AllocateHashSet<AssetRef<Map>>();
            }

            bool isDisabled = false;
            if (!disabledStages.Remove(Stage)) {
                // Failed to remove, meaning it WASN'T disabled before, so add it.
                disabledStages.Add(Stage);
                isDisabled = true;
            } else {
                isDisabled = false;
            }

            f.Events.RandomStageToggled(Stage, isDisabled);
        }
    }
}