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


namespace Quantum {
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
  
  public unsafe partial class Frame {
    public unsafe partial struct FrameEvents {
      static partial void GetEventTypeCountCodeGen(ref Int32 eventCount) {
        eventCount = 23;
      }
      static partial void GetParentEventIDCodeGen(Int32 eventID, ref Int32 parentEventID) {
        switch (eventID) {
          default: break;
        }
      }
      static partial void GetEventTypeCodeGen(Int32 eventID, ref System.Type result) {
        switch (eventID) {
          case EventMarioPlayerCollectedStar.ID: result = typeof(EventMarioPlayerCollectedStar); return;
          case EventMarioPlayerCollectedCoin.ID: result = typeof(EventMarioPlayerCollectedCoin); return;
          case EventCoinChangedType.ID: result = typeof(EventCoinChangedType); return;
          case EventCoinChangeCollected.ID: result = typeof(EventCoinChangeCollected); return;
          case EventLiquidSplashed.ID: result = typeof(EventLiquidSplashed); return;
          case EventMarioPlayerJumped.ID: result = typeof(EventMarioPlayerJumped); return;
          case EventMarioPlayerGroundpoundStarted.ID: result = typeof(EventMarioPlayerGroundpoundStarted); return;
          case EventMarioPlayerGroundpounded.ID: result = typeof(EventMarioPlayerGroundpounded); return;
          case EventMarioPlayerCrouched.ID: result = typeof(EventMarioPlayerCrouched); return;
          case EventMarioPlayerCollectedPowerup.ID: result = typeof(EventMarioPlayerCollectedPowerup); return;
          case EventMarioPlayerUsedReserveItem.ID: result = typeof(EventMarioPlayerUsedReserveItem); return;
          case EventMarioPlayerWalljumped.ID: result = typeof(EventMarioPlayerWalljumped); return;
          case EventMarioPlayerShotProjectile.ID: result = typeof(EventMarioPlayerShotProjectile); return;
          case EventMarioPlayerUsedPropeller.ID: result = typeof(EventMarioPlayerUsedPropeller); return;
          case EventMarioPlayerPropellerSpin.ID: result = typeof(EventMarioPlayerPropellerSpin); return;
          case EventMarioPlayerDied.ID: result = typeof(EventMarioPlayerDied); return;
          case EventMarioPlayerRespawned.ID: result = typeof(EventMarioPlayerRespawned); return;
          case EventPowerupBecameActive.ID: result = typeof(EventPowerupBecameActive); return;
          case EventProjectileDestroyed.ID: result = typeof(EventProjectileDestroyed); return;
          case EventTileChanged.ID: result = typeof(EventTileChanged); return;
          case EventTileBroken.ID: result = typeof(EventTileBroken); return;
          case EventTimerExpired.ID: result = typeof(EventTimerExpired); return;
          default: break;
        }
      }
      public EventMarioPlayerCollectedStar MarioPlayerCollectedStar(Frame Frame, EntityRef Entity, MarioPlayer Mario) {
        if (_f.IsPredicted) return null;
        var ev = _f.Context.AcquireEvent<EventMarioPlayerCollectedStar>(EventMarioPlayerCollectedStar.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerCollectedCoin MarioPlayerCollectedCoin(Frame Frame, EntityRef Entity, MarioPlayer Mario, Byte Coins, QBoolean ItemSpawned, FPVector2 CoinLocation) {
        if (_f.IsPredicted) return null;
        var ev = _f.Context.AcquireEvent<EventMarioPlayerCollectedCoin>(EventMarioPlayerCollectedCoin.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        ev.Coins = Coins;
        ev.ItemSpawned = ItemSpawned;
        ev.CoinLocation = CoinLocation;
        _f.AddEvent(ev);
        return ev;
      }
      public EventCoinChangedType CoinChangedType(Frame Frame, EntityRef Entity, Coin Coin) {
        var ev = _f.Context.AcquireEvent<EventCoinChangedType>(EventCoinChangedType.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Coin = Coin;
        _f.AddEvent(ev);
        return ev;
      }
      public EventCoinChangeCollected CoinChangeCollected(Frame Frame, EntityRef Entity, Coin Coin) {
        var ev = _f.Context.AcquireEvent<EventCoinChangeCollected>(EventCoinChangeCollected.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Coin = Coin;
        _f.AddEvent(ev);
        return ev;
      }
      public EventLiquidSplashed LiquidSplashed(EntityRef Entity, FP Force, FPVector2 Position, QBoolean Exit) {
        var ev = _f.Context.AcquireEvent<EventLiquidSplashed>(EventLiquidSplashed.ID);
        ev.Entity = Entity;
        ev.Force = Force;
        ev.Position = Position;
        ev.Exit = Exit;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerJumped MarioPlayerJumped(Frame Frame, EntityRef Entity, MarioPlayer Mario, JumpState JumpState) {
        var ev = _f.Context.AcquireEvent<EventMarioPlayerJumped>(EventMarioPlayerJumped.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        ev.JumpState = JumpState;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerGroundpoundStarted MarioPlayerGroundpoundStarted(Frame Frame, EntityRef Entity, MarioPlayer Mario) {
        var ev = _f.Context.AcquireEvent<EventMarioPlayerGroundpoundStarted>(EventMarioPlayerGroundpoundStarted.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerGroundpounded MarioPlayerGroundpounded(Frame Frame, EntityRef Entity, MarioPlayer Mario) {
        var ev = _f.Context.AcquireEvent<EventMarioPlayerGroundpounded>(EventMarioPlayerGroundpounded.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerCrouched MarioPlayerCrouched(Frame Frame, EntityRef Entity, MarioPlayer Mario) {
        var ev = _f.Context.AcquireEvent<EventMarioPlayerCrouched>(EventMarioPlayerCrouched.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerCollectedPowerup MarioPlayerCollectedPowerup(Frame Frame, EntityRef Entity, MarioPlayer Mario, PowerupReserveResult Result, PowerupAsset Scriptable) {
        if (_f.IsPredicted) return null;
        var ev = _f.Context.AcquireEvent<EventMarioPlayerCollectedPowerup>(EventMarioPlayerCollectedPowerup.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        ev.Result = Result;
        ev.Scriptable = Scriptable;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerUsedReserveItem MarioPlayerUsedReserveItem(Frame Frame, EntityRef Entity, MarioPlayer Mario, QBoolean Success) {
        var ev = _f.Context.AcquireEvent<EventMarioPlayerUsedReserveItem>(EventMarioPlayerUsedReserveItem.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        ev.Success = Success;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerWalljumped MarioPlayerWalljumped(Frame Frame, EntityRef Entity, MarioPlayer Mario, FPVector2 Position, QBoolean WasOnRightWall) {
        var ev = _f.Context.AcquireEvent<EventMarioPlayerWalljumped>(EventMarioPlayerWalljumped.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        ev.Position = Position;
        ev.WasOnRightWall = WasOnRightWall;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerShotProjectile MarioPlayerShotProjectile(Frame Frame, EntityRef Entity, MarioPlayer Mario, Projectile Projectile) {
        var ev = _f.Context.AcquireEvent<EventMarioPlayerShotProjectile>(EventMarioPlayerShotProjectile.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        ev.Projectile = Projectile;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerUsedPropeller MarioPlayerUsedPropeller(Frame Frame, EntityRef Entity, MarioPlayer Mario) {
        var ev = _f.Context.AcquireEvent<EventMarioPlayerUsedPropeller>(EventMarioPlayerUsedPropeller.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerPropellerSpin MarioPlayerPropellerSpin(Frame Frame, EntityRef Entity, MarioPlayer Mario) {
        var ev = _f.Context.AcquireEvent<EventMarioPlayerPropellerSpin>(EventMarioPlayerPropellerSpin.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerDied MarioPlayerDied(Frame Frame, EntityRef Entity, MarioPlayer Mario) {
        if (_f.IsPredicted) return null;
        var ev = _f.Context.AcquireEvent<EventMarioPlayerDied>(EventMarioPlayerDied.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        _f.AddEvent(ev);
        return ev;
      }
      public EventMarioPlayerRespawned MarioPlayerRespawned(Frame Frame, EntityRef Entity, MarioPlayer Mario) {
        var ev = _f.Context.AcquireEvent<EventMarioPlayerRespawned>(EventMarioPlayerRespawned.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.Mario = Mario;
        _f.AddEvent(ev);
        return ev;
      }
      public EventPowerupBecameActive PowerupBecameActive(Frame Frame, EntityRef Entity) {
        var ev = _f.Context.AcquireEvent<EventPowerupBecameActive>(EventPowerupBecameActive.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        _f.AddEvent(ev);
        return ev;
      }
      public EventProjectileDestroyed ProjectileDestroyed(Frame Frame, EntityRef Entity, QBoolean PlayEffect) {
        var ev = _f.Context.AcquireEvent<EventProjectileDestroyed>(EventProjectileDestroyed.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.PlayEffect = PlayEffect;
        _f.AddEvent(ev);
        return ev;
      }
      public EventTileChanged TileChanged(Frame Frame, Int32 TileX, Int32 TileY, StageTileInstance NewTile) {
        var ev = _f.Context.AcquireEvent<EventTileChanged>(EventTileChanged.ID);
        ev.Frame = Frame;
        ev.TileX = TileX;
        ev.TileY = TileY;
        ev.NewTile = NewTile;
        _f.AddEvent(ev);
        return ev;
      }
      public EventTileBroken TileBroken(Frame Frame, EntityRef Entity, Int32 TileX, Int32 TileY, StageTileInstance Tile) {
        var ev = _f.Context.AcquireEvent<EventTileBroken>(EventTileBroken.ID);
        ev.Frame = Frame;
        ev.Entity = Entity;
        ev.TileX = TileX;
        ev.TileY = TileY;
        ev.Tile = Tile;
        _f.AddEvent(ev);
        return ev;
      }
      public EventTimerExpired TimerExpired(Frame Frame) {
        var ev = _f.Context.AcquireEvent<EventTimerExpired>(EventTimerExpired.ID);
        ev.Frame = Frame;
        _f.AddEvent(ev);
        return ev;
      }
    }
  }
  public unsafe partial class EventMarioPlayerCollectedStar : EventBase {
    public new const Int32 ID = 1;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    protected EventMarioPlayerCollectedStar(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerCollectedStar() : 
        base(1, EventFlags.Server|EventFlags.Client|EventFlags.Synced) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 41;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerCollectedCoin : EventBase {
    public new const Int32 ID = 2;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    public Byte Coins;
    public QBoolean ItemSpawned;
    public FPVector2 CoinLocation;
    protected EventMarioPlayerCollectedCoin(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerCollectedCoin() : 
        base(2, EventFlags.Server|EventFlags.Client|EventFlags.Synced) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 43;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        hash = hash * 31 + Coins.GetHashCode();
        hash = hash * 31 + ItemSpawned.GetHashCode();
        hash = hash * 31 + CoinLocation.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventCoinChangedType : EventBase {
    public new const Int32 ID = 3;
    public Frame Frame;
    public EntityRef Entity;
    public Coin Coin;
    protected EventCoinChangedType(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventCoinChangedType() : 
        base(3, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 47;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Coin.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventCoinChangeCollected : EventBase {
    public new const Int32 ID = 4;
    public Frame Frame;
    public EntityRef Entity;
    public Coin Coin;
    protected EventCoinChangeCollected(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventCoinChangeCollected() : 
        base(4, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 53;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Coin.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventLiquidSplashed : EventBase {
    public new const Int32 ID = 5;
    public EntityRef Entity;
    public FP Force;
    public FPVector2 Position;
    public QBoolean Exit;
    protected EventLiquidSplashed(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventLiquidSplashed() : 
        base(5, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 59;
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Force.GetHashCode();
        hash = hash * 31 + Position.GetHashCode();
        hash = hash * 31 + Exit.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerJumped : EventBase {
    public new const Int32 ID = 6;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    public JumpState JumpState;
    protected EventMarioPlayerJumped(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerJumped() : 
        base(6, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 61;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        hash = hash * 31 + JumpState.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerGroundpoundStarted : EventBase {
    public new const Int32 ID = 7;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    protected EventMarioPlayerGroundpoundStarted(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerGroundpoundStarted() : 
        base(7, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 67;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerGroundpounded : EventBase {
    public new const Int32 ID = 8;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    protected EventMarioPlayerGroundpounded(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerGroundpounded() : 
        base(8, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 71;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerCrouched : EventBase {
    public new const Int32 ID = 9;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    protected EventMarioPlayerCrouched(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerCrouched() : 
        base(9, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 73;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerCollectedPowerup : EventBase {
    public new const Int32 ID = 10;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    public PowerupReserveResult Result;
    public PowerupAsset Scriptable;
    protected EventMarioPlayerCollectedPowerup(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerCollectedPowerup() : 
        base(10, EventFlags.Server|EventFlags.Client|EventFlags.Synced) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 79;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        hash = hash * 31 + Result.GetHashCode();
        hash = hash * 31 + Scriptable.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerUsedReserveItem : EventBase {
    public new const Int32 ID = 11;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    public QBoolean Success;
    protected EventMarioPlayerUsedReserveItem(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerUsedReserveItem() : 
        base(11, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 83;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        hash = hash * 31 + Success.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerWalljumped : EventBase {
    public new const Int32 ID = 12;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    public FPVector2 Position;
    public QBoolean WasOnRightWall;
    protected EventMarioPlayerWalljumped(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerWalljumped() : 
        base(12, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 89;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        hash = hash * 31 + Position.GetHashCode();
        hash = hash * 31 + WasOnRightWall.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerShotProjectile : EventBase {
    public new const Int32 ID = 13;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    public Projectile Projectile;
    protected EventMarioPlayerShotProjectile(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerShotProjectile() : 
        base(13, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 97;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        hash = hash * 31 + Projectile.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerUsedPropeller : EventBase {
    public new const Int32 ID = 14;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    protected EventMarioPlayerUsedPropeller(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerUsedPropeller() : 
        base(14, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 101;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerPropellerSpin : EventBase {
    public new const Int32 ID = 15;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    protected EventMarioPlayerPropellerSpin(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerPropellerSpin() : 
        base(15, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 103;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerDied : EventBase {
    public new const Int32 ID = 16;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    protected EventMarioPlayerDied(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerDied() : 
        base(16, EventFlags.Server|EventFlags.Client|EventFlags.Synced) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 107;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventMarioPlayerRespawned : EventBase {
    public new const Int32 ID = 17;
    public Frame Frame;
    public EntityRef Entity;
    public MarioPlayer Mario;
    protected EventMarioPlayerRespawned(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventMarioPlayerRespawned() : 
        base(17, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 109;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + Mario.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventPowerupBecameActive : EventBase {
    public new const Int32 ID = 18;
    public Frame Frame;
    public EntityRef Entity;
    protected EventPowerupBecameActive(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventPowerupBecameActive() : 
        base(18, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 113;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventProjectileDestroyed : EventBase {
    public new const Int32 ID = 19;
    public Frame Frame;
    public EntityRef Entity;
    public QBoolean PlayEffect;
    protected EventProjectileDestroyed(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventProjectileDestroyed() : 
        base(19, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 127;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + PlayEffect.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventTileChanged : EventBase {
    public new const Int32 ID = 20;
    public Frame Frame;
    public Int32 TileX;
    public Int32 TileY;
    public StageTileInstance NewTile;
    protected EventTileChanged(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventTileChanged() : 
        base(20, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 131;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + TileX.GetHashCode();
        hash = hash * 31 + TileY.GetHashCode();
        hash = hash * 31 + NewTile.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventTileBroken : EventBase {
    public new const Int32 ID = 21;
    public Frame Frame;
    public EntityRef Entity;
    public Int32 TileX;
    public Int32 TileY;
    public StageTileInstance Tile;
    protected EventTileBroken(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventTileBroken() : 
        base(21, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 137;
        hash = hash * 31 + Frame.GetHashCode();
        hash = hash * 31 + Entity.GetHashCode();
        hash = hash * 31 + TileX.GetHashCode();
        hash = hash * 31 + TileY.GetHashCode();
        hash = hash * 31 + Tile.GetHashCode();
        return hash;
      }
    }
  }
  public unsafe partial class EventTimerExpired : EventBase {
    public new const Int32 ID = 22;
    public Frame Frame;
    protected EventTimerExpired(Int32 id, EventFlags flags) : 
        base(id, flags) {
    }
    public EventTimerExpired() : 
        base(22, EventFlags.Server|EventFlags.Client) {
    }
    public new QuantumGame Game {
      get {
        return (QuantumGame)base.Game;
      }
      set {
        base.Game = value;
      }
    }
    public override Int32 GetHashCode() {
      unchecked {
        var hash = 139;
        hash = hash * 31 + Frame.GetHashCode();
        return hash;
      }
    }
  }
}
#pragma warning restore 0109
#pragma warning restore 1591
