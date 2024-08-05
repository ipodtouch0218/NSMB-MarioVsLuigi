using System;
using UnityEngine;

namespace Quantum {
    public unsafe partial class Frame {

        public StageTileInstance[] StageTiles => _stageTiles;
        public Vector2Int StageDimensions => _stageDimensions;

        private StageTileInstance[] _stageTiles;
        private Vector2Int _stageDimensions;

        partial void InitUser() {
            var stage = FindAsset<VersusStageData>(Map.UserAsset);
            _stageDimensions = stage.TileDimensions;
            _stageTiles = new StageTileInstance[_stageDimensions.x * _stageDimensions.y];
        }

        partial void SerializeUser(FrameSerializer serializer) {
            serializer.Stream.WriteInt(_stageDimensions.x);
            serializer.Stream.WriteInt(_stageDimensions.y);
            serializer.Stream.SerializeArrayLength(ref _stageTiles);
            for (int i = 0; i < _stageTiles.Length; i++) {
                serializer.Stream.Serialize(ref _stageTiles[i].Tile);
                serializer.Stream.Serialize(ref _stageTiles[i].Rotation);
                serializer.Stream.Serialize(ref _stageTiles[i].Scale);
            }
        }

        partial void CopyFromUser(Frame frame) {
            Array.Copy(frame._stageTiles, _stageTiles, frame._stageTiles.Length);
        }
    }
}