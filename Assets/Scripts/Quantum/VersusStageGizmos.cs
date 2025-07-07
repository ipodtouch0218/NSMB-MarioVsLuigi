using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using System;
using UnityEngine;

namespace NSMB.Quantum {
    public class VersusStageGizmos : MonoBehaviour {

#if UNITY_EDITOR

        private static readonly Vector3[] UnityVertexBuffer = new Vector3[128];
        private static readonly FPVector2[] VertexBuffer = new FPVector2[128];
        private static readonly int[] ShapeVertexCountBuffer = new int[16];

        //---Serialized
        [SerializeField] private QuantumMapData mapData;

        public void OnValidate() {
            this.SetIfNull(ref mapData);
        }

        public unsafe void OnDrawGizmos() {
            if (!mapData) {
                return;
            }

            var stage = QuantumUnityDB.GetGlobalAsset<VersusStageData>(mapData.Asset.UserAsset.Id);

            if (!stage) {
                return;
            }

            for (int i = 0; i < 10; i++) {
                Gizmos.color = new Color((float) i / 10, 0, 0, 0.75f);
                Gizmos.DrawCube(stage.GetWorldSpawnpointForPlayer(i, 10).ToUnityVector3() + Vector3.up * 0.25f, Vector2.one * 0.5f);
            }

            Vector2 cameraDimensions = (stage.CameraMaxPosition - stage.CameraMinPosition).ToUnityVector2();
            Vector2 cameraCenter = ((stage.CameraMaxPosition + stage.CameraMinPosition) / 2).ToUnityVector2();

            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(cameraCenter, cameraDimensions);

            Vector2 levelWorldOrigin = ((Vector2) stage.TileOrigin.ToVector2Int()) * 0.5f + stage.TilemapWorldPosition.ToUnityVector2();
            Vector2 levelWorldDimensions = ((Vector2) stage.TileDimensions.ToVector2Int()) * 0.5f;

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(levelWorldOrigin + levelWorldDimensions / 2, levelWorldDimensions);

            for (int x = 0; x < stage.TileDimensions.X; x++) {
                for (int y = 0; y < stage.TileDimensions.Y; y++) {
                    if (stage.TileData.Length <= (x + y * stage.TileDimensions.X)) {
                        break;
                    }
                    StageTileInstance tile = stage.TileData[x + y * stage.TileDimensions.X];
                    StageTile stageTile = QuantumUnityDB.GetGlobalAsset(tile.Tile);
                    if (!stageTile) {
                        continue;
                    }
                    StageTile originalTile = stageTile;
                    FPVector2 worldPos = QuantumUtils.RelativeTileToWorldRounded(stage, new IntVector2(x, y));
                    if (stageTile is TileInteractionRelocator tir) {
                        tile = stage.TileData[tir.RelocateTo.X + tir.RelocateTo.Y * stage.TileDimensions.X];
                        stageTile = QuantumUnityDB.GetGlobalAsset(tile.Tile);
                    }

                    if (stageTile) {
                        if (stageTile.CollisionData.IsFullTile) {
                            Gizmos.color = Color.red;
                            Vector3 worldPosUnity = worldPos.ToUnityVector3();
                            Gizmos.DrawLineStrip(new Vector3[] {
                            worldPosUnity + new Vector3(-0.25f,  0.25f),
                            worldPosUnity + new Vector3( 0.25f,  0.25f),
                            worldPosUnity + new Vector3( 0.25f, -0.25f),
                            worldPosUnity + new Vector3(-0.25f, -0.25f)
                        }, true);
                        } else {
                            Gizmos.color = Color.green;
                            tile.GetWorldPolygons((FrameThreadSafe) null, stage, stageTile, VertexBuffer, ShapeVertexCountBuffer, worldPos);

                            int shapeIndex = 0;
                            int vertexIndex = 0;
                            int shapeVertexCount;
                            while ((shapeVertexCount = ShapeVertexCountBuffer[shapeIndex++]) > 0) {
                                for (int i = 0; i < shapeVertexCount; i++) {
                                    UnityVertexBuffer[i] = VertexBuffer[vertexIndex + i].ToUnityVector3();
                                }

                                Gizmos.DrawLineStrip(UnityVertexBuffer.AsSpan(0, shapeVertexCount), stageTile.IsPolygon);
                                vertexIndex += shapeVertexCount;
                            }
                        }
                    }

                    if (stageTile is CoinTile) {
                        Gizmos.DrawIcon(worldPos.ToUnityVector3(), "Coin");
                    } else if (stageTile is PowerupTile) {
                        Gizmos.DrawIcon(worldPos.ToUnityVector3(), "Powerup");
                    } else if (originalTile is TileInteractionRelocator tir2) {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawLine(worldPos.ToUnityVector3(), QuantumUtils.RelativeTileToWorldRounded(stage, tir2.RelocateTo).ToUnityVector3());
                    }
                }
            }

            var game = QuantumRunner.DefaultGame;
            Frame f;
            foreach (GameObject starSpawn in GameObject.FindGameObjectsWithTag("StarSpawn")) {
                Gizmos.color = new Color(0, 1, 0, 0.4f);
                if (game != null) {
                    f = game.Frames.Predicted;
                    int index = Array.IndexOf(stage.BigStarSpawnpoints, starSpawn.transform.position.ToRoundedFPVector2());
                    if (index != -1) {
                        if (f.Global->UsedStarSpawns.IsSet(index)) {
                            Gizmos.color = new Color(1, 0, 0, 0.4f);
                        }
                    }
                }
                Gizmos.DrawCube(starSpawn.transform.position, Vector3.one);
                Gizmos.DrawWireSphere(starSpawn.transform.position, 2);
                Gizmos.DrawIcon(starSpawn.transform.position, "star", true);
            }
        }

#endif
    }
}
