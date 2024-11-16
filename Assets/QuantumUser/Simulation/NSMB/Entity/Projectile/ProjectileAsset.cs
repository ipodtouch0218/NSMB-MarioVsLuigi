
using Photon.Deterministic;
using Quantum;

public class ProjectileAsset : AssetObject {
    public ProjectileEffectType Effect;
    public bool Bounce = true;
    public FP Speed;
    public FP BounceStrength;
    public FPVector2 Gravity;
    public bool DestroyOnSecondBounce;
    public bool DestroyOnHit = true;
    public bool LockTo45Degrees = true;
    public bool InheritShooterVelocity;
    public bool HasCollision = true;

    public bool DoesntEffectBlueShell = true;
    public byte BlueShellSlowdownFrames = 40;

    public ParticleEffect DestroyParticleEffect = ParticleEffect.None;
    public SoundEffect ShootSound = SoundEffect.Powerup_Fireball_Shoot;
}

public enum ProjectileEffectType {
    Knockback,
    Freeze,
}