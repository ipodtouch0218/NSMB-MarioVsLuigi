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

        //---Properties
        public bool IsPaused { get; set; }

        //---Serialized Variables
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private List<DebugSpawnCommand> debugSpawnCommands = new();
#endif
        [SerializeField] private PlayerElements playerElements;

        public void Start() {
            Settings.Controls.Player.ReserveItem.performed += OnPowerupAction;
            QuantumCallback.Subscribe<CallbackPollInput>(this, OnPollInput);
        }

        public void OnDestroy() {
            Settings.Controls.Player.ReserveItem.performed -= OnPowerupAction;
        }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void Update() {
            foreach (var debug in debugSpawnCommands) {
                if (UnityEngine.Input.GetKeyDown(debug.KeyCode)) {
                    QuantumRunner.DefaultGame.SendCommand(new CommandMvLDebugCmd { 
                        CommandId = CommandMvLDebugCmd.DebugCommand.SpawnEntity,
                        SpawnData = debug.Entity,
                    });
                }
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.P)) {
                QuantumRunner.DefaultGame.SendCommand(new CommandMvLDebugCmd {
                    CommandId = CommandMvLDebugCmd.DebugCommand.KillSelf,
                });
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.O)) {
                QuantumRunner.DefaultGame.SendCommand(new CommandMvLDebugCmd {
                    CommandId = CommandMvLDebugCmd.DebugCommand.FreezeSelf,
                });
            }
        }

        [Serializable]
        public class DebugSpawnCommand {
            public KeyCode KeyCode;
            public AssetRef<EntityPrototype> Entity;
        }
#endif

        public void OnPowerupAction(InputAction.CallbackContext context) {
            if (!playerElements.IsSpectating) {
                QuantumRunner.DefaultGame.SendCommand(new CommandSpawnReserveItem());
            }
        }

        public void OnPollInput(CallbackPollInput callback) {
            Input i;

            if (IsPaused) {
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

                bool jump = Settings.Controls.Player.Jump.ReadValue<float>() > 0.5f;
                bool sprint = (Settings.Controls.Player.Sprint.ReadValue<float>() > 0.5f) ^ Settings.Instance.controlsAutoSprint;
                bool powerupAction = Settings.Controls.Player.PowerupAction.ReadValue<float>() > 0.5f;

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
                };
            }

            callback.SetInput(i, DeterministicInputFlags.Repeatable);
        }
    }
}
