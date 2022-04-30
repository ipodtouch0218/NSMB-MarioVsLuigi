using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.Tilemaps;
using System.Linq;

public class Utils {
    public static RaiseEventOptions EVENT_OTHERS { get; } = new() { Receivers=ReceiverGroup.Others };
    public static RaiseEventOptions EVENT_ALL { get; } = new() { Receivers=ReceiverGroup.All };
    public static Vector3Int WorldToTilemapPosition(Vector3 worldVec, GameManager manager = null, bool wrap = true) {
        if (!manager) 
            manager = GameManager.Instance;
        Vector3Int tileLocation = manager.tilemap.WorldToCell(worldVec);
        if (wrap)
            WrapTileLocation(ref tileLocation, manager);

        return tileLocation;
    }

    public static bool WrapWorldLocation(ref Vector3 location, GameManager manager = null) {
        if (!manager)
            manager = GameManager.Instance;
        if (!manager.loopingLevel)
            return false;

        if (location.x < manager.GetLevelMinX()) {
            location.x += manager.levelWidthTile / 2;
            return true;
        } else if (location.x >= manager.GetLevelMaxX()) {
            location.x -= manager.levelWidthTile / 2;
            return true;
        }
        return false;
    }

    public static void WrapTileLocation(ref Vector3Int tileLocation, GameManager manager = null) {
        if (!manager) 
            manager = GameManager.Instance;
        if (tileLocation.x < manager.levelMinTileX)
            tileLocation.x += manager.levelWidthTile;
        if (tileLocation.x >= manager.levelMinTileX + manager.levelWidthTile)
            tileLocation.x -= manager.levelWidthTile;
    }

    public static Vector3Int WorldToTilemapPosition(float worldX, float worldY) {
        return WorldToTilemapPosition(new Vector3(worldX, worldY));
    }

    public static Vector3 TilemapToWorldPosition(Vector3Int tileVec, GameManager manager = null) {
        if (!manager)
            manager = GameManager.Instance;
        return manager.tilemap.CellToWorld(tileVec);
    }

    public static Vector3 TilemapToWorldPosition(int tileX, int tileY) {
        return TilemapToWorldPosition(new Vector3Int(tileX, tileY));
    }

    public static int GetCharacterIndex(Player player = null) {
        if (player == null) 
            player = PhotonNetwork.LocalPlayer;
        player.CustomProperties.TryGetValue(Enums.NetPlayerProperties.Character, out object index);
        if (index == null) 
            index = 0;
        return (int) index;
    }
    public static PlayerData GetCharacterData(Player player = null) {
        return GlobalController.Instance.characters[GetCharacterIndex(player)];
    }

    public static TileBase GetTileAtTileLocation(Vector3Int tileLocation) {
        WrapTileLocation(ref tileLocation);
        return GameManager.Instance.tilemap.GetTile(tileLocation);
    }
    public static TileBase GetTileAtWorldLocation(Vector3 worldLocation) {
        return GetTileAtTileLocation(WorldToTilemapPosition(worldLocation));
    }

    public static bool IsTileSolidAtTileLocation(Vector3Int tileLocation) {
        WrapTileLocation(ref tileLocation);
        Tile tile2 = GameManager.Instance.tilemap.GetTile<Tile>(tileLocation);
        if (tile2 && tile2.colliderType == Tile.ColliderType.Grid)
            return true;

        AnimatedTile animated = GameManager.Instance.tilemap.GetTile<AnimatedTile>(tileLocation);
        if (animated && animated.m_TileColliderType == Tile.ColliderType.Grid)
            return true;

        RuleTile ruleTile = GameManager.Instance.tilemap.GetTile<RuleTile>(tileLocation);
        if (ruleTile && ruleTile.m_DefaultColliderType == Tile.ColliderType.Grid)
            return true;

        return false;
    } 
    public static bool IsTileSolidAtWorldLocation(Vector3 worldLocation) {
        Collider2D collision = Physics2D.OverlapPoint(worldLocation, LayerMask.GetMask("Ground"));
        if (collision && !collision.isTrigger && !collision.CompareTag("Player"))
            return true;

        return IsTileSolidAtTileLocation(WorldToTilemapPosition(worldLocation));
    }

    public static void GetCustomProperty<T>(string key, out T value) {
        PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object temp);
        if (temp != null) {
            value = (T) temp;
        } else {
            value = default;
        }
    }

    public static Powerup[] powerups = null;
    public static Powerup GetRandomItem(int stars) {
        GameManager gm = GameManager.Instance;
        float starPercentage = (float) stars / gm.starRequirement;
        float totalChance = 0;

        if (powerups == null)
            powerups = Resources.LoadAll<Powerup>("Scriptables/Powerups");

        GetCustomProperty(Enums.NetRoomProperties.NewPowerups, out bool custom);
        bool big = gm.spawnBigPowerups;
        bool vertical = gm.spawnVerticalPowerups;

        foreach (Powerup powerup in powerups) {
            if (powerup.name == "MegaMushroom" && GameManager.Instance.musicState == Enums.MusicState.MegaMushroom)
                continue;
            if ((powerup.big && !big) || (powerup.vertical && !vertical) || (powerup.custom && !custom))
                continue;

            totalChance += powerup.GetModifiedChance(starPercentage);
        }

        float rand = Random.value * totalChance;
        foreach (Powerup powerup in powerups) {
            if (powerup.name == "MegaMushroom" && gm.musicState == Enums.MusicState.MegaMushroom)
                continue;
            if ((powerup.big && !big) || (powerup.vertical && !vertical) || (powerup.custom && !custom))
                continue;

            float chance = powerup.GetModifiedChance(starPercentage);

            if (rand < chance) 
                return powerup;
            rand -= chance;
        }

        return powerups[0];
    }
    public static float QuadraticEaseOut(float v) {
        return -1 * v * (v - 2);
    }

    public static ExitGames.Client.Photon.Hashtable GetTilemapChanges(TileBase[] original, BoundsInt bounds, Tilemap tilemap) {
        Dictionary<int, int> changes = new();
        List<string> tiles = new();

        TileBase[] current = tilemap.GetTilesBlock(bounds);

        for (int i = 0; i < original.Length; i++) {
            if (current[i] == original[i])
                continue;

            TileBase cTile = current[i];
            string path;
            if (cTile == null) {
                path = "";
            } else {
                path = ResourceDB.GetAsset(cTile.name).ResourcesPath;
            }

            if (!tiles.Contains(path))
                tiles.Add(path);
            
            changes[i] = tiles.IndexOf(path);
        }

        return new() {
            ["T"] = tiles.ToArray(),
            ["C"] = changes,
        };
    }

    public static void ApplyTilemapChanges(TileBase[] original, BoundsInt bounds, Tilemap tilemap, ExitGames.Client.Photon.Hashtable changesTable) {
        TileBase[] copy = (TileBase[]) original.Clone();

        Dictionary<int, int> changes = (Dictionary<int, int>) changesTable["C"];
        string[] tiles = (string[]) changesTable["T"];

        foreach (KeyValuePair<int, int> pairs in changes) {
            copy[pairs.Key] = GetTileFromCache(tiles[pairs.Value]);
        }

        tilemap.SetTilesBlock(bounds, copy);
    }

    private static readonly Dictionary<string, TileBase> tileCache = new();
    public static TileBase GetTileFromCache(string tilename) {
        if (tilename == null || tilename == "")
            return null;
        
        if (!tilename.StartsWith("Tilemaps/Tiles/"))
            tilename = "Tilemaps/Tiles/" + tilename;

        return tileCache.ContainsKey(tilename) ?
            tileCache[tilename] :
            tileCache[tilename] = Resources.Load(tilename) as TileBase;
    }
}