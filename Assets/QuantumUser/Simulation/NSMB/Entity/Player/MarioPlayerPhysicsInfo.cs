using Photon.Deterministic;
using Quantum;
using System.Linq;

public class MarioPlayerPhysicsInfo : AssetObject {

    // --- Walking
    // Normal
    public int WalkSpeedStage = 1, RunSpeedStage = 3, StarSpeedStage = 4;
    public FP[] WalkMaxVelocity = FF(new[] { 0.9375f, 2.8125f, 4.21875f, 5.625f, 8.4375f });
    public FP[] WalkAcceleration = FF(new[] { 7.91015625f, 3.955081725f, 3.515625f, 2.63671875f, 84.375f });
    public FP WalkBlueShellMultiplier = FF(0.9f);
    public FP WalkButtonReleaseDeceleration = FF(3.9550781196f);

    // Turnaround
    public FP[] SlowTurnaroundAcceleration = FF(new[] { 3.955078125f, 8.7890625f, 8.7890625f, 21.093756f });
    public FP FastTurnaroundAcceleration = FF(28.125f);

    public FP SkiddingMinimumVelocity = FF(4.6875f);
    public FP SkiddingDeceleration = FF(10.54687536f);
    public FP SkiddingStarmanDeceleration = FF(84.375f);

    // Crouching
    public FP CrouchOffEdgeVelocity = FF(-3.75f);

    // Mega
    public FP[] WalkMegaAcceleration = FF(new[] { 28.125f, 4.83398433f, 4.83398433f, 4.83398433f, 4.83398433f });
    public FP[] SlowTurnaroundMegaAcceleration = FF(new[] { 4.614257808f, 10.546875f, 21.09375f, 21.09375f });

    // Special Tiles
    public FP[] WalkIceAcceleration = FF(new[] { 1.9775390625f, 3.955081725f, 3.515625f, 2.63671875f, 84.375f });
    public FP[] WalkButtonReleaseIceDeceleration = FF(new[] { 0.439453125f, 1.483154296875f, 1.483154296875f, 1.483154296875f, 1.483154296875f });
    public FP SkiddingIceDeceleration = FF(3.955078125f);
    public FP SlowTurnaroundIceAcceleration = FF(2.63671875f);

    // --- Hitboxes
    public FP SmallHitboxHeight = FF(0.42f);
    public FP LargeHitboxHeight = FF(0.82f);

    // --- Jumping
    public byte JumpBufferFrames = 12;
    public byte CoyoteTimeFrames = 3;
    public FP JumpVelocity = FF(6.62109375f);
    public FP JumpSpeedBonusVelocity = FF(0.46875f);
    public FP JumpTripleBonusVelocity = FP._0_50;
    public FP JumpMegaVelocity = FF(12.1875f);
    public FP JumpMegaSpeedBonusVelocity = FF(0.52734375f);
    public FP JumpMiniVelocity = FF(5.408935546875f);
    public FP JumpMiniSpeedBonusVelocity = FF(0.428466796875f);

    // Walljump
    public FP WalljumpHorizontalVelocity = FF(4.21875f);
    public FP WalljumpVerticalVelocity = FF(6.4453125f);
    public FP WalljumpMiniVerticalVelocity = FF(5.1708984375f);

    // --- Gravity
    public FP[] GravityVelocity = FF(new[] { 4.16015625f, 2.109375f, 0f, -5.859375f });
    public FP[] GravityAcceleration = FF(new[] { -7.03125f, -28.125f, -38.671875f, -28.125f, -38.671875f });
    public FP[] GravityMiniVelocity = FF(new[] { 4.566650390625f, 2.633056640625f, 0f, -3.929443359375f });
    public FP[] GravityMiniAcceleration = FF(new[] { -4.833984375f, -7.03125f, -10.546875f, -7.03125f, -10.546875f });
    public FP[] GravityMegaVelocity = FF(new[] { 4.04296875f, });
    public FP[] GravityMegaAcceleration = FF(new[] { -28.125f, -38.671875f });
    public FP[] GravitySwimmingVelocity = FF(new[] { 0f });
    public FP[] GravitySwimmingAcceleration = FF(new[] { -4.833984375f, -3.076171875f });
    public FP GravityFlyingAcceleration = FF(-7.848f);
    public FP GravityGroundpoundStart = FF(0.15f);

    // --- Terminal Velocity
    public FP TerminalVelocity = FF(-7.5f);
    public FP TerminalVelocityMiniMultiplier = FF(0.625f);
    public FP TerminalVelocityMegaMultiplier = 2;
    public FP TerminalVelocityFlying = FF(-1.40625f);
    public FP TerminalVelocityDrilling = FF(-7.5f);
    public FP TerminalVelocityWallslide = FF(-4.6875f);
    public FP TerminalVelocityPropeller = FF(-2f);
    public FP TerminalVelocityPropellerSpin = FF(-1.5f);
    public FP TerminalVelocityGroundpound = FF(-11.25f);

    // --- Pipes
    public byte PipeEnterDuration = 90;

    // --- Swimming
    public FP SwimJumpVelocity = FF(2.26318359375f);
    public FP SwimMaxVerticalVelocity = FF(4.833984375f);
    public FP SwimTerminalVelocityButtonHeld = FF(-0.9375f);
    public FP SwimTerminalVelocity = FF(-2.8125f);
    public FP SwimDeceleration = FF(1.7578125f);

    public FP[] SwimMaxVelocity = FF(new[] { 0f, 2.109375f });
    public FP[] SwimAcceleration = FF(new[] { 1.7578125f, 3.076171875f, 0.439453125f });
    public FP[] SwimShellMaxVelocity = FF(new[] { 3.1640625f });
    public FP[] SwimShellAcceleration = FF(new[] { 6.15234375f, 6.15234375f });

    public FP[] SwimWalkMaxVelocity = FF(new[] { 1.0546875f, 1.0546875f });
    public FP[] SwimWalkAcceleration = FF(new[] { 3.07617875f, 1.7578125f });
    public FP[] SwimWalkShellMaxVelocity = FF(new[] { 1.58203125f, 1.58203125f });
    public FP[] SwimWalkShellAcceleration = FF(new[] { 6.15234375f, 6.15234375f });
    public FP SwimGroundpoundDeceleration = FF(38.671875f);

    // --- Flying
    public FP[] FlyingMaxVelocity = FF(new[] { 1.12060546875f, 2.8125f });
    public FP[] FlyingAcceleration = FF(new[] { 7.91015625f, 3.955078125f });
    public FP SpinnerLaunchVelocity = 12;

    // --- Knockback
    public FP KnockbackDeceleration = FF(7.9101585f);

    // --- Sliding
    public FP SlideMaxVelocity = FF(7.5f);
    public FP SlideFastAcceleration = FF(13.1835975f);
    public FP SlideSlowAcceleration = FF(5.2734375f);
    public FP SlideMinimumAngle = FF(12.5f);

    // --- Groundpounding
    public FPVector2 GroundpoundStartVelocity = FPVector2.Up * FP._1_50;
    public byte GroundpoundStartFrames = 16;
    public byte GroundpoundStartMegaFrames = 24;

    // --- Powerups
    public byte ProjectileVolleySize = 2;
    public byte ProjectileVolleyFrames = 75;
    public byte ProjectileDelayFrames = 6;
    public byte MaxProjecitles = 6;

    public byte PropellerSpinFrames = 30;
    public FP PropellerLaunchVelocity = 6;
    public byte PropellerLaunchFrames = 60;

    private static FP FF(float x) {
        return FP.FromFloat_UNSAFE(x);
    }

    private static FP[] FF(float[] x) {
        return x.Select(FP.FromFloat_UNSAFE).ToArray();
    }
}