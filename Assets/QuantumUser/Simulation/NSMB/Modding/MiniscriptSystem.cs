using Miniscript;
using Photon.Deterministic;
using Quantum.Profiling;

namespace Quantum {
    public unsafe class MiniscriptSystem : SystemMainThread {

        private static bool intrinsicsInitialized = false;
        private ValMap Frame, PlayerData, StageTile;
        private Intrinsic Frame_GetStageTile;

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

            /*
            // Player info
            ValList players = new();
            foreach ((var _, var playerData) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                ValMap playerMap = new();
                playerMap["Inputs"] = GetInputs(f, playerData->PlayerRef);
                playerMap["PlayerRef"] = new ValNumber((int) playerData->PlayerRef);
                playerMap["Team"] = new ValNumber((int) playerData->RealTeam);
                playerMap["IsHost"] = (playerData->IsRoomHost ? ValNumber.one : ValNumber.zero);
                players.values.Add(playerMap);
            }
            f.MiniscriptInterpreter.SetGlobalValue("Players", players);

            // Mario info
            */

            // Execute
            f.MiniscriptInterpreter.RunUntilDone((10 * f.DeltaTime).AsDouble, true);
            if (!f.MiniscriptInterpreter.vm.yielding) {
                // Ran for 10 frames. Probably an infinite loop. Bail.
                
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
                value = null;
                return true;
            };
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
            inputMap.assignOverride = DoNothing;
            return inputMap;
        }

        private void AddButtonInput(ValMap inputMap, ValString name, Button button) {
            ValMap thisInputMap = new();
            thisInputMap["WasPressed"] =  button.WasPressed ? ValNumber.one : ValNumber.zero;
            thisInputMap["IsDown"] =      button.IsDown ? ValNumber.one : ValNumber.zero;
            thisInputMap["WasReleased"] = button.WasReleased ? ValNumber.one : ValNumber.zero;
            thisInputMap.assignOverride = DoNothing;
            inputMap.map[name] = thisInputMap;
        }

        private static ValMap.AssignOverrideFunc DoNothing = (s,k,v) => {
            return false;
        };
    }
}