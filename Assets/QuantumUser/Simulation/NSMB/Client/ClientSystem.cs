using UnityEngine;

namespace Quantum {
    public unsafe class ClientSystem : SystemMainThread, ISignalOnPlayerConnected, ISignalOnPlayerDisconnected {

        public override void Update(Frame f) {

        }

        public void OnPlayerConnected(Frame f, PlayerRef player) {
            var config = f.SimulationConfig;

            RuntimePlayer data = f.GetPlayerData(player);
            int characterIndex = Mathf.Clamp(data.CharacterIndex, 0, config.CharacterDatas.Length - 1);
            CharacterAsset character = config.CharacterDatas[characterIndex];

            EntityRef newPlayer = f.Create(character.Prototype);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(newPlayer);
            mario->PlayerRef = player;
            var newTransform = f.Unsafe.GetPointer<Transform2D>(newPlayer);
            newTransform->Position = f.FindAsset<VersusStageData>(f.Map.UserAsset).Spawnpoint;
        }

        public void OnPlayerDisconnected(Frame f, PlayerRef player) {

        }
    }
}