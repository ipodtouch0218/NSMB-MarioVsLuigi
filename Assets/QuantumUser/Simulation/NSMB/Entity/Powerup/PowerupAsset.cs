using Photon.Deterministic;
using Quantum;
using UnityEngine;

public class PowerupAsset : AssetObject {

    public PowerupType Type;
    public PowerupState State;

    public AssetRef<EntityPrototype> Prefab;
    public bool SoundPlaysEverywhere;
    public SoundEffect SoundEffect = SoundEffect.Player_Sound_PowerupCollect;
    public SoundEffect BlockSpawnSoundEffect = SoundEffect.World_Block_Powerup;
    public FP SpawnChance = FP._0_10, LosingSpawnBonus = 0;
    public bool BigPowerup, VerticalPowerup, CustomPowerup, LivesOnlyPowerup;
    public Sprite ReserveSprite;

    public bool AvoidPlayers;
    public FP Speed;
    public FP BounceStrength;
    public FP TerminalVelocity;

    public bool FollowAnimationCurve;
    public FPAnimationCurve AnimationCurveX;
    public FPAnimationCurve AnimationCurveY;

    public sbyte StatePriority = -1, ItemPriority = -1;

    public FP GetModifiedChance(int starsToWin, int leaderStars, int ourStars) {
        int starDifference = leaderStars - ourStars;
        FP bonus = LosingSpawnBonus * FPMath.Log(starDifference + 1, FP.E) * (FP._1 - ((FP) (starsToWin - leaderStars) / starsToWin));
        return FPMath.Max(0, SpawnChance + bonus);
    }
}

public enum PowerupType {
    Basic,
    Starman,
    ExtraLife,
}