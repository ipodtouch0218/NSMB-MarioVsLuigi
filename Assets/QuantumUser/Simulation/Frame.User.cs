using Miniscript;
using System;

namespace Quantum {
    public unsafe partial class Frame {

        public StageTileInstance[] StageTiles;
        public Interpreter MiniscriptInterpreter;

        partial void InitUser() {
            StageTiles = Array.Empty<StageTileInstance>();
            
            string script = @"
coinEntities = []

update = function()
    // Add a coin when a player hits space
    for i in range(0,9)
        playerData = frame.getPlayerData(i)
        if playerData == null then
            continue
        end if
            
        if playerData.inputs.jump.wasPressed then
            coin = frame.findAsset(""QuantumUser/Resources/EntityPrototypes/Coins/EnemyCoinEntityPrototype"")
            globals.coinEntities.push(frame.create(coin))
        end if
    end for

    // Remove coins that despawned
    toRemove = []
    for i in globals.coinEntities.indexes
        if not frame.exists(globals.coinEntities[i]) then
            toRemove.push(i)
        end if
    end for
    removed = 0
    for i in toRemove
        globals.coinEntities.remove(i - removed)
        removed += 1
    end for

    // Move coins in circles
    count = 0
    for coin in globals.coinEntities
        coinPhysicsObject = frame.get(coin, ""PhysicsObject"")
        coinPhysicsObject.disableCollision = true

        coinTransform = frame.get(coin, ""Transform2D"")
        rad = frame.number * 2 * pi / 120
        rad += count * (pi / 8)
        count += 1
        coinTransform.position.x = -14 + sin(rad)
        coinTransform.position.y = 1.5 + cos(rad)
    end for
end function
";
            script += @"
_events = []
while true
    if _events.len > 0 then
        _nextEvent = _events.pull
        _nextEvent
    end if
    yield
end while";
            string debug = "", error = "";
            MiniscriptInterpreter = new(script, 
                (str, ln) => {
                    debug += str;
                    if (ln) {
                        UnityEngine.Debug.Log(str);
                        str = "";
                    }
                },
                (str, ln) => {
                    error += str;
                    if (ln) {
                        UnityEngine.Debug.LogError(str);
                        str = "";
                    }
                });
            MiniscriptInterpreter.Compile();
        }

        partial void SerializeUser(FrameSerializer serializer) {
            // Possible desync fix?
            StageTiles ??= Array.Empty<StageTileInstance>();

            serializer.Stream.SerializeArrayLength(ref StageTiles);
            for (int i = 0; i < StageTiles.Length; i++) {
                ref StageTileInstance tile = ref StageTiles[i];
                serializer.Stream.Serialize(ref tile.Tile);

                if (serializer.Stream.Writing) {
                    serializer.Stream.WriteByte((byte) tile.Flags);
                    serializer.Stream.WriteByte((byte) (tile.Rotation >> 8));
                    serializer.Stream.WriteByte((byte) tile.Rotation);
                } else {
                    tile.Flags = (StageTileFlags) serializer.Stream.ReadByte();
                    tile.Rotation = (ushort) (serializer.Stream.ReadByte() << 8);
                    tile.Rotation |= serializer.Stream.ReadByte();
                }
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