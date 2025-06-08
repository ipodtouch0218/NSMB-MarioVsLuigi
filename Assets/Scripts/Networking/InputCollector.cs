using NSMB.UI.Game;
using Photon.Deterministic;
using Quantum;
using UnityEngine;
using UnityEngine.InputSystem;
using Input = Quantum.Input;

public class InputCollector : MonoBehaviour {

    //---Properties
    public bool IsPaused { get; set; }

    //---Serialized Variables
    [SerializeField] private PlayerElements playerElements;

    public void Start() {
        Settings.Controls.Player.ReserveItem.performed += OnPowerupAction;
        QuantumCallback.Subscribe<CallbackPollInput>(this, OnPollInput);
    }

    public void OnDestroy() {
        Settings.Controls.Player.ReserveItem.performed -= OnPowerupAction;
    }

    public void OnPowerupAction(InputAction.CallbackContext context) {
        if (!playerElements.IsSpectating) {
            NetworkHandler.Game.SendCommand(new CommandSpawnReserveItem());
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
