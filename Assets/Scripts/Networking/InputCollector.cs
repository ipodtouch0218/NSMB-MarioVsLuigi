using Photon.Deterministic;
using Quantum;
using System.ComponentModel.Design;
using UnityEngine;
using UnityEngine.InputSystem;
using Input = Quantum.Input;

public class InputCollector : MonoBehaviour {

    //---Serailized
    [SerializeField] private InputActionReference movementAction, jumpAction, sprintAction, powerupAction, reservePowerupAction;

    public void Start() {
        QuantumCallback.Subscribe<CallbackPollInput>(this, PollInput);
        reservePowerupAction.action.performed += OnPowerupAction;
    }

    public void OnDestroy() {
        reservePowerupAction.action.performed -= OnPowerupAction;
    }

    public void OnPowerupAction(InputAction.CallbackContext context) {
        QuantumRunner.DefaultGame.SendCommand(new CommandSpawnReserveItem());
    }

    public void PollInput(CallbackPollInput callback) {

        jumpAction.action.actionMap.Enable();

        Vector2 stick = movementAction.action.ReadValue<Vector2>();
        Vector2 normalizedJoystick = stick.normalized;
        //TODO: changeable deadzone?
        bool up = Vector2.Dot(normalizedJoystick, Vector2.up) > 0.6f;
        bool down = Vector2.Dot(normalizedJoystick, Vector2.down) > 0.6f;
        bool left = Vector2.Dot(normalizedJoystick, Vector2.left) > 0.4f;
        bool right = Vector2.Dot(normalizedJoystick, Vector2.right) > 0.4f;

        bool jump = jumpAction.action.ReadValue<float>() > 0.5f;
        bool sprint = sprintAction.action.ReadValue<float>() > 0.5f;

        Input i = new() {
            Up = up,
            Down = down,
            Left = left,
            Right = right,
            Jump = jump,
            Sprint = sprint ^ Settings.Instance.controlsAutoSprint,
            PowerupAction = powerupAction.action.ReadValue<float>() > 0.5f,
            FireballPowerupAction = Settings.Instance.controlsFireballSprint && sprint,
            PropellerPowerupAction = Settings.Instance.controlsPropellerJump && jump,
        };

        callback.SetInput(i, DeterministicInputFlags.Repeatable);
    }
}
