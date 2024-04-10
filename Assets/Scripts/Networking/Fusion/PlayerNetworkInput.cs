using Fusion;

public struct PlayerNetworkInput : INetworkInput {

    public NetworkButtons Buttons;
    public int LastJumpPressTick;
    public byte PowerupActionCounter;

}

public enum PlayerControls {
    Up,
    Down,
    Left,
    Right,
    Jump,
    Sprint,
    PowerupAction,
}
