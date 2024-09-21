using System;
using UnityEngine;

namespace Quantum {
    public unsafe partial class Frame {

        public StageTileInstance[] StageTiles => _stageTiles;
        public Vector2Int StageDimensions => _stageDimensions;

        private StageTileInstance[] _stageTiles;
        private Vector2Int _stageDimensions;
        private bool validStage;

        partial void InitUser() {
            if (!Map) {
                return;
            }
            var stage = FindAsset<VersusStageData>(Map.UserAsset);
            _stageDimensions = stage.TileDimensions;
            _stageTiles = new StageTileInstance[_stageDimensions.x * _stageDimensions.y];
        }

        partial void SerializeUser(FrameSerializer serializer) {

            serializer.Stream.Serialize(ref validStage);

            int width = _stageDimensions.x;
            int height = _stageDimensions.y;

            if (validStage) {
                serializer.Stream.Serialize(ref width);
                serializer.Stream.Serialize(ref height);
                serializer.Stream.SerializeArrayLength(ref _stageTiles);
                for (int i = 0; i < _stageTiles.Length; i++) {
                    serializer.Stream.Serialize(ref _stageTiles[i].Tile);
                    serializer.Stream.Serialize(ref _stageTiles[i].Rotation);
                    serializer.Stream.Serialize(ref _stageTiles[i].Scale);
                }

                _stageDimensions.x = width;
                _stageDimensions.y = height;
            }
        }

        partial void CopyFromUser(Frame frame) {
            validStage = Map;
            if (frame.validStage && validStage) {
                // Copy map data
                _stageDimensions = frame._stageDimensions;
                Array.Copy(frame._stageTiles, _stageTiles, frame._stageTiles.Length);
            }
        }
    }
}