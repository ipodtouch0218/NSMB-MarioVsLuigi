using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.Tilemaps;

public class Utils {
    public static RaiseEventOptions EVENT_OTHERS {get;} = new RaiseEventOptions{Receivers=ReceiverGroup.Others};
    public static RaiseEventOptions EVENT_ALL {get;} = new RaiseEventOptions{Receivers=ReceiverGroup.All};
    public static Vector3Int WorldToTilemapPosition(Vector3 worldVec) {
        return GameManager.Instance.tilemap.WorldToCell(worldVec);
    }

    public static Vector3Int WorldToTilemapPosition(float worldX, float worldY) {
        return WorldToTilemapPosition(new Vector3(worldX, worldY));
    }

    public static Vector3 TilemapToWorldPosition(Vector3Int tileVec) {
        return GameManager.Instance.tilemap.CellToWorld(tileVec);
    }

    public static Vector3 TilemapToWorldPosition(int tileX, int tileY) {
        return TilemapToWorldPosition(new Vector3Int(tileX, tileY));
    }

    public static int GetCharacterIndex(Player player = null) {
        if (player == null) player = PhotonNetwork.LocalPlayer;
        object index;
        player.CustomProperties.TryGetValue("character", out index);
        if (index == null) index = 0;
        return (int) index;
    }

    public static PlayerData GetCharacterData(Player player = null) {
        return GlobalController.Instance.characters[GetCharacterIndex(player)];
    }

    public static TileBase GetTileAtTileLocation(Vector3Int tileLocation) {
        return GameManager.Instance.tilemap.GetTile(tileLocation);
    }
    public static TileBase GetTileAtWorldLocation(Vector3 worldLocation) {
        return GetTileAtTileLocation(WorldToTilemapPosition(worldLocation));
    }

    public static bool IsTileSolidAtTileLocation(Vector3Int tileLocation) {
        TileBase tile = GetTileAtTileLocation(tileLocation);
        if (tile is TileWithProperties)
            return !((TileWithProperties) tile).isBackgroundTile;
        return tile != null;
    } 
    public static bool IsTileSolidAtWorldLocation(Vector3 worldLocation) {
        return IsTileSolidAtTileLocation(WorldToTilemapPosition(worldLocation));
    } 


    public static Powerup[] powerups = null;
    public static Powerup GetRandomItem(int stars) {
        float starPercentage = (float) stars / GameManager.Instance.starRequirement;
        float totalChance = 0;
        if (powerups == null) {
            powerups = Resources.LoadAll<Powerup>("Scriptables/Powerups");
        }
        foreach (Powerup powerup in powerups) {
            totalChance += powerup.GetModifiedChance(starPercentage);
        }

        float rand = UnityEngine.Random.value * totalChance;
        foreach (Powerup powerup in powerups) {
            float chance = powerup.GetModifiedChance(starPercentage);
            if (rand < chance) {
                return powerup;
            } else {
                rand -= chance;
            }
        }

        return powerups[0];
    }
}