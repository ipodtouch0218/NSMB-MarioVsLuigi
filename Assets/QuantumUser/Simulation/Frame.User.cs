using Miniscript;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace Quantum {
    public unsafe partial class Frame {

        public StageTileInstance* StageTiles;
        public int StageTilesLength;

        public Interpreter MiniscriptInterpreter;

        partial void InitUser() {
            string script = @"
coinEntities = []

update = function()
    // Add a coin when a player hits space
    for i in range(0,9)
        input = frame.getPlayerInput(i)
        if input == null then
            continue
        end if
            
        if input.jump.wasPressed then
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
        
        pos = {""x"": -14 + sin(rad), ""y"": 1.5 + cos(rad)}
        coinTransform.position = pos
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

        partial void FreeUser() {
            if (StageTiles != null) {
                UnsafeUtility.Free(StageTiles, Unity.Collections.Allocator.Persistent);
                StageTiles = null;
            }
        }

        partial void SerializeUser(FrameSerializer serializer) {
            var stream = serializer.Stream;

            // Tilemap
            if (stream.Writing) {
                stream.WriteInt(StageTilesLength);
            } else {
                int newLength = stream.ReadInt();
                ReallocStageTiles(newLength);
            }
            for (int i = 0; i < StageTilesLength; i++) {
                StageTileInstance.Serialize(StageTiles + i, serializer);
            }

            // Miniscript Globals
            if (stream.Writing) {
                ValMap globals = MiniscriptInterpreter.vm.globalContext.variables;
                List<Value> values = new();
                foreach (var (k,v) in globals.map) {
                    int keyIndex = values.IndexOf(k);
                    if (keyIndex == -1) {
                        keyIndex = values.Count;
                        values.Add(k);
                    }
                    int valueIndex = values.IndexOf(v);
                    if (valueIndex == -1) {
                        valueIndex = values.Count;
                        values.Add(v);
                    }
                }

                stream.WriteInt(values.Count);
                foreach (Value value in values) {
                    value.Serialize(serializer);
                }
            } else {
                int globalCount = stream.ReadInt();
                for (int i = 0; i < globalCount; i++) {
                    
                }
            }
        }

        partial void CopyFromUser(Frame frame) {
            ReallocStageTiles(frame.StageTilesLength);
            UnsafeUtility.MemCpy(StageTiles, frame.StageTiles, StageTileInstance.SIZE * frame.StageTilesLength);
        }

        public void ReallocStageTiles(int newSize) {
            if (StageTilesLength == newSize) {
                return;
            }

            if (StageTiles != null) {
                UnsafeUtility.Free(StageTiles, Unity.Collections.Allocator.Persistent);
                StageTiles = null;
            }
            
            if (newSize > 0) {
                StageTiles = (StageTileInstance*) UnsafeUtility.Malloc(StageTileInstance.SIZE * newSize, StageTileInstance.ALIGNMENT, Unity.Collections.Allocator.Persistent);
            }

            StageTilesLength = newSize;
        }
    }
}