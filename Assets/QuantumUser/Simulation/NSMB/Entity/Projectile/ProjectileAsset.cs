
using Photon.Deterministic;
using Quantum;

public class ProjectileAsset : AssetObject {
    public ProjectileEffectType Effect;
    public bool Bounce = true;
    public FP Speed;
    public FP BounceStrength;
    public FPVector2 Gravity;
    public bool DestroyOnSecondBounce;
    public bool LockTo45Degrees = true;
    public bool InheritShooterVelocity;

    public bool DoesntEffectBlueShell = true;
    public byte BlueShellSlowdownFrames = 40;
}

public enum ProjectileEffectType {
    Knockback,
    Freeze,
}