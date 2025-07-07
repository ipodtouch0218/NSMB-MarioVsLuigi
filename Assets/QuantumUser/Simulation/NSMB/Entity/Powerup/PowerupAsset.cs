using Photon.Deterministic;
using Quantum;

public class PowerupAsset : CoinItemAsset {

    public PowerupType Type;
    public PowerupState State;

    public bool SoundPlaysEverywhere;
    public SoundEffect SoundEffect = SoundEffect.Player_Sound_PowerupCollect;

#if QUANTUM_UNITY
    public UnityEngine.Sprite ReserveSprite;
#endif

    public bool AvoidPlayers;
    public FP Speed;
    public FP BounceStrength;
    public FP TerminalVelocity;

    public bool FollowAnimationCurve;
    public FPAnimationCurve AnimationCurveX;
    public FPAnimationCurve AnimationCurveY;

    public sbyte StatePriority = -1, ItemPriority = -1;
}

public enum PowerupType {
    Basic,
    Starman,
    ExtraLife,
}