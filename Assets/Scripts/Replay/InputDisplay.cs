using NSMB.Extensions;
using NSMB.UI.Game;
using Quantum;
using UnityEngine;
using UnityEngine.UI;

public class InputDisplay : MonoBehaviour {

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
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView, onlyIfActiveAndEnabled: true);
        QuantumCallback.Subscribe<CallbackSimulateFinished>(this, OnSimulateFinished, onlyIfActiveAndEnabled: true);
    }

    private unsafe void OnUpdateView(CallbackUpdateView e) {
        Frame f = e.Game.Frames.Predicted;
        if (!f.Unsafe.TryGetPointer(playerElements.Entity, out MarioPlayer* mario)) {
            return;
        }

        bool isPressed;
        PlayerRef player = mario->PlayerRef;
        if (inputType != InputType.ReserveItem) {

            Quantum.Input input;
            if (player.IsValid) {
                input = *f.GetPlayerInput(player);
            } else {
                input = default;
            }
            isPressed = GetButton(input, inputType);
        } else {
            int diff = f.Number - commandFrame;
            isPressed = diff > 0 && diff < f.UpdateRate / 3;
        }
        display.color = isPressed ? pressedColor : unpressedColor;
    }

    private unsafe void OnSimulateFinished(CallbackSimulateFinished e) {
        Frame f = e.Game.Frames.Verified;
        if (!f.Unsafe.TryGetPointer(playerElements.Entity, out MarioPlayer* mario)
            || inputType != InputType.ReserveItem) {
            return;
        }

        PlayerRef player = mario->PlayerRef;
        /*
        if (e.Game.PlayerIsLocal(player)) {
            f = e.Game.Frames.Predicted;
        }
        */

        if (f.GetPlayerCommand(player) is CommandSpawnReserveItem) {
            commandFrame = f.Number;
        }
    }

    private static bool GetButton(Quantum.Input input, InputType inputType) {
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
        Jump, Sprint, PowerupAction, ReserveItem
    }
}
