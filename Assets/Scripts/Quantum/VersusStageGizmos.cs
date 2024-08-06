using NSMB.Extensions;
using Photon.Deterministic;
using Quantum;
using System.Linq;
using UnityEngine;

public class VersusStageGizmos : MonoBehaviour {

    //---Serialized
    [SerializeField] private QuantumMapData mapData;

    public void OnValidate() {
        this.SetIfNull(ref mapData);
    }


    public void OnDrawGizmos() {
        var stage = QuantumUnityDB.GetGlobalAsset<VersusStageData>(mapData.Asset.UserAsset.Id);

        if (!stage) {
            return;
        }

        for (int i = 0; i < 10; i++) {
            Gizmos.color = new Color((float) i / 10, 0, 0, 0.75f);
            Gizmos.DrawCube(stage.GetWorldSpawnpointForPlayer(i, 10).ToUnityVector3() + Vector3.down * 0.25f, Vector2.one * 0.5f);
        }

        Vector2 cameraDimensions = (stage.CameraMaxPosition - stage.CameraMinPosition).ToUnityVector2();
        Vector2 cameraCenter = ((stage.CameraMaxPosition + stage.CameraMinPosition) / 2).ToUnityVector2();

        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(cameraCenter, cameraDimensions);

        Vector2 levelWorldOrigin = ((Vector2) stage.TileOrigin) * 0.5f + stage.TilemapWorldPosition.ToUnityVector2();
        Vector2 levelWorldDimensions = ((Vector2) stage.TileDimensions) * 0.5f;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(levelWorldOrigin + levelWorldDimensions / 2, levelWorldDimensions);

        Gizmos.color = Color.green;
        for (int x = 0; x < stage.TileDimensions.x; x++) {
            for (int y = 0; y < stage.TileDimensions.y; y++) {
                if (stage.TileData.Length <= (x + y * stage.TileDimensions.x)) {
                    break;
                }
                StageTileInstance ti = stage.TileData[x + y * stage.TileDimensions.x];
                StageTile t = QuantumUnityDB.GetGlobalAsset(ti.Tile);
                if (!t) {
                    continue;
                }
                FPVector2 worldPos = new FPVector2(((FP) x + stage.TileOrigin.x) / 2, ((FP) y + stage.TileOrigin.y) / 2) + stage.TilemapWorldPosition + (FPVector2.One / 4);
                FPVector2[][] polygons = ti.GetWorldPolygons(t, worldPos);

                foreach (FPVector2[] polygon in polygons) {
                    Gizmos.DrawLineStrip(polygon.Select(point => point.ToUnityVector3()).ToArray(), true);
                }
            }
        }

        Gizmos.color = new Color(1, 1, 0, 0.4f);
        foreach (GameObject starSpawn in GameObject.FindGameObjectsWithTag("StarSpawn")) {
            Gizmos.DrawCube(starSpawn.transform.position, Vector3.one);
            Gizmos.DrawIcon(starSpawn.transform.position, "star", true);
        }


        /* TODO
        for (int x = 0; x < levelWidthTile; x++) {
            for (int y = 0; y < levelHeightTile; y++) {
                Vector2Int loc = new(x + levelMinTileX, y + levelMinTileY);
                TileBase tile = tilemap.GetTile((Vector3Int) loc);

                if (tile is CoinTile) {
                    Gizmos.DrawIcon(Utils.Utils.TilemapToWorldPosition(loc, this) + OneFourth, "coin");
                } else if (tile is PowerupTile) {
                    Gizmos.DrawIcon(Utils.Utils.TilemapToWorldPosition(loc, this) + OneFourth, "powerup");
                }
            }
        }
        */
    }
}
