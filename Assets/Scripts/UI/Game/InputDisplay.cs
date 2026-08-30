using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Input = Quantum.Input;

namespace NSMB.UI.Game {
    public class InputDisplay : QuantumSceneViewComponent {

        private static Dictionary<InputType, Type> CommandTypes = new() {
            [InputType.ReserveItem] = typeof(CommandSpawnReserveItem),
            [InputType.Taunt] = typeof(CommandTaunt),
        };

        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private InputType inputType;
        [SerializeField] private Image display;
        [SerializeField] private Color unpressedColor = Color.black, pressedColor = Color.white;

        //---Private Variables
        private int commandFrame;

        public void OnValidate() {
            this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
            this.SetIfNull(ref display);
        }
        
        public void Start() {
            QuantumCallback.Subscribe<CallbackSimulateFinished>(this, OnSimulateFinished, onlyIfActiveAndEnabled: true);
        }

        public override unsafe void OnUpdateView() {
            Frame f = VerifiedFrame;
            if (!f.Unsafe.TryGetPointer(playerElements.Entity, out MarioPlayer* mario)) {
                return;
            }

            PlayerRef player = mario->PlayerRef;
            if (Game.PlayerIsLocal(player)) {
                // Use predicted inputs instead.
                f = PredictedFrame;
            }

            bool isPressed;
            if (CommandTypes.ContainsKey(inputType)) {
                int diff = f.Number - commandFrame;
                isPressed = diff > 0 && diff < f.UpdateRate / 3;
            } else { 
                Input input;
                if (player.IsValid) {
                    input = *f.GetPlayerInput(player);
                } else {
                    input = default;
                }
                isPressed = GetButton(input, inputType);
            }
            display.color = isPressed ? pressedColor : unpressedColor;
        }

        private unsafe void OnSimulateFinished(CallbackSimulateFinished e) {
            Frame f = e.Game.Frames.Verified;
            if (!f.Unsafe.TryGetPointer(playerElements.Entity, out MarioPlayer* mario)) {
                return;
            }

            if (CommandTypes.TryGetValue(inputType, out Type commandType)) {
                PlayerRef player = mario->PlayerRef;
                if (f.GetPlayerCommand(player)?.GetType() == commandType) {
                    commandFrame = f.Number;
                }
            }
        }

        private static bool GetButton(Input input, InputType inputType) {
            return inputType switch {
                InputType.Up => input.Up,
                InputType.Right => input.Right,
                InputType.Down => input.Down,
                InputType.Left => input.Left,
                InputType.Jump => input.Jump,
                InputType.Sprint => input.Sprint,
                InputType.PowerupAction => input.PowerupAction,
                _ => false,
            };
        }

        public enum InputType {
            Up, Down, Left, Right,
            Jump, Sprint, PowerupAction,
            ReserveItem, Taunt
        }
    }
}
