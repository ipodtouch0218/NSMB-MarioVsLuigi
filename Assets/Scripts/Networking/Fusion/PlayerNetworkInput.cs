using Fusion;

public struct PlayerNetworkInput : INetworkInput {

    public NetworkButtons buttons;
    public int lastJumpPressTick;
    public byte powerupActionCounter;

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
