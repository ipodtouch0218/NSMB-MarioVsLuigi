using Photon.Deterministic;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NSMB.Tiles {
    /// <summary>
    /// A RuleTile that can match with both itself, and other defined "sibling" tiles.
    /// Credit: https://github.com/Unity-Technologies/2d-extras/issues/67#issuecomment-616013310
    /// </summary>
    [CreateAssetMenu(fileName = "New Sibling Rule Tile", menuName = "Sibling RuleTile", order = 0)]
    [ExecuteInEditMode]
    public class SiblingRuleTile : RuleTile<SiblingRuleTile.Neighbor> {

        public class Neighbor : RuleTile.TilingRule.Neighbor {
            public const int Self = 3;
            public const int NotSelf = 4;
        }

        //---Static Variables
        private static QuantumMapData mapData;
        private static VersusStageData cachedStage;
#if UNITY_EDITOR
        private static float lastMapCheckTime;
#endif

        //---Serialized Variables
        [SerializeField] public List<TileBase> siblings;

        public override bool RuleMatch(int neighbor, TileBase other) {
            return neighbor switch {
                TilingRuleOutput.Neighbor.This => siblings.Contains(other) || other == this,
                TilingRuleOutput.Neighbor.NotThis => !siblings.Contains(other) && other != this,
                Neighbor.Self => other == this,
                Neighbor.NotSelf => other != this,
                _ => base.RuleMatch(neighbor, other),
            };
        }

        public override Vector3Int GetOffsetPosition(Vector3Int position, Vector3Int offset) {
            Vector3Int result = position + offset;
            if (!cachedStage || !mapData) {
#if UNITY_EDITOR
                if (!Application.isPlaying) {
                    if (lastMapCheckTime == Time.time) {
                        return result;
                    }
                    lastMapCheckTime = Time.time;
                }
#endif
                if (!(mapData = FindFirstObjectByType<QuantumMapData>(FindObjectsInactive.Include))) {
                    return result;
                }
                cachedStage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(mapData.Asset.UserAsset);
                if (!cachedStage) {
                    return result;
                }
            }

            FPVector2 wrapped = QuantumUtils.WrapUnityTile(cachedStage, new FPVector2(result.x, result.y), out _);
            return new Vector3Int(wrapped.X.AsInt, wrapped.Y.AsInt, result.z);
        }

        public override Vector3Int GetOffsetPositionReverse(Vector3Int position, Vector3Int offset) {
            Vector3Int result = position - offset;
            if (!cachedStage || !mapData) {
#if UNITY_EDITOR
                if (!Application.isPlaying) {
                    if (lastMapCheckTime == Time.time) {
                        return result;
                    }
                    lastMapCheckTime = Time.time;
                }
#endif
                if (!(mapData = FindFirstObjectByType<QuantumMapData>(FindObjectsInactive.Include))) {
                    return result;
                }
                cachedStage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(mapData.Asset.UserAsset);
                if (!cachedStage) {
                    return result;
                }
            }

            FPVector2 wrapped = QuantumUtils.WrapUnityTile(cachedStage, new FPVector2(result.x, result.y), out _);
            return new Vector3Int(wrapped.X.AsInt, wrapped.Y.AsInt, result.z);
        }
    }
}
