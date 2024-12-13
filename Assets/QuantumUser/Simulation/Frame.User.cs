using System;

namespace Quantum {
    public unsafe partial class Frame {

        public StageTileInstance[] StageTiles;

        

        /* IDK if this is valid
        private AssetRef<Map> cachedMap;
        private VersusStageData cachedUserAsset;
        public VersusStageData StageAsset {
            get {
                if (!MapAssetRef.Equals(cachedMap)) {
                    cachedMap = MapAssetRef;
                    if (Map == null) {
                        cachedUserAsset = null;
                    } else {
                        cachedUserAsset = FindAsset<VersusStageData>(Map.UserAsset);
                    }
                }

                return cachedUserAsset;
            }
        }
        */

        partial void InitUser() {
            StageTiles = Array.Empty<StageTileInstance>();
        }

        partial void SerializeUser(FrameSerializer serializer) {
            serializer.Stream.SerializeArrayLength(ref StageTiles);
            for (int i = 0; i < StageTiles.Length; i++) {
                serializer.Stream.Serialize(ref StageTiles[i].Tile);
                serializer.Stream.Serialize(ref StageTiles[i].Rotation);
                serializer.Stream.Serialize(ref StageTiles[i].Scale);
            }
        }

        partial void CopyFromUser(Frame frame) {
            if (StageTiles.Length != frame.StageTiles.Length) {
                StageTiles = new StageTileInstance[frame.StageTiles.Length];
            }
            Array.Copy(frame.StageTiles, StageTiles, frame.StageTiles.Length);
        }
    }
}