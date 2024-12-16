using Photon.Deterministic;
using Quantum;
using UnityEngine;
using UnityEngine.InputSystem;
using Input = Quantum.Input;

public class InputCollector : MonoBehaviour {

    //---Properties
    public bool IsPaused { get; set; }

    public void Start() {
        ControlSystem.controls.Player.ReserveItem.performed += OnPowerupAction;
        QuantumCallback.Subscribe<CallbackPollInput>(this, OnPollInput);
    }

    public void OnDestroy() {
        ControlSystem.controls.Player.ReserveItem.performed -= OnPowerupAction;
    }

    public void OnPowerupAction(InputAction.CallbackContext context) {
        QuantumRunner.DefaultGame.SendCommand(new CommandSpawnReserveItem());
    }

    public void OnPollInput(CallbackPollInput callback) {
        Input i;

        if (IsPaused) {
            i = new();
        } else {
            ControlSystem.controls.Player.Enable();

            Vector2 stick = ControlSystem.controls.Player.Movement.ReadValue<Vector2>();
            Vector2 normalizedJoystick = stick.normalized;
            //TODO: changeable deadzone?
            bool up = Vector2.Dot(normalizedJoystick, Vector2.up) > 0.6f;
            bool down = Vector2.Dot(normalizedJoystick, Vector2.down) > 0.6f;
            bool left = Vector2.Dot(normalizedJoystick, Vector2.left) > 0.4f;
            bool right = Vector2.Dot(normalizedJoystick, Vector2.right) > 0.4f;

            bool jump = ControlSystem.controls.Player.Jump.ReadValue<float>() > 0.5f;
            bool sprint = ControlSystem.controls.Player.Sprint.ReadValue<float>() > 0.5f;

            i = new() {
                Up = up,
                Down = down,
                Left = left,
                Right = right,
                Jump = jump,
                Sprint = sprint ^ Settings.Instance.controlsAutoSprint,
                PowerupAction = ControlSystem.controls.Player.PowerupAction.ReadValue<float>() > 0.5f,
                FireballPowerupAction = Settings.Instance.controlsFireballSprint && sprint,
                PropellerPowerupAction = Settings.Instance.controlsPropellerJump && jump,
            };
        }

        callback.SetInput(i, DeterministicInputFlags.Repeatable);
    }
}
