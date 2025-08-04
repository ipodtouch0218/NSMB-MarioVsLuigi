using Miniscript;
using Photon.Deterministic;
using Quantum.Profiling;

namespace Quantum {
    public unsafe class MiniscriptSystem : SystemMainThread {

        private static bool intrinsicsInitialized = false;
        private ValMap Input, Button, EmptyObject;

        public override void OnInit(Frame f) {
            InitializeIntrinsics();
            InitializeObjects();
            f.MiniscriptInterpreter.SetGlobalValue("frame", new ValFrame());
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
            f.MiniscriptInterpreter.hostData = f;
            ((ValMap) f.MiniscriptInterpreter.GetGlobalValue("frame")).userData = f;

            // Execute
            f.MiniscriptInterpreter.RunUntilDone(0.2, true);
            if (!f.MiniscriptInterpreter.vm.yielding) {
                // Ran for 200ms. Probably an infinite loop. Crash.
                
            }
        }

        private void InitializeIntrinsics() {
            if (intrinsicsInitialized) {
                //Intrinsic.Clear();
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


/*
            PlayerData_Inputs = Intrinsic.Create("");
            PlayerData_Inputs.code = (context, partial) => {
                ValMap self = context.self as ValMap;
                Frame f = context.interpreter.hostData as Frame;
                EntityRef en = (EntityRef) self.userData;
                var ret = Input.ShallowCopy();
                ret.userData = f.Unsafe.GetPointer<PlayerData>(en)->PlayerRef;
                return new Intrinsic.Result(ret);
            };
*/
            intrinsicsInitialized = true;
        }

        private void InitializeObjects() {            
                /*
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
                */

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