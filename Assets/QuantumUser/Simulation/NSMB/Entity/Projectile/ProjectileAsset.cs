using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;

public class ProjectileAsset : AssetObject, ISoundOverrideProvider {
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

    public byte MaxProjectiles = 6;
    public byte ProjectileVolleySize = 2;
    public byte ProjectileDelayFrames = 6;
    public byte ProjectileVolleyFrames = 75;

    public ParticleEffect DestroyParticleEffect = ParticleEffect.None;
    public SoundEffect ShootSound = SoundEffect.Powerup_Fireball_Shoot;

    public SoundEffectOverride[] SfxOverrides;

    [NonSerialized] private Dictionary<SoundEffect, SoundEffectOverride> overridesDict;
    public override void Loaded(IResourceManager resourceManager, Native.Allocator allocator) {
        overridesDict = new();
        if (SfxOverrides != null) {
            foreach (var @override in SfxOverrides) {
                overridesDict[@override.SoundEffect] = @override;
            }
        }
    }

    public SoundEffectOverride GetOverride(SoundEffect sfx) {
        overridesDict.TryGetValue(sfx, out var result);
        return result;
    }
}

public enum ProjectileEffectType : byte {
    Fire,
    Freeze,
    KillEnemiesAndSoftKnockbackPlayers,

    None = 0xff,
}