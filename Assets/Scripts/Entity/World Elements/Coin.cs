using Fusion;

public abstract class Coin : NetworkBehaviour {

    [Networked(OnChanged = nameof(OnCoinCollected))] public NetworkBool IsCollected { get; set; } = false;

    public abstract void OnCoinCollected();

}
