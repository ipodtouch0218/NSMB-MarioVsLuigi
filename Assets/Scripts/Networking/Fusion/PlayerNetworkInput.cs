using Fusion;

public struct PlayerNetworkInput : INetworkInput {

    public NetworkButtons buttons;

}

public enum PlayerControls {
    Up, Down, Left, Right, Jump, Sprint, PowerupAction, SprintPowerupAction
}