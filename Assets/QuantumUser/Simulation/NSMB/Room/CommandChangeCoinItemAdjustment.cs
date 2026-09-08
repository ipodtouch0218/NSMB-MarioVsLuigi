using Photon.Deterministic;

namespace Quantum {
    public unsafe class CommandChangeCoinItemAdjustment : DeterministicCommand, ILobbyCommand {

        public AssetRef<CoinItemAsset> CoinItem;
        public FP CustomWeight;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref CoinItem);
            stream.Serialize(ref CustomWeight);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom
                || !playerData->IsRoomHost(f)) {
                // Nuh uh uh... nice try big boy
                return;
            }

            if (!f.TryResolveDictionary(f.Global->Rules.CoinItemCustomSpawnWeights, out var customWeights)) {
                f.Global->Rules.CoinItemCustomSpawnWeights = customWeights = f.AllocateDictionary<AssetRef<CoinItemAsset>, FP>();
            }

            customWeights[CoinItem] = CustomWeight;
            f.Events.CoinItemCustomSpawnWeightChanged(CoinItem, CustomWeight);
        }
    }
}
