using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;

namespace NSMB.Utils {
    public class Utils {

        //---Buffers
        private static readonly List<Vector2> PhysicsShapeBuffer = new(8);
        private static readonly Vector2[] BoxPointsBuffer = new Vector2[4];

        public static bool BitTest(long v, int index) {
            return (v & (1L << index)) != 0;
        }

        public static bool BitTest(ulong v, int index) {
            return (v & (1UL << index)) != 0;
        }

        public static void BitSet(ref byte v, int index, bool value) {
            if (value) {
                v |= (byte) (1 << index);
            } else {
                v &= (byte) ~(1 << index);
            }
        }

        public static void BitSet(ref int v, int index, bool value) {
            if (value) {
                v |= (1 << index);
            } else {
                v &= ~(1 << index);
            }
        }

        public static void BitSet(ref uint v, int index, bool value) {
            if (value) {
                v |= (1U << index);
            } else {
                v &= ~(1U << index);
            }
        }

        public static void BitSet(ref ulong v, int index, bool value) {
            if (value) {
                v |= (1UL << index);
            } else {
                v &= ~(1UL << index);
            }
        }


        public static Vector2Int WorldToTilemapPosition(Vector2 worldVec, GameManager gm = null, bool wrap = true) {
            if (!gm) {
                gm = GameManager.Instance;
            }

            Vector2Int tileLocation = (Vector2Int) gm.tilemap.WorldToCell(worldVec);
            if (wrap) {
                WrapTileLocation(ref tileLocation, gm);
            }

            return tileLocation;
        }

        public static Vector2 WrapWorldLocation(Vector2 location, GameManager gm = null) {
            if (!gm) {
                gm = GameManager.Instance;
            }

            if (!gm.loopingLevel) {
                return location;
            }

            if (location.x < gm.LevelMinX) {
                location.x += gm.LevelWidth;
                return location;
            } else if (location.x >= gm.LevelMaxX) {
                location.x -= gm.LevelWidth;
                return location;
            }
            return location;
        }

        public static bool WrapWorldLocation(ref Vector2 location, GameManager gm = null) {
            if (!gm) {
                gm = GameManager.Instance;
            }

            if (!gm.loopingLevel) {
                return false;
            }

            if (location.x < gm.LevelMinX) {
                location.x += gm.LevelWidth;
                return true;
            } else if (location.x >= gm.LevelMaxX) {
                location.x -= gm.LevelWidth;
                return true;
            }
            return false;
        }

        public static bool WrapWorldLocation(ref Vector3 location, GameManager gm = null) {
            if (!gm) {
                gm = GameManager.Instance;
            }

            if (!gm || !gm.loopingLevel) {
                return false;
            }

            if (location.x < gm.LevelMinX) {
                location.x += gm.LevelWidth;
                return true;
            } else if (location.x >= gm.LevelMaxX) {
                location.x -= gm.LevelWidth;
                return true;
            }
            return false;
        }

        public static int WrappedDirectionSign(Vector2 a, Vector2 b, GameManager gm = null) {
            if (!gm) {
                gm = GameManager.Instance;
            }

            if (!gm.loopingLevel) {
                return a.x > b.x ? 1 : -1;
            }

            if (Mathf.Abs(a.x - b.x) > GameManager.Instance.LevelWidth * 0.5f) {
                return (a.x < b.x) ? 1 : -1;
            } else {
                return (a.x > b.x) ? 1 : -1;
            }
        }

        public static void WrapTileLocation(ref Vector3Int tileLocation, GameManager gm = null) {
            if (!gm) {
                gm = GameManager.Instance;
            }

            if (!gm.loopingLevel) {
                return;
            }

            if (tileLocation.x < gm.levelMinTileX) {
                tileLocation.x += gm.levelWidthTile;
            } else if (tileLocation.x >= gm.levelMinTileX + gm.levelWidthTile) {
                tileLocation.x -= gm.levelWidthTile;
            }
        }

        public static void UnwrapLocations(Vector2 a, Vector2 b, out Vector2 newA, out Vector2 newB, GameManager gm = null) {
            if (!gm) {
                gm = GameManager.Instance;
            }

            newA = a;
            newB = b;

            if (!gm.loopingLevel) {
                return;
            }

            if (Mathf.Abs(newA.x - newB.x) > gm.LevelWidth * 0.5f) {
                newB.x += gm.LevelWidth * (newB.x > gm.LevelMiddleX ? -1 : 1);
            }
        }

        public static void WrapTileLocation(ref Vector2Int tileLocation, GameManager manager = null) {
            if (!manager) {
                manager = GameManager.Instance;
            }

            if (!manager.loopingLevel) {
                return;
            }

            if (tileLocation.x < manager.levelMinTileX) {
                tileLocation.x += manager.levelWidthTile;
            } else if (tileLocation.x >= manager.levelMinTileX + manager.levelWidthTile) {
                tileLocation.x -= manager.levelWidthTile;
            }
        }

        public static Vector3 TilemapToWorldPosition(Vector2Int tileVec, GameManager manager = null) {
            if (!manager) {
                manager = GameManager.Instance;
            }

            return manager.tilemap.CellToWorld((Vector3Int) tileVec);
        }

        public static TileBase GetTileAtTileLocation(Vector2Int tileLocation) {
            WrapTileLocation(ref tileLocation);
            return GameManager.Instance.tileManager.GetTile(tileLocation);
        }
        public static TileBase GetTileAtWorldLocation(Vector2 worldLocation) {
            return GetTileAtTileLocation(WorldToTilemapPosition(worldLocation));
        }

        public static bool IsTileSolidAtTileLocation(Vector2Int tileLocation) {
            WrapTileLocation(ref tileLocation);
            return GetColliderType(tileLocation) == Tile.ColliderType.Grid;
        }

        private static Tile.ColliderType GetColliderType(Vector2Int tileLocation) {

            TileBase tileBase = GetTileAtTileLocation(tileLocation);

            if (tileBase is AnimatedTile aTile) {
                return aTile.m_TileColliderType;
            }

            if (tileBase is RuleTile rTile) {
                return rTile.m_DefaultColliderType;
            }

            if (tileBase is Tile tTile) {
                return tTile.colliderType;
            }

            return Tile.ColliderType.None;
        }

        public static bool IsTileSolidBetweenWorldBox(Vector2Int tileLocation, Vector2 worldLocation, Vector2 worldBox, bool boxcast = true, GameManager gm = null) {
            if (boxcast) {
                bool hitTriggers = Physics2D.queriesHitTriggers;
                Physics2D.queriesHitTriggers = false;
                Collider2D collision = Physics2D.OverlapPoint(worldLocation, Layers.MaskSolidGround);
                Physics2D.queriesHitTriggers = hitTriggers;

                if (collision) {
                    return true;
                }
            }

            if (!gm) {
                gm = GameManager.Instance;
            }

            Vector2 ogWorldLocation = worldLocation;
            while (GetTileAtTileLocation(tileLocation) is TileInteractionRelocator it) {
                worldLocation += (Vector2) it.offset * 0.5f;
                tileLocation += it.offset;
            }

            Matrix4x4 tileTransform = gm.tilemap.GetTransformMatrix((Vector3Int) tileLocation);

            Vector2 halfBox = worldBox * 0.5f;

            BoxPointsBuffer[0] = ogWorldLocation + Vector2.up * halfBox + Vector2.right * halfBox;   // ++
            BoxPointsBuffer[1] = ogWorldLocation + Vector2.up * halfBox + Vector2.left * halfBox;    // +-
            BoxPointsBuffer[2] = ogWorldLocation + Vector2.down * halfBox + Vector2.left * halfBox;  // --
            BoxPointsBuffer[3] = ogWorldLocation + Vector2.down * halfBox + Vector2.right * halfBox; // -+

            Sprite sprite = gm.tilemap.GetSprite((Vector3Int) tileLocation);
            switch (GetColliderType(tileLocation)) {
            case Tile.ColliderType.Grid:
                return true;
            case Tile.ColliderType.None:
                return false;
            case Tile.ColliderType.Sprite:

                for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++) {
                    sprite.GetPhysicsShape(i, PhysicsShapeBuffer);

                    for (int j = 0; j < PhysicsShapeBuffer.Count; j++) {
                        Vector2 point = PhysicsShapeBuffer[j];
                        point *= 0.5f;
                        point = tileTransform.MultiplyPoint(point);
                        point += (Vector2) tileLocation * 0.5f;
                        point += (Vector2) gm.tilemap.transform.position;
                        point += Vector2.one * 0.25f;
                        PhysicsShapeBuffer[j] = point;
                    }

                    if (DoPolygonsOverlap(PhysicsShapeBuffer, BoxPointsBuffer)) {

                        for (int j = 0; j < BoxPointsBuffer.Length; j++) {
                            Debug.DrawLine(BoxPointsBuffer[j], BoxPointsBuffer[(j + 1) % BoxPointsBuffer.Length], Color.red);
                        }

                        for (int j = 0; j < PhysicsShapeBuffer.Count; j++) {
                            Debug.DrawLine(PhysicsShapeBuffer[j], PhysicsShapeBuffer[(j + 1) % PhysicsShapeBuffer.Count], Color.green);
                        }

                        return true;
                    }
                }
                return false;
            }

            return IsTileSolidAtTileLocation(WorldToTilemapPosition(worldLocation));
        }

        // https://stackoverflow.com/questions/10635040/how-to-detect-overlapping-polygons
        private static bool DoPolygonsOverlap(IList<Vector2> firstPolygon, IList<Vector2> secondPolygon) {
            foreach (var item in firstPolygon) {
                if (IsPointInPolygon(secondPolygon, item)) {
                    return true;
                }
            }
            foreach (var item in secondPolygon) {
                if (IsPointInPolygon(firstPolygon, item)) {
                    return true;
                }
            }
            return false;
        }

        private static bool IsPointInPolygon(IList<Vector2> polygon, Vector2 testPoint) {
            bool result = false;
            int j = polygon.Count() - 1;
            for (int i = 0; i < polygon.Count(); i++) {
                if (polygon[i].y < testPoint.y && polygon[j].y >= testPoint.y || polygon[j].y < testPoint.y && polygon[i].y >= testPoint.y) {
                    if (polygon[i].x + (testPoint.y - polygon[i].y) / (polygon[j].y - polygon[i].y) * (polygon[j].x - polygon[i].x) < testPoint.x) {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        public static bool IsTileSolidAtWorldLocation(Vector2 worldLocation) {
            Collider2D collision = NetworkHandler.Runner.GetPhysicsScene2D().OverlapPoint(worldLocation, Layers.MaskSolidGround);
            if (collision && !collision.isTrigger && !collision.CompareTag("Player")) {
                return true;
            }

            while (GetTileAtWorldLocation(worldLocation) is TileInteractionRelocator it) {
                worldLocation += (Vector2) it.offset * 0.5f;
            }

            Vector2Int tileLocation = WorldToTilemapPosition(worldLocation);
            Matrix4x4 tileTransform = GameManager.Instance.tilemap.GetTransformMatrix((Vector3Int) tileLocation);

            Sprite sprite = GameManager.Instance.tilemap.GetSprite((Vector3Int) tileLocation);
            switch (GetColliderType(tileLocation)) {
            case Tile.ColliderType.Grid:
                return true;
            case Tile.ColliderType.None:
                return false;
            case Tile.ColliderType.Sprite:
                for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++) {
                    sprite.GetPhysicsShape(i, PhysicsShapeBuffer);

                    for (int j = 0; j < PhysicsShapeBuffer.Count; j++) {
                        Vector2 point = PhysicsShapeBuffer[j];
                        point *= 0.5f;
                        point = tileTransform.MultiplyPoint(point);
                        point += (Vector2) tileLocation * 0.5f;
                        point += (Vector2) GameManager.Instance.tilemap.transform.position;
                        point += Vector2.one * 0.25f;
                        PhysicsShapeBuffer[j] = point;
                    }

                    if (IsPointInPolygon(PhysicsShapeBuffer, worldLocation)) {
                        return true;
                    }
                }
                return false;
            }

            return IsTileSolidAtTileLocation(WorldToTilemapPosition(worldLocation));
        }

        public static bool IsAnyTileSolidBetweenWorldBox(Vector2 checkPosition, Vector2 checkSize, bool boxcast = true) {
            Vector2Int minPos = WorldToTilemapPosition(checkPosition - (checkSize * 0.5f), wrap: false);
            Vector2Int size = WorldToTilemapPosition(checkPosition + (checkSize * 0.5f), wrap: false) - minPos;

            for (int x = 0; x <= size.x; x++) {
                for (int y = 0; y <= size.y; y++) {

                    Vector2Int tileLocation = new(minPos.x + x, minPos.y + y);
                    WrapTileLocation(ref tileLocation);

                    if (IsTileSolidBetweenWorldBox(tileLocation, checkPosition, checkSize, boxcast)) {
                        return true;
                    }
                }
            }
            return false;
        }

        public static float WrappedDistance(Vector2 a, Vector2 b) {
            GameManager gm = GameManager.Instance;
            if (gm && gm.loopingLevel && Mathf.Abs(a.x - b.x) > gm.LevelWidth * 0.5f) {
                a.x -= gm.LevelWidth * Mathf.Sign(a.x - b.x);
            }

            return Vector2.Distance(a, b);
        }

        public static float WrappedDistance(Vector2 a, Vector2 b, out float xDifference) {
            GameManager gm = GameManager.Instance;
            if (gm && gm.loopingLevel && Mathf.Abs(a.x - b.x) > gm.LevelWidth * 0.5f) {
                a.x -= gm.LevelWidth * Mathf.Sign(a.x - b.x);
            }

            xDifference = a.x - b.x;
            return Vector2.Distance(a, b);
        }

        // MAX(0,$B15+(IF(stars behind >0,LOG(B$1+1, 2.71828),0)*$C15*(1-(($M$15-$M$14))/$M$15)))
        public static PowerupScriptable GetRandomItem(PlayerController player) {
            PowerupScriptable[] powerups = ScriptableManager.Instance.powerups;
            GameManager gm = GameManager.Instance;

            // "Losing" variable based on ln(x+1), x being the # of stars we're behind
            gm.teamManager.GetTeamStars(player.Data.Team, out int ourStars);
            int leaderStars = gm.teamManager.GetFirstPlaceStars();

            int starsToWin = SessionData.Instance.StarRequirement;
            bool custom = SessionData.Instance.CustomPowerups;
            bool lives = SessionData.Instance.Lives > 0;


            bool big = gm.spawnBigPowerups;
            bool vertical = gm.spawnVerticalPowerups;

            bool cantSpawnMega = GameManager.Instance.AlivePlayers.Any(pc => pc.State == Enums.PowerupState.MegaMushroom);

            float totalChance = 0;
            foreach (PowerupScriptable powerup in powerups) {
                if (powerup.state == Enums.PowerupState.MegaMushroom && cantSpawnMega) {
                    continue;
                }

                if ((powerup.big && !big) || (powerup.vertical && !vertical) || (powerup.custom && !custom) || (powerup.lives && !lives)) {
                    continue;
                }

                totalChance += powerup.GetModifiedChance(starsToWin, leaderStars, ourStars);
            }

            float rand = GameManager.Instance.random.NextSingleExclusive() * totalChance;
            foreach (PowerupScriptable powerup in powerups) {
                if (powerup.state == Enums.PowerupState.MegaMushroom && cantSpawnMega) {
                    continue;
                }

                if ((powerup.big && !big) || (powerup.vertical && !vertical) || (powerup.custom && !custom) || (powerup.lives && !lives)) {
                    continue;
                }

                float chance = powerup.GetModifiedChance(starsToWin, leaderStars, ourStars);

                if (rand < chance) {
                    return powerup;
                }

                rand -= chance;
            }

            return powerups[1];
        }

        public static float QuadraticEaseOut(float v) {
            return -1 * v * (v - 2);
        }

        private static readonly Dictionary<char, string> uiSymbols = new() {
            ['0'] = "hudnumber_0",
            ['1'] = "hudnumber_1",
            ['2'] = "hudnumber_2",
            ['3'] = "hudnumber_3",
            ['4'] = "hudnumber_4",
            ['5'] = "hudnumber_5",
            ['6'] = "hudnumber_6",
            ['7'] = "hudnumber_7",
            ['8'] = "hudnumber_8",
            ['9'] = "hudnumber_9",
            ['x'] = "hudnumber_x",
            ['C'] = "hudnumber_coin",
            ['S'] = "hudnumber_star",
            ['T'] = "hudnumber_timer",
            ['/'] = "hudnumber_slash",
            [':'] = "hudnumber_colon",
        };
        public static readonly Dictionary<char, string> numberSymbols = new() {
            ['0'] = "coinnumber_0",
            ['1'] = "coinnumber_1",
            ['2'] = "coinnumber_2",
            ['3'] = "coinnumber_3",
            ['4'] = "coinnumber_4",
            ['5'] = "coinnumber_5",
            ['6'] = "coinnumber_6",
            ['7'] = "coinnumber_7",
            ['8'] = "coinnumber_8",
            ['9'] = "coinnumber_9",
        };
        public static readonly Dictionary<char, string> smallSymbols = new() {
            ['0'] = "room_smallnumber_0",
            ['1'] = "room_smallnumber_1",
            ['2'] = "room_smallnumber_2",
            ['3'] = "room_smallnumber_3",
            ['4'] = "room_smallnumber_4",
            ['5'] = "room_smallnumber_5",
            ['6'] = "room_smallnumber_6",
            ['7'] = "room_smallnumber_7",
            ['8'] = "room_smallnumber_8",
            ['9'] = "room_smallnumber_9",
        };
        public static string GetSymbolString(string str, Dictionary<char, string> dict = null) {
            dict ??= uiSymbols;

            StringBuilder ret = new();
            foreach (char c in str) {
                if (dict.TryGetValue(c, out string name)) {
                    ret.Append("<sprite name=").Append(name).Append(">");
                } else {
                    ret.Append(c);
                }
            }
            return ret.ToString();
        }

        private static readonly Color spectatorColor = new(0.9f, 0.9f, 0.9f, 0.7f);
        public static Color GetPlayerColor(PlayerRef player, float s = 1, float v = 1) {
            if (!player.IsRealPlayer) {
                return spectatorColor;
            }

            return GetPlayerColor(player.GetPlayerData(), s, v);
        }

        public static Color GetPlayerColor(PlayerData player, float s = 1, float v = 1) {

            // Prioritize spectator status
            if (!player || player.IsManualSpectator || player.IsCurrentlySpectating) {
                return spectatorColor;
            }

            // Then teams
            if (SessionData.Instance && SessionData.Instance.Teams && player.Team >= 0 && player.Team < ScriptableManager.Instance.teams.Length) {
                return GetTeamColor(player.Team, s, v);
            }

            // Then id based color
            int result = -1;
            int count = 0;
            var players = SessionData.Instance.PlayerDatas.OrderBy(pd => pd.Value.JoinTick);

            foreach ((_, PlayerData playerData) in players) {
                // Skip spectators in color calculations
                if (playerData.IsManualSpectator || playerData.IsCurrentlySpectating) {
                    continue;
                }

                if (playerData == player) {
                    result = count;
                }

                count++;
            }

            if (result == -1) {
                return spectatorColor;
            }

            return Color.HSVToRGB(result / ((float) count + 1), s, v);
        }

        public static Color GetTeamColor(int team, float s = 1, float v = 1) {
            if (team < 0 || team >= ScriptableManager.Instance.teams.Length) {
                return spectatorColor;
            }

            Color color = ScriptableManager.Instance.teams[team].color;
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            return Color.HSVToRGB(hue, saturation * s, value * v);
        }

        public static string GetPingSymbol(int ping) {
            string pingSymbol;
            if (ping < 0) {
                pingSymbol = "<sprite name=connection_disconnected>";
            } else if (ping == 0) {
                pingSymbol = "<sprite name=connection_host>";
            } else if (ping < 60) {
                pingSymbol = "<sprite name=connection_great>";
            } else if (ping < 110) {
                pingSymbol = "<sprite name=connection_good>";
            } else if (ping < 150) {
                pingSymbol = "<sprite name=connection_fair>";
            } else {
                pingSymbol = "<sprite name=connection_bad>";
            }
            return pingSymbol;
        }

        public static Color GetRainbowColor(NetworkRunner runner) {
            // Four seconds per revolution
            if (!runner) {
                return Color.white;
            }

            double time = (runner.LocalRenderTime * 0.25d) % 1d;
            return GlobalController.Instance.rainbowGradient.Evaluate((float) time);
        }

        public static int ParseTimeToSeconds(string time) {
            int minutes;
            int seconds;

            if (time.Contains(":")) {
                string[] split = time.Split(":");
                int.TryParse(split[0], out minutes);
                int.TryParse(split[1], out seconds);
            } else {
                minutes = 0;
                int.TryParse(time, out seconds);
            }

            if (seconds >= 60) {
                minutes += seconds / 60;
                seconds %= 60;
            }

            seconds = minutes * 60 + seconds;

            return seconds;
        }

        public static bool BufferContains<T>(T[] buffer, int bufferLength, T element) {
            for (int i = 0; i < bufferLength; i++) {
                if (element.Equals(buffer[i])) {
                    return true;
                }
            }
            return false;
        }

        public static void IntersectWithBuffer<T>(IList<T> collection, T[] buffer, int bufferLength) {
            for (int i = collection.Count - 1; i >= 0; i--) {
                if (!BufferContains(buffer, bufferLength, collection[i])) {
                    collection.RemoveAt(i);
                }
            }
        }
    }
}
