using Miniscript;
using Photon.Deterministic;
using Quantum.Profiling;
using System;
using UnityEngine;

namespace Quantum {
    public unsafe class MiniscriptSystem : SystemMainThread {

        private static bool intrinsicsInitialized = false;
        private ValMap Frame, PlayerData, StageTile, Input, Button, EmptyObject;
        private Intrinsic Frame_GetStageTile, Frame_GetPlayerData, Frame_Create, Frame_Exists, Frame_Destroy, Frame_FindAsset, Frame_Add, Frame_Get, Frame_Has, Frame_Remove, PlayerData_Inputs;

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
                Intrinsic.Clear();
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
            Frame_Create.AddParam("assetRef");
            Frame_Create.code = (context, partial) => {
                Value param = context.GetLocal("assetRef", null);
                Frame f = Frame.userData as Frame;
                EntityRef newEntity;

                if (param is ValAssetRef valAssetObject) {
                    if (f.Context.ResourceManager.TryGetAsset(valAssetObject.Asset, out EntityPrototype ep)) {
                        // Create from prototype
                        newEntity = f.Create(ep);
                    } else {
                        // Failure, do nothing
                        return Intrinsic.Result.Null;
                    }
                } else if (param == null) {
                    // Create empty
                    newEntity = f.Create();
                } else {
                    // Do nothing- invalid param
                    return Intrinsic.Result.Null;
                }

                return new Intrinsic.Result(new ValEntityRef(newEntity));
            };

            Frame_Exists = Intrinsic.Create("");
            Frame_Exists.AddParam("entityRef");
            Frame_Exists.code = (context, partial) => {
                EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
                Frame f = Frame.userData as Frame;
                return f.Exists(entity) ? Intrinsic.Result.True : Intrinsic.Result.False;
            };

            Frame_Destroy = Intrinsic.Create("");
            Frame_Destroy.AddParam("entityRef");
            Frame_Destroy.code = (context, partial) => {
                EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
                Frame f = Frame.userData as Frame;
                return f.Destroy(entity) ? Intrinsic.Result.True : Intrinsic.Result.False;
            };

            Frame_FindAsset = Intrinsic.Create("");
            Frame_FindAsset.AddParam("path");
            Frame_FindAsset.code = (context, partial) => {
                string path = context.GetLocalString("path");
                Frame f = Frame.userData as Frame;
                var asset = f.Context.ResourceManager.GetAsset(path);
                return new Intrinsic.Result(new ValAssetRef(asset));
            };

            Frame_Add = Intrinsic.Create("");
            Frame_Add.AddParam("entityRef");
            Frame_Add.AddParam("type");
            Frame_Add.code = (context, partial) => {
                EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
                int id = ComponentTypeId.GetComponentIndex(context.GetLocalString("componentTypeName"));
                Frame f = Frame.userData as Frame;
                try {
                    int index = ComponentTypeId.GetComponentIndex(context.GetLocalString("componentTypeName"));
                    var result = f.Add(entity, index, out _);
                    return (result is AddResult.ComponentAdded or AddResult.ComponentAlreadyExists) ? Intrinsic.Result.True : Intrinsic.Result.False;
                } catch {
                    return Intrinsic.Result.False;
                }
            };

            Frame_Get = Intrinsic.Create("");
            Frame_Get.AddParam("entityRef");
            Frame_Get.AddParam("type");
            Frame_Get.code = (context, partial) => {
                EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
                Frame f = Frame.userData as Frame;
                try {
                    string typeStr = context.GetLocalString("type");
                    int index = ComponentTypeId.GetComponentIndex(typeStr);
                    Type type = ComponentTypeId.GetComponentType(index);

                    if (!f.Unsafe.TryGetPointer(entity, index, out void* ptr)) {
                        return Intrinsic.Result.Null;
                    }
                    return new Intrinsic.Result(new ValUnsafeStruct(type, (byte*) ptr));
                } catch {
                    return Intrinsic.Result.Null;
                }
            };

            Frame_Has = Intrinsic.Create("");
            Frame_Has.AddParam("entityRef");
            Frame_Has.AddParam("type");
            Frame_Has.code = (context, partial) => {
                EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
                Frame f = Frame.userData as Frame;
                try {
                    int index = ComponentTypeId.GetComponentIndex(context.GetLocalString("type"));
                    return f.Has(entity, index) ? Intrinsic.Result.True : Intrinsic.Result.False;
                } catch {
                    return Intrinsic.Result.False;
                }
            };

            Frame_Remove = Intrinsic.Create("");
            Frame_Remove.AddParam("entityRef");
            Frame_Remove.AddParam("type");
            Frame_Remove.code = (context, partial) => {
                EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
                Frame f = Frame.userData as Frame;
                try {
                    int index = ComponentTypeId.GetComponentIndex(context.GetLocalString("type"));
                    return f.Remove(entity, index) ? Intrinsic.Result.True : Intrinsic.Result.False;
                } catch {
                    return Intrinsic.Result.False;
                }
            };

            PlayerData_Inputs = Intrinsic.Create("");
            PlayerData_Inputs.code = (context, partial) => {
                ValMap self = context.self as ValMap;
                Frame f = Frame.userData as Frame;
                EntityRef en = (EntityRef) self.userData;
                var ret = Input.ShallowCopy();
                ret.userData = f.Unsafe.GetPointer<PlayerData>(en)->PlayerRef;
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
                case "findAsset": outValue = Frame_FindAsset.GetFunc(); break;
                case "add": outValue = Frame_Add.GetFunc(); break;
                case "has": outValue = Frame_Has.GetFunc(); break;
                case "get": outValue = Frame_Get.GetFunc(); break;
                case "remove": outValue = Frame_Remove.GetFunc(); break;
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
                outValue = null;
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