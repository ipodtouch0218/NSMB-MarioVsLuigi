using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.Tilemaps;

public class Utils {

    public static int FirstPlaceStars {
        get {
            return GameManager.Instance.allPlayers.Max(pc => pc.stars);
        }
    }


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

        GetCustomProperty(Enums.NetPlayerProperties.Character, out int index, player.CustomProperties);
        return index;
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
    public static bool IsAnyTileSolidBetweenWorldBox(Vector2 checkPosition, Vector2 checkSize) {
        Vector3Int minPos = WorldToTilemapPosition(checkPosition - (checkSize / 2), wrap: false);
        Vector3Int size = WorldToTilemapPosition(checkPosition + (checkSize / 2), wrap: false) - minPos;

        Debug.Log("size" + size);

        for (int x = 0; x <= size.x; x++) {
            for (int y = 0; y <= size.y; y++) {

                Vector3Int tileLocation = new(minPos.x + x, minPos.y + y, 0);
                WrapTileLocation(ref tileLocation);

                if (IsTileSolidAtTileLocation(tileLocation))
                    return true;
            }
        }
        return false;
    }

    public static float WrappedDistance(Vector2 a, Vector2 b) {
        if (GameManager.Instance && GameManager.Instance.loopingLevel && Mathf.Abs(a.x - b.x) > GameManager.Instance.levelWidthTile / 4f) 
            a.x -= GameManager.Instance.levelWidthTile / 2f * Mathf.Sign(a.x - b.x);

        return Vector2.Distance(a, b);
    }

    public static void GetCustomProperty<T>(string key, out T value, ExitGames.Client.Photon.Hashtable properties = null) {
        if (properties == null)
            properties = PhotonNetwork.CurrentRoom.CustomProperties;

        properties.TryGetValue(key, out object temp);
        if (temp != null) {
            value = (T) temp;
        } else {
            value = default;
        }
    }

    public static Powerup[] powerups = null;
    public static Powerup GetRandomItem(PlayerController player) {
        GameManager gm = GameManager.Instance;

        // "losing" variable based on ln(x+1), x being the # of stars we're behind (max being 5)
        int x = Mathf.Max(0, FirstPlaceStars - player.stars);
        float losing = Mathf.Log(x + 1, 2.71828f);

        if (powerups == null)
            powerups = Resources.LoadAll<Powerup>("Scriptables/Powerups");

        GetCustomProperty(Enums.NetRoomProperties.NewPowerups, out bool custom);
        bool big = gm.spawnBigPowerups;
        bool vertical = gm.spawnVerticalPowerups;

        float totalChance = 0;
        foreach (Powerup powerup in powerups) {
            if (powerup.name == "MegaMushroom" && GameManager.Instance.musicState == Enums.MusicState.MegaMushroom)
                continue;
            if ((powerup.big && !big) || (powerup.vertical && !vertical) || (powerup.custom && !custom))
                continue;

            totalChance += powerup.GetModifiedChance(losing);
        }

        float rand = Random.value * totalChance;
        foreach (Powerup powerup in powerups) {
            if (powerup.name == "MegaMushroom" && gm.musicState == Enums.MusicState.MegaMushroom)
                continue;
            if ((powerup.big && !big) || (powerup.vertical && !vertical) || (powerup.custom && !custom))
                continue;

            float chance = powerup.GetModifiedChance(losing);

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

    private static readonly Dictionary<char, int> charToSymbolIndex = new() {
        ['c'] = 6,
        ['0'] = 11,
        ['1'] = 12,
        ['2'] = 13,
        ['3'] = 14,
        ['4'] = 15,
        ['5'] = 16,
        ['6'] = 17,
        ['7'] = 18,
        ['8'] = 19,
        ['9'] = 20,
        ['x'] = 21,
        ['C'] = 22,
        ['S'] = 23,
        ['/'] = 24,
        [':'] = 25,
    };
    public static string GetSymbolString(string str) {
        string ret = "";
        foreach (char c in str) {
            if (charToSymbolIndex.ContainsKey(c)) {
                ret += "<sprite=" + charToSymbolIndex[c] + ">";
            } else {
                ret += c;
            }
        }
        return ret;
    }

    public static Color GetPlayerColor(Player player, float s = 1, float v = 1) {
        int id = System.Array.IndexOf(PhotonNetwork.PlayerList, player);
        return Color.HSVToRGB(id / ((float) PhotonNetwork.PlayerList.Length + 1), s, v);
    }

    public static void TickTimer(ref float counter, float min, float delta, float max = float.MaxValue) {
        counter = Mathf.Clamp(counter - delta, min, max);
    }
}