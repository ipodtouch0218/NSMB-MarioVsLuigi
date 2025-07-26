using Miniscript;
using Photon.Deterministic;
using Quantum.Profiling;
using UnityEngine;

namespace Quantum {
    public unsafe class MiniscriptSystem : SystemMainThread {

        private static bool intrinsicsInitialized = false;
        private ValMap Frame, PlayerData, StageTile, Input, Button, EmptyObject;
        private Intrinsic Frame_GetStageTile, Frame_GetPlayerData, Frame_Create, Frame_Exists, Frame_Destroy, PlayerData_Inputs;

        public override void OnInit(Frame f) {
            InitializeIntrinsics();
            InitializeObjects();
        }

        public override void Update(Frame f) {
            using var x = HostProfiler.Start("Miniscript");
            //f.MiniscriptInterpreter.vm?.globalContext?.variables?.map?.Clear();

            Value updateFunction = f.MiniscriptInterpreter.GetGlobalValue("update");
            if (f.MiniscriptInterpreter.GetGlobalValue("_events") is not ValList events) {
                f.MiniscriptInterpreter.SetGlobalValue("_events", events = new ValList());
            }
            events.values.Add(updateFunction);

            // Frame
            Frame.userData = f;
            f.MiniscriptInterpreter.SetGlobalValue("frame", Frame);

            // Execute
            f.MiniscriptInterpreter.RunUntilDone(0.2, true);
            if (!f.MiniscriptInterpreter.vm.yielding) {
                // Ran for 200ms. Probably an infinite loop. Crash.
                
            }
        }

        private void InitializeIntrinsics() {
            if (intrinsicsInitialized) {
                return;
            }

            // Globals
            Intrinsic lerp = Intrinsic.Create("lerp");
            lerp.AddParam("a");
            lerp.AddParam("b");
            lerp.AddParam("t");
            lerp.code = (context, partial) => {
                FP a = context.GetLocalFloat("a");
                FP b = context.GetLocalFloat("b");
                FP t = context.GetLocalFloat("t");
                return new Intrinsic.Result(FPMath.Lerp(a, b, t));
            };

            Frame_GetStageTile = Intrinsic.Create("");
            Frame_GetStageTile.AddParam("x");
            Frame_GetStageTile.AddParam("y");
            Frame_GetStageTile.code = (context, partial) => {
                int x = context.GetLocalInt("x");
                int y = context.GetLocalInt("y");
                Frame f = Frame.userData as Frame;
                VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                if (x < 0 || x >= stage.TileDimensions.X || y < 0 || y >= stage.TileDimensions.Y) {
                    return Intrinsic.Result.Null;
                }
                int index = x + y * stage.TileDimensions.X;

                ValMap result = StageTile.ShallowCopy();
                result.userData = index;
                return new Intrinsic.Result(result);
            };

            Frame_GetPlayerData = Intrinsic.Create("");
            Frame_GetPlayerData.AddParam("player");
            Frame_GetPlayerData.code = (context, partial) => {
                int playerRef = context.GetLocalInt("player");
                Frame f = Frame.userData as Frame;
                var dict = f.ResolveDictionary(f.Global->PlayerDatas);

                if (!dict.ContainsKey(playerRef)) {
                    return Intrinsic.Result.Null;
                }
                ValMap result = PlayerData.ShallowCopy();
                result.userData = dict[playerRef];
                return new Intrinsic.Result(result);
            };

            Frame_Create = Intrinsic.Create("");
            Frame_Create.AddParam("entityPrototypeRef");
            Frame_Create.code = (context, partial) => {
                ValMap entityPrototypeRef = context.GetLocal("entityPrototypeRef") as ValMap;
                Frame f = Frame.userData as Frame;
                EntityRef newEntity;
                if (entityPrototypeRef?.userData is AssetRef<EntityPrototype> ep) {
                    try {
                        newEntity = f.Create(ep);
                    } catch {
                        return Intrinsic.Result.Null;
                    }
                } else {
                    newEntity = f.Create();
                }
                ValMap ret = EmptyObject.ShallowCopy();
                ret.userData = newEntity;
                return new Intrinsic.Result(ret);
            };

            Frame_Exists = Intrinsic.Create("");
            Frame_Exists.AddParam("entityRef");
            Frame_Exists.code = (context, partial) => {
                ValMap entityRef = context.GetLocal("entityRef") as ValMap;
                if (entityRef?.userData is EntityRef en) {
                    Frame f = Frame.userData as Frame;
                    return f.Exists(en) ? Intrinsic.Result.True : Intrinsic.Result.False;
                }
                return Intrinsic.Result.False;
            };

            Frame_Destroy = Intrinsic.Create("");
            Frame_Destroy.AddParam("entityRef");
            Frame_Destroy.code = (context, partial) => {
                ValMap entityRef = context.GetLocal("entityRef") as ValMap;
                if (entityRef?.userData is EntityRef en) {
                    Frame f = Frame.userData as Frame;
                    return f.Destroy(en) ? Intrinsic.Result.True : Intrinsic.Result.False;
                }
                return Intrinsic.Result.False;
            };

            PlayerData_Inputs = Intrinsic.Create("");
            PlayerData_Inputs.code = (context, partial) => {
                ValMap self = context.self as ValMap;
                Frame f = Frame.userData as Frame;
                var ret = Input.ShallowCopy();
                ret.userData = f.Unsafe.GetPointer<PlayerData>((EntityRef) self.userData)->PlayerRef;
                return new Intrinsic.Result(ret);
            };

            intrinsicsInitialized = true;
        }

        private void InitializeObjects() {
            // Frame object
            Frame = new();
            Frame.evalOverride = (ValMap self, Value key, out Value outValue) => {
                outValue = null;
                Frame f = self.userData as Frame;
                switch (key.ToString()) {
                case "number": outValue = new ValNumber(f.Number); break;
                case "isVerified": outValue = f.IsVerified ? ValNumber.one : ValNumber.zero; break;
                case "isPredicted": outValue = f.IsPredicted ? ValNumber.one : ValNumber.zero; break;
                case "deltaTime": outValue = new ValNumber(f.DeltaTime); break;
                case "playerCount": outValue = new ValNumber(f.ComponentCount<PlayerData>()); break;
                case "rng": outValue = new ValNumber(f.RNG->Next()); break;
                case "getStageTile": outValue = Frame_GetStageTile.GetFunc(); break;
                case "getPlayerData": outValue = Frame_GetPlayerData.GetFunc(); break;
                case "create": outValue = Frame_Create.GetFunc(); break;
                case "exists": outValue = Frame_Exists.GetFunc(); break;
                case "destroy": outValue = Frame_Destroy.GetFunc(); break;
                }
                return true;
            };
            Frame.assignOverride = (ValMap self, Value key, Value value) => {
                Frame f = self.userData as Frame;
                switch (key.ToString()) {
                case "deltaTime": f.DeltaTime = value.FloatValue(); break;
                }
                return true;
            };

            StageTile = new();
            StageTile.evalOverride = (ValMap self, Value key, out Value outValue) => {
                outValue = null;
                Frame f = Frame.userData as Frame;
                int index = (int) self.userData;
                ref StageTileInstance tile = ref f.StageTiles[index];
                switch (key.ToString()) {
                //case "tile": outvalue = tile.Tile; break;
                case "mirrorX": outValue = tile.Flags.HasFlag(StageTileFlags.MirrorX) ? ValNumber.one : ValNumber.zero; break;
                case "mirrorY": outValue = tile.Flags.HasFlag(StageTileFlags.MirrorY) ? ValNumber.one : ValNumber.zero; break;
                case "rotation": outValue = new ValNumber(tile.Rotation); break;
                }

                return true;
            };
            StageTile.assignOverride = (ValMap self, Value key, Value value) => {
                Frame f = Frame.userData as Frame;
                int index = (int) self.userData;
                ref StageTileInstance tile = ref f.StageTiles[index];
                switch (key.ToString()) {
                //case "tile": outvalue = tile.Tile; break;
                case "mirrorX": tile.Flags = QuantumUtils.SetFlag(tile.Flags, StageTileFlags.MirrorX, value.BoolValue()); break;
                case "mirrorY": tile.Flags = QuantumUtils.SetFlag(tile.Flags, StageTileFlags.MirrorY, value.BoolValue()); break;
                case "rotation": tile.Rotation = (ushort) value.UIntValue(); break;
                default: return true;
                }
                VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                f.Events.TileChanged(
                    QuantumUtils.RelativeTileToUnityTile(stage, new IntVector2(index % stage.TileDimensions.X, index / stage.TileDimensions.X)),
                    tile);
                return true;
            };
            
            PlayerData = new();
            PlayerData.evalOverride = (ValMap self, Value key, out Value outValue) => {
                Frame f = Frame.userData as Frame;
                EntityRef en = (EntityRef) self.userData;
                var playerData = f.Unsafe.GetPointer<PlayerData>(en);

                switch (key.ToString()) {
                case "playerRef": outValue = new ValNumber((int) playerData->PlayerRef); break;
                case "team": outValue = new ValNumber((int) playerData->RealTeam); break;
                case "isRoomHost": outValue = playerData->IsRoomHost ? ValNumber.one : ValNumber.zero; break;
                case "wins": outValue = new ValNumber(playerData->Wins); break;
                case "ping": outValue = new ValNumber(playerData->Ping); break;
                case "isSpectator": outValue = playerData->IsSpectator ? ValNumber.one : ValNumber.zero; break;
                case "character": outValue = new ValNumber(playerData->Character); break;
                case "palette": outValue = new ValNumber(playerData->Palette); break;
                case "inputs": outValue = PlayerData_Inputs.GetFunc(); break;
                }
                outValue = null;
                return true;
            };
            PlayerData.assignOverride = (ValMap self, Value key, Value value) => {
                Frame f = Frame.userData as Frame;
                EntityRef en = (EntityRef) self.userData;
                var playerData = f.Unsafe.GetPointer<PlayerData>(en);

                switch (key.ToString()) {
                case "team": playerData->RealTeam = (byte) value.IntValue(); break;
                case "isRoomHost":
                    if (value.BoolValue() == true) {
                        foreach ((var _, var otherPlayerData) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                            otherPlayerData->IsRoomHost = false;
                        }
                        playerData->IsRoomHost = true;
                    }
                    break;
                case "wins": playerData->Wins = value.IntValue(); break;
                //case "character": playerData->Character = (byte) value.IntValue(); break;
                //case "palette": playerData->Palette = (byte) value.IntValue(); break;
                }
                return true;
            };

            Input = new();
            Input.evalOverride = (ValMap self, Value key, out Value outValue) => {
                outValue = null;
                Frame f = Frame.userData as Frame;
                PlayerRef player = (PlayerRef) self.userData;
                Input* inputs = f.GetPlayerInput(player);
                if (inputs == null) {
                    return true;
                }

                var ret = Button.ShallowCopy();
                switch (key.ToString()) {
                case "up": ret.userData = inputs->Up; break;
                case "right": ret.userData = inputs->Right; break;
                case "down": ret.userData = inputs->Down; break;
                case "left": ret.userData = inputs->Left; break;
                case "jump": ret.userData = inputs->Jump; break;
                case "sprint": ret.userData = inputs->Sprint; break;
                case "powerupAction": ret.userData = inputs->PowerupAction; break;
                case "fireballPowerupAction": ret.userData = inputs->FireballPowerupAction; break;
                case "propellerPowerupAction": ret.userData = inputs->PropellerPowerupAction; break;
                default: return true;
                }
                outValue = ret;
                return true;
            };
            Input.assignOverride = AssignNothing;

            Button = new();
            Button.evalOverride = (ValMap self, Value key, out Value outValue) => {
                outValue = null;
                Button b = (Button) self.userData;

                switch (key.ToString()) {
                case "isDown": outValue = (b.IsDown ? ValNumber.one : ValNumber.zero); break;
                case "wasPressed": outValue = (b.WasPressed ? ValNumber.one : ValNumber.zero); break;
                case "wasReleased": outValue = (b.WasReleased ? ValNumber.one : ValNumber.zero); break;
                default: return true;
                }
                return true;
            };
            Button.assignOverride = AssignNothing;

            EmptyObject = new();
            EmptyObject.evalOverride = EvalNothing;
            EmptyObject.assignOverride = AssignNothing;
        }

        private ValMap GetInputs(Frame f, PlayerRef player) {
            Input* inputs = f.GetPlayerInput(player);
            if (inputs == null) {
                return null;
            }

            ValMap inputMap = new();
            AddButtonInput(inputMap, new ValString("Up"), inputs->Up);
            AddButtonInput(inputMap, new ValString("Down"), inputs->Down);
            AddButtonInput(inputMap, new ValString("Left"), inputs->Left);
            AddButtonInput(inputMap, new ValString("Right"), inputs->Right);
            AddButtonInput(inputMap, new ValString("Jump"), inputs->Jump);
            AddButtonInput(inputMap, new ValString("Sprint"), inputs->Sprint);
            AddButtonInput(inputMap, new ValString("PowerupAction"), inputs->PowerupAction);
            AddButtonInput(inputMap, new ValString("FireballAction"), inputs->FireballPowerupAction);
            AddButtonInput(inputMap, new ValString("PropellerAction"), inputs->PropellerPowerupAction);
            inputMap.assignOverride = AssignNothing;
            return inputMap;
        }

        private void AddButtonInput(ValMap inputMap, ValString name, Button button) {
            ValMap thisInputMap = new();
            thisInputMap["WasPressed"] =  button.WasPressed ? ValNumber.one : ValNumber.zero;
            thisInputMap["IsDown"] =      button.IsDown ? ValNumber.one : ValNumber.zero;
            thisInputMap["WasReleased"] = button.WasReleased ? ValNumber.one : ValNumber.zero;
            thisInputMap.assignOverride = AssignNothing;
            inputMap.map[name] = thisInputMap;
        }

        private static ValMap.EvalOverrideFunc EvalNothing = (ValMap s, Value k, out Value v) => {
            v = null;
            return true;
        };

        private static ValMap.AssignOverrideFunc AssignNothing = (s,k,v) => {
            return true;
        };
    }
}