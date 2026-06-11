using NSMB.UI.Game;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Input = Quantum.Input;

namespace NSMB.Quantum {
    public class InputCollector : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private List<DebugSpawnCommand> debugSpawnCommands = new();
        [SerializeField] private PlayerElements playerElements;

        public void Start() {
            Settings.Controls.Player.ReserveItem.performed += OnPowerupAction;
            Settings.Controls.Player.Taunt.performed += OnTaunt;
            QuantumCallback.Subscribe<CallbackPollInput>(this, OnPollInput);
        }

        public void OnDestroy() {
            Settings.Controls.Player.ReserveItem.performed -= OnPowerupAction;
            Settings.Controls.Player.Taunt.performed -= OnTaunt;
        }

        public void Update() {
            var game = QuantumRunner.DefaultGame;
            if (game == null || game.Configurations.Runtime.IsRealGame) {
                return;
            }
            var keyboard = Keyboard.current;

            foreach (var debug in debugSpawnCommands) {
                if (keyboard[debug.Key].wasPressedThisFrame) {
                    game.SendCommand(new CommandMvLDebugCmd {
                        CommandId = CommandMvLDebugCmd.DebugCommand.SpawnEntity,
                        SpawnData = debug.Entity,
                    });
                }
            }
            if (keyboard[Key.P].wasPressedThisFrame) {
                game.SendCommand(new CommandMvLDebugCmd {
                    CommandId = CommandMvLDebugCmd.DebugCommand.KillSelf,
                });
            }
            if (keyboard[Key.O].wasPressedThisFrame) {
                game.SendCommand(new CommandMvLDebugCmd {
                    CommandId = CommandMvLDebugCmd.DebugCommand.FreezeSelf,
                });
            }
            if (keyboard[Key.I].wasPressedThisFrame) {
                game.SendCommand(new CommandMvLDebugCmd {
                    CommandId = CommandMvLDebugCmd.DebugCommand.KnockbackSelf,
                });
            }
        }

        public void OnPowerupAction(InputAction.CallbackContext context) {
            if (!playerElements.IsSpectating && !playerElements.PauseMenu.IsPaused) {
                QuantumRunner.DefaultGame.SendCommand(new CommandSpawnReserveItem());
            }
        }

        private void OnTaunt(InputAction.CallbackContext context) {
            if (!playerElements.IsSpectating && !playerElements.PauseMenu.IsPaused) {
                QuantumRunner.DefaultGame.SendCommand(new CommandTaunt());
            }
        }

        public void OnPollInput(CallbackPollInput callback) {
            Input i;

            if (playerElements.PauseMenu.IsPaused) {
                i = new();
            } else {
                Settings.Controls.Player.Enable();

                Vector2 stick = Settings.Controls.Player.Movement.ReadValue<Vector2>();
                Vector2 normalizedJoystick = stick.normalized;
                //TODO: changeable deadzone?
                bool up = Vector2.Dot(normalizedJoystick, Vector2.up) > 0.6f;
                bool down = Vector2.Dot(normalizedJoystick, Vector2.down) > 0.6f;
                bool left = Vector2.Dot(normalizedJoystick, Vector2.left) > 0.4f;
                bool right = Vector2.Dot(normalizedJoystick, Vector2.right) > 0.4f;

                bool jump = Settings.Controls.Player.Jump.IsPressed();
                bool sprint = Settings.Controls.Player.Sprint.IsPressed() ^ Settings.Instance.controlsAutoSprint;
                bool powerupAction = Settings.Controls.Player.PowerupAction.IsPressed();

                i = new() {
                    Up = up,
                    Down = down,
                    Left = left,
                    Right = right,
                    Jump = jump,
                    Sprint = sprint,
                    PowerupAction = powerupAction,
                    FireballPowerupAction = Settings.Instance.controlsFireballSprint && sprint,
                    PropellerPowerupAction = Settings.Instance.controlsPropellerJump && jump,
                    AllowGroundpoundWithLeftRight = Settings.Instance.controlsAllowGroundpoundWithLeftRight,
                };
            }

            callback.SetInput(i, DeterministicInputFlags.Repeatable);
        }


        [Serializable]
        public class DebugSpawnCommand {
            public Key Key;
            public AssetRef<EntityPrototype> Entity;
        }

    }
}
