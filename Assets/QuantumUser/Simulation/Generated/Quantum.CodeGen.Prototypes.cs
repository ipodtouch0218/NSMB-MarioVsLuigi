// <auto-generated>
// This code was auto-generated by a tool, every time
// the tool executes this code will be reset.
//
// If you need to extend the classes generated to add
// fields or methods to them, please create partial
// declarations in another file.
// </auto-generated>
#pragma warning disable 0109
#pragma warning disable 1591


namespace Quantum.Prototypes {
  using Photon.Deterministic;
  using Quantum;
  using Quantum.Core;
  using Quantum.Collections;
  using Quantum.Inspector;
  using Quantum.Physics2D;
  using Quantum.Physics3D;
  using Byte = System.Byte;
  using SByte = System.SByte;
  using Int16 = System.Int16;
  using UInt16 = System.UInt16;
  using Int32 = System.Int32;
  using UInt32 = System.UInt32;
  using Int64 = System.Int64;
  using UInt64 = System.UInt64;
  using Boolean = System.Boolean;
  using String = System.String;
  using Object = System.Object;
  using FlagsAttribute = System.FlagsAttribute;
  using SerializableAttribute = System.SerializableAttribute;
  using MethodImplAttribute = System.Runtime.CompilerServices.MethodImplAttribute;
  using MethodImplOptions = System.Runtime.CompilerServices.MethodImplOptions;
  using FieldOffsetAttribute = System.Runtime.InteropServices.FieldOffsetAttribute;
  using StructLayoutAttribute = System.Runtime.InteropServices.StructLayoutAttribute;
  using LayoutKind = System.Runtime.InteropServices.LayoutKind;
  #if QUANTUM_UNITY //;
  using TooltipAttribute = UnityEngine.TooltipAttribute;
  using HeaderAttribute = UnityEngine.HeaderAttribute;
  using SpaceAttribute = UnityEngine.SpaceAttribute;
  using RangeAttribute = UnityEngine.RangeAttribute;
  using HideInInspectorAttribute = UnityEngine.HideInInspector;
  using PreserveAttribute = UnityEngine.Scripting.PreserveAttribute;
  using FormerlySerializedAsAttribute = UnityEngine.Serialization.FormerlySerializedAsAttribute;
  using MovedFromAttribute = UnityEngine.Scripting.APIUpdating.MovedFromAttribute;
  using CreateAssetMenu = UnityEngine.CreateAssetMenuAttribute;
  using RuntimeInitializeOnLoadMethodAttribute = UnityEngine.RuntimeInitializeOnLoadMethodAttribute;
  #endif //;
  
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.BigStar))]
  public unsafe partial class BigStarPrototype : ComponentPrototype<Quantum.BigStar> {
    public QBoolean IsStationary;
    public Int32 Lifetime;
    public FP Speed;
    public FP BounceForce;
    public QBoolean FacingRight;
    partial void MaterializeUser(Frame frame, ref Quantum.BigStar result, in PrototypeMaterializationContext context);
    public override Boolean AddToEntity(FrameBase f, EntityRef entity, in PrototypeMaterializationContext context) {
        Quantum.BigStar component = default;
        Materialize((Frame)f, ref component, in context);
        return f.Set(entity, component) == SetResult.ComponentAdded;
    }
    public void Materialize(Frame frame, ref Quantum.BigStar result, in PrototypeMaterializationContext context = default) {
        result.IsStationary = this.IsStationary;
        result.Lifetime = this.Lifetime;
        result.Speed = this.Speed;
        result.BounceForce = this.BounceForce;
        result.FacingRight = this.FacingRight;
        MaterializeUser(frame, ref result, in context);
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.CameraController))]
  public unsafe partial class CameraControllerPrototype : ComponentPrototype<Quantum.CameraController> {
    public FPVector2 CurrentPosition;
    public FP LastFloorHeight;
    public FPVector2 LastPlayerPosition;
    public FPVector2 SmoothDampVelocity;
    partial void MaterializeUser(Frame frame, ref Quantum.CameraController result, in PrototypeMaterializationContext context);
    public override Boolean AddToEntity(FrameBase f, EntityRef entity, in PrototypeMaterializationContext context) {
        Quantum.CameraController component = default;
        Materialize((Frame)f, ref component, in context);
        return f.Set(entity, component) == SetResult.ComponentAdded;
    }
    public void Materialize(Frame frame, ref Quantum.CameraController result, in PrototypeMaterializationContext context = default) {
        result.CurrentPosition = this.CurrentPosition;
        result.LastFloorHeight = this.LastFloorHeight;
        result.LastPlayerPosition = this.LastPlayerPosition;
        result.SmoothDampVelocity = this.SmoothDampVelocity;
        MaterializeUser(frame, ref result, in context);
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.Coin))]
  public unsafe partial class CoinPrototype : ComponentPrototype<Quantum.Coin> {
    public QBoolean IsFloating;
    public QBoolean IsDotted;
    public Byte DottedChangeTimer;
    partial void MaterializeUser(Frame frame, ref Quantum.Coin result, in PrototypeMaterializationContext context);
    public override Boolean AddToEntity(FrameBase f, EntityRef entity, in PrototypeMaterializationContext context) {
        Quantum.Coin component = default;
        Materialize((Frame)f, ref component, in context);
        return f.Set(entity, component) == SetResult.ComponentAdded;
    }
    public void Materialize(Frame frame, ref Quantum.Coin result, in PrototypeMaterializationContext context = default) {
        result.IsFloating = this.IsFloating;
        result.IsDotted = this.IsDotted;
        result.DottedChangeTimer = this.DottedChangeTimer;
        MaterializeUser(frame, ref result, in context);
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.Input))]
  public unsafe partial class InputPrototype : StructPrototype {
    public Button Up;
    public Button Down;
    public Button Left;
    public Button Right;
    public Button Jump;
    public Button Sprint;
    public Button PowerupAction;
    partial void MaterializeUser(Frame frame, ref Quantum.Input result, in PrototypeMaterializationContext context);
    public void Materialize(Frame frame, ref Quantum.Input result, in PrototypeMaterializationContext context = default) {
        result.Up = this.Up;
        result.Down = this.Down;
        result.Left = this.Left;
        result.Right = this.Right;
        result.Jump = this.Jump;
        result.Sprint = this.Sprint;
        result.PowerupAction = this.PowerupAction;
        MaterializeUser(frame, ref result, in context);
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.Liquid))]
  public unsafe class LiquidPrototype : ComponentPrototype<Quantum.Liquid> {
    public LiquidType LiquidType;
    public Int32 WidthTiles;
    public FP HeightTiles;
    [FreeOnComponentRemoved()]
    [DynamicCollectionAttribute()]
    public MapEntityId[] SplashedEntities = {};
    public override Boolean AddToEntity(FrameBase f, EntityRef entity, in PrototypeMaterializationContext context) {
        Quantum.Liquid component = default;
        Materialize((Frame)f, ref component, in context);
        return f.Set(entity, component) == SetResult.ComponentAdded;
    }
    public void Materialize(Frame frame, ref Quantum.Liquid result, in PrototypeMaterializationContext context = default) {
        result.LiquidType = this.LiquidType;
        result.WidthTiles = this.WidthTiles;
        result.HeightTiles = this.HeightTiles;
        if (this.SplashedEntities.Length == 0) {
          result.SplashedEntities = default;
        } else {
          var list = frame.AllocateList(out result.SplashedEntities, this.SplashedEntities.Length);
          for (int i = 0; i < this.SplashedEntities.Length; ++i) {
            EntityRef tmp = default;
            PrototypeValidator.FindMapEntity(this.SplashedEntities[i], in context, out tmp);
            list.Add(tmp);
          }
        }
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.MarioPlayer))]
  public unsafe class MarioPlayerPrototype : ComponentPrototype<Quantum.MarioPlayer> {
    public AssetRef<MarioPlayerPhysicsInfo> PhysicsAsset;
    public PlayerRef PlayerRef;
    public Byte Team;
    public PowerupState CurrentPowerupState;
    public AssetRef<PowerupAsset> CurrentPowerupScriptable;
    public PowerupState PreviousPowerupState;
    public AssetRef<PowerupAsset> ReserveItem;
    public Byte Stars;
    public Byte Coins;
    public Byte Lives;
    public QBoolean IsDead;
    public QBoolean FireDeath;
    public QBoolean IsRespawning;
    public QBoolean FacingRight;
    public QBoolean IsSkidding;
    public QBoolean IsTurnaround;
    public Byte FastTurnaroundFrames;
    public Byte SlowTurnaroundFrames;
    public JumpState JumpState;
    public Byte JumpLandingFrames;
    public Byte JumpBufferFrames;
    public Byte CoyoteTimeFrames;
    public Int32 LandedFrame;
    public QBoolean WasTouchingGroundLastFrame;
    public QBoolean WallslideLeft;
    public QBoolean WallslideRight;
    public Byte WallslideEndFrames;
    public Byte WalljumpFrames;
    public QBoolean IsGroundpounding;
    public QBoolean IsGroundpoundActive;
    public Byte GroundpoundStartFrames;
    public Byte GroundpoundCooldownFrames;
    public Byte GroundpoundStandFrames;
    public Byte WaterColliderCount;
    public QBoolean SwimExitForceJump;
    public QBoolean IsInKnockback;
    public QBoolean IsInWeakKnockback;
    public QBoolean KnockbackWasOriginallyFacingRight;
    public Byte DamageInvincibilityFrames;
    public QBoolean IsCrouching;
    public QBoolean IsSliding;
    public QBoolean IsSpinnerFlying;
    public QBoolean IsDrilling;
    public Int32 InvincibilityFrames;
    public Byte ProjectileDelayFrames;
    public Byte ProjectileVolleyFrames;
    public Byte CurrentProjectiles;
    public Byte CurrentVolley;
    public QBoolean IsInShell;
    public Byte ShellSlowdownFrames;
    public QBoolean IsPropellerFlying;
    public Byte PropellerLaunchFrames;
    public Byte PropellerSpinFrames;
    public QBoolean UsedPropellerThisJump;
    public Byte PropellerDrillCooldown;
    public MapEntityId HeldEntity;
    public MapEntityId CurrentPipe;
    public override Boolean AddToEntity(FrameBase f, EntityRef entity, in PrototypeMaterializationContext context) {
        Quantum.MarioPlayer component = default;
        Materialize((Frame)f, ref component, in context);
        return f.Set(entity, component) == SetResult.ComponentAdded;
    }
    public void Materialize(Frame frame, ref Quantum.MarioPlayer result, in PrototypeMaterializationContext context = default) {
        result.PhysicsAsset = this.PhysicsAsset;
        result.PlayerRef = this.PlayerRef;
        result.Team = this.Team;
        result.CurrentPowerupState = this.CurrentPowerupState;
        result.CurrentPowerupScriptable = this.CurrentPowerupScriptable;
        result.PreviousPowerupState = this.PreviousPowerupState;
        result.ReserveItem = this.ReserveItem;
        result.Stars = this.Stars;
        result.Coins = this.Coins;
        result.Lives = this.Lives;
        result.IsDead = this.IsDead;
        result.FireDeath = this.FireDeath;
        result.IsRespawning = this.IsRespawning;
        result.FacingRight = this.FacingRight;
        result.IsSkidding = this.IsSkidding;
        result.IsTurnaround = this.IsTurnaround;
        result.FastTurnaroundFrames = this.FastTurnaroundFrames;
        result.SlowTurnaroundFrames = this.SlowTurnaroundFrames;
        result.JumpState = this.JumpState;
        result.JumpLandingFrames = this.JumpLandingFrames;
        result.JumpBufferFrames = this.JumpBufferFrames;
        result.CoyoteTimeFrames = this.CoyoteTimeFrames;
        result.LandedFrame = this.LandedFrame;
        result.WasTouchingGroundLastFrame = this.WasTouchingGroundLastFrame;
        result.WallslideLeft = this.WallslideLeft;
        result.WallslideRight = this.WallslideRight;
        result.WallslideEndFrames = this.WallslideEndFrames;
        result.WalljumpFrames = this.WalljumpFrames;
        result.IsGroundpounding = this.IsGroundpounding;
        result.IsGroundpoundActive = this.IsGroundpoundActive;
        result.GroundpoundStartFrames = this.GroundpoundStartFrames;
        result.GroundpoundCooldownFrames = this.GroundpoundCooldownFrames;
        result.GroundpoundStandFrames = this.GroundpoundStandFrames;
        result.WaterColliderCount = this.WaterColliderCount;
        result.SwimExitForceJump = this.SwimExitForceJump;
        result.IsInKnockback = this.IsInKnockback;
        result.IsInWeakKnockback = this.IsInWeakKnockback;
        result.KnockbackWasOriginallyFacingRight = this.KnockbackWasOriginallyFacingRight;
        result.DamageInvincibilityFrames = this.DamageInvincibilityFrames;
        result.IsCrouching = this.IsCrouching;
        result.IsSliding = this.IsSliding;
        result.IsSpinnerFlying = this.IsSpinnerFlying;
        result.IsDrilling = this.IsDrilling;
        result.InvincibilityFrames = this.InvincibilityFrames;
        result.ProjectileDelayFrames = this.ProjectileDelayFrames;
        result.ProjectileVolleyFrames = this.ProjectileVolleyFrames;
        result.CurrentProjectiles = this.CurrentProjectiles;
        result.CurrentVolley = this.CurrentVolley;
        result.IsInShell = this.IsInShell;
        result.ShellSlowdownFrames = this.ShellSlowdownFrames;
        result.IsPropellerFlying = this.IsPropellerFlying;
        result.PropellerLaunchFrames = this.PropellerLaunchFrames;
        result.PropellerSpinFrames = this.PropellerSpinFrames;
        result.UsedPropellerThisJump = this.UsedPropellerThisJump;
        result.PropellerDrillCooldown = this.PropellerDrillCooldown;
        PrototypeValidator.FindMapEntity(this.HeldEntity, in context, out result.HeldEntity);
        PrototypeValidator.FindMapEntity(this.CurrentPipe, in context, out result.CurrentPipe);
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.PhysicsContact))]
  public unsafe partial class PhysicsContactPrototype : StructPrototype {
    public FPVector2 Position;
    public FPVector2 Normal;
    public FP Distance;
    public Int32 TileX;
    public Int32 TileY;
    partial void MaterializeUser(Frame frame, ref Quantum.PhysicsContact result, in PrototypeMaterializationContext context);
    public void Materialize(Frame frame, ref Quantum.PhysicsContact result, in PrototypeMaterializationContext context = default) {
        result.Position = this.Position;
        result.Normal = this.Normal;
        result.Distance = this.Distance;
        result.TileX = this.TileX;
        result.TileY = this.TileY;
        MaterializeUser(frame, ref result, in context);
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.PhysicsObject))]
  public unsafe partial class PhysicsObjectPrototype : ComponentPrototype<Quantum.PhysicsObject> {
    public FPVector2 Velocity;
    public FPVector2 Gravity;
    public FP TerminalVelocity;
    public QBoolean IsFrozen;
    public QBoolean DisableCollision;
    public QBoolean IsTouchingLeftWall;
    public QBoolean IsTouchingRightWall;
    public QBoolean IsTouchingCeiling;
    public QBoolean IsTouchingGround;
    public FP FloorAngle;
    public QBoolean IsOnSlipperyGround;
    public QBoolean IsOnSlideableGround;
    [DynamicCollectionAttribute()]
    public Quantum.Prototypes.PhysicsContactPrototype[] Contacts = {};
    partial void MaterializeUser(Frame frame, ref Quantum.PhysicsObject result, in PrototypeMaterializationContext context);
    public override Boolean AddToEntity(FrameBase f, EntityRef entity, in PrototypeMaterializationContext context) {
        Quantum.PhysicsObject component = default;
        Materialize((Frame)f, ref component, in context);
        return f.Set(entity, component) == SetResult.ComponentAdded;
    }
    public void Materialize(Frame frame, ref Quantum.PhysicsObject result, in PrototypeMaterializationContext context = default) {
        result.Velocity = this.Velocity;
        result.Gravity = this.Gravity;
        result.TerminalVelocity = this.TerminalVelocity;
        result.IsFrozen = this.IsFrozen;
        result.DisableCollision = this.DisableCollision;
        result.IsTouchingLeftWall = this.IsTouchingLeftWall;
        result.IsTouchingRightWall = this.IsTouchingRightWall;
        result.IsTouchingCeiling = this.IsTouchingCeiling;
        result.IsTouchingGround = this.IsTouchingGround;
        result.FloorAngle = this.FloorAngle;
        result.IsOnSlipperyGround = this.IsOnSlipperyGround;
        result.IsOnSlideableGround = this.IsOnSlideableGround;
        if (this.Contacts.Length == 0) {
          result.Contacts = default;
        } else {
          var list = frame.AllocateList(out result.Contacts, this.Contacts.Length);
          for (int i = 0; i < this.Contacts.Length; ++i) {
            Quantum.PhysicsContact tmp = default;
            this.Contacts[i].Materialize(frame, ref tmp, in context);
            list.Add(tmp);
          }
        }
        MaterializeUser(frame, ref result, in context);
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.Powerup))]
  public unsafe class PowerupPrototype : ComponentPrototype<Quantum.Powerup> {
    public AssetRef<PowerupAsset> Scriptable;
    public QBoolean FacingRight;
    public Int32 Lifetime;
    public QBoolean BlockSpawn;
    public QBoolean LaunchSpawn;
    public FPVector2 BlockSpawnOrigin;
    public FPVector2 BlockSpawnDestination;
    public Byte BlockSpawnAnimationLength;
    public Byte SpawnAnimationFrames;
    public Byte IgnorePlayerFrames;
    public MapEntityId ParentMarioPlayer;
    public FPVector2 AnimationCurveOrigin;
    public FP AnimationCurveTimer;
    public override Boolean AddToEntity(FrameBase f, EntityRef entity, in PrototypeMaterializationContext context) {
        Quantum.Powerup component = default;
        Materialize((Frame)f, ref component, in context);
        return f.Set(entity, component) == SetResult.ComponentAdded;
    }
    public void Materialize(Frame frame, ref Quantum.Powerup result, in PrototypeMaterializationContext context = default) {
        result.Scriptable = this.Scriptable;
        result.FacingRight = this.FacingRight;
        result.Lifetime = this.Lifetime;
        result.BlockSpawn = this.BlockSpawn;
        result.LaunchSpawn = this.LaunchSpawn;
        result.BlockSpawnOrigin = this.BlockSpawnOrigin;
        result.BlockSpawnDestination = this.BlockSpawnDestination;
        result.BlockSpawnAnimationLength = this.BlockSpawnAnimationLength;
        result.SpawnAnimationFrames = this.SpawnAnimationFrames;
        result.IgnorePlayerFrames = this.IgnorePlayerFrames;
        PrototypeValidator.FindMapEntity(this.ParentMarioPlayer, in context, out result.ParentMarioPlayer);
        result.AnimationCurveOrigin = this.AnimationCurveOrigin;
        result.AnimationCurveTimer = this.AnimationCurveTimer;
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.Projectile))]
  public unsafe class ProjectilePrototype : ComponentPrototype<Quantum.Projectile> {
    public AssetRef<ProjectileAsset> Asset;
    public FP Speed;
    public MapEntityId Owner;
    public QBoolean FacingRight;
    public QBoolean HasBounced;
    public QBoolean PlayDestroySound;
    public override Boolean AddToEntity(FrameBase f, EntityRef entity, in PrototypeMaterializationContext context) {
        Quantum.Projectile component = default;
        Materialize((Frame)f, ref component, in context);
        return f.Set(entity, component) == SetResult.ComponentAdded;
    }
    public void Materialize(Frame frame, ref Quantum.Projectile result, in PrototypeMaterializationContext context = default) {
        result.Asset = this.Asset;
        result.Speed = this.Speed;
        PrototypeValidator.FindMapEntity(this.Owner, in context, out result.Owner);
        result.FacingRight = this.FacingRight;
        result.HasBounced = this.HasBounced;
        result.PlayDestroySound = this.PlayDestroySound;
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.StageTileInstance))]
  public unsafe partial class StageTileInstancePrototype : StructPrototype {
    public AssetRef<StageTile> Tile;
    public FP Rotation;
    public FPVector2 Scale;
    partial void MaterializeUser(Frame frame, ref Quantum.StageTileInstance result, in PrototypeMaterializationContext context);
    public void Materialize(Frame frame, ref Quantum.StageTileInstance result, in PrototypeMaterializationContext context = default) {
        result.Tile = this.Tile;
        result.Rotation = this.Rotation;
        result.Scale = this.Scale;
        MaterializeUser(frame, ref result, in context);
    }
  }
  [System.SerializableAttribute()]
  [Quantum.Prototypes.Prototype(typeof(Quantum.WrappingObject))]
  public unsafe partial class WrappingObjectPrototype : ComponentPrototype<Quantum.WrappingObject> {
    [HideInInspector()]
    public Int32 _empty_prototype_dummy_field_;
    partial void MaterializeUser(Frame frame, ref Quantum.WrappingObject result, in PrototypeMaterializationContext context);
    public override Boolean AddToEntity(FrameBase f, EntityRef entity, in PrototypeMaterializationContext context) {
        Quantum.WrappingObject component = default;
        Materialize((Frame)f, ref component, in context);
        return f.Set(entity, component) == SetResult.ComponentAdded;
    }
    public void Materialize(Frame frame, ref Quantum.WrappingObject result, in PrototypeMaterializationContext context = default) {
        MaterializeUser(frame, ref result, in context);
    }
  }
}
#pragma warning restore 0109
#pragma warning restore 1591
