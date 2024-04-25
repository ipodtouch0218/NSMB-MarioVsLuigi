using System.Linq;
using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;
using NSMB.Utils;

namespace NSMB.Entities.Collectable.Powerups {

    //[OrderAfter(typeof(PlayerController), typeof(EntityMover))]
    public class Powerup : CollectableEntity, IBlockBumpable {

        //---Static Variables
        private static LayerMask GroundMask;
        private static readonly int OriginalSortingOrder = 10;

        //---Networked Variables
        [Networked] protected PlayerController FollowPlayer { get; set; }
        [Networked] private TickTimer IgnorePlayerTimer { get; set; }
        [Networked] private Vector2 BlockSpawnOrigin { get; set; }
        [Networked] private Vector2 BlockSpawnDestination { get; set; }
        [Networked] private NetworkBool BlockSpawn { get; set; }
        [Networked] private float BlockSpawnAnimationLength { get; set; }
        [Networked] private NetworkBool LaunchSpawn { get; set; }
        [Networked] public TickTimer SpawnAnimationTimer { get; set; }
        [Networked] public int SpawnTick { get; set; }

        //---Public Variables
        public PowerupScriptable powerupScriptable;

        //---Serialized Variables
        [SerializeField] private float speed, bouncePower, terminalVelocity = 4, blinkingRate = 4, scaleSize = 0.5f, scaleRate = 30f/4f;
        [SerializeField] private Vector2 launchVelocity = new Vector2(4f, 9f);
        [SerializeField] private bool avoidPlayers;

        //---Components
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private Animator childAnimator;
        [SerializeField] private Animation childAnimation;
        [SerializeField] private BoxCollider2D hitbox;
        [SerializeField] private ParticleSystem particles;
        private IPowerupCollect collectScript;

        //---Private Variables
        private MaterialPropertyBlock mpb;
        private bool disableSpawnAnimation;

        public override void OnValidate() {
            base.OnValidate();
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref hitbox);
            this.SetIfNull(ref childAnimator, UnityExtensions.GetComponentType.Children);
        }

        public void Awake() {
            collectScript = GetComponent<IPowerupCollect>();

            if (GroundMask == default) {
                GroundMask = (1 << Layers.LayerGround) | (1 << Layers.LayerPassthrough);
            }
        }

        public void OnBeforeSpawned(float pickupDelay) {
            SpawnAnimationTimer = TickTimer.CreateFromSeconds(Runner, pickupDelay);
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, 10f + pickupDelay);
        }

        public void OnBeforeSpawned(PlayerController playerToFollow) {
            OnBeforeSpawned(1);

            FollowPlayer = playerToFollow;
            transform.position = body.Position = new(playerToFollow.transform.position.x, playerToFollow.cameraController.CurrentPosition.y + 1.68f);
        }

        public void OnBeforeSpawned(float pickupDelay, Vector2 spawnOrigin, Vector2 spawnDestination, bool launch = false) {
            OnBeforeSpawned(pickupDelay);

            LaunchSpawn = launch;
            BlockSpawn = !launch;
            BlockSpawnOrigin = spawnOrigin;
            BlockSpawnDestination = spawnDestination;
            BlockSpawnAnimationLength = pickupDelay;
            body.Position = spawnOrigin;

            if (BlockSpawnDestination.y < BlockSpawnOrigin.y) {
                // Downwards powerup, adjust based on the powerup's height.
                BlockSpawnDestination = new(BlockSpawnOrigin.x, BlockSpawnOrigin.y - sRenderer.bounds.size.y);
            }
        }

        public override void Spawned() {
            base.Spawned();
            SpawnTick = Runner.Tick;

            if (Runner.Topology == Topologies.ClientServer) {
                Runner.SetIsSimulated(Object, true);
            }

            if (FollowPlayer) {
                // Spawned following a player.
                body.Freeze = true;
                gameObject.layer = Layers.LayerHitsNothing;
                sRenderer.sortingOrder = 15;
                if (childAnimator) {
                    childAnimator.enabled = false;
                }

                sRenderer.GetPropertyBlock(mpb = new());

                PlaySound(Enums.Sounds.Player_Sound_PowerupReserveUse);

            } else {
                if (BlockSpawn) {
                    // Spawned from a block.
                    body.Freeze = true;
                    gameObject.layer = Layers.LayerHitsNothing;
                    sRenderer.sortingOrder = -1000;

                    if (childAnimation) {
                        childAnimation.Play();
                    }
                } else if (LaunchSpawn) {
                    // Spawn with velocity
                    body.Freeze = false;
                    body.Velocity = launchVelocity;
                    sRenderer.sortingOrder = -1000;
                    gameObject.layer = Layers.LayerHitsNothing;

                } else {
                    // Spawned by any other means (blue koopa, usually.)
                    body.Freeze = false;
                    gameObject.layer = Layers.LayerEntityNoGroundEntity;
                    sRenderer.sortingOrder = OriginalSortingOrder;
                }

                PlaySound(powerupScriptable.powerupBlockEffect);
            }

            FacingRight = true;
        }

        public override void Render() {
            base.Render();
            if (Collector) {
                return;
            }

            if (childAnimator) {
                childAnimator.SetBool("onGround", body.Data.OnGround);
            }

            HandleSpawningAnimation();
            HandleDespawningBlinking();
        }

        public override void FixedUpdateNetwork() {
            base.FixedUpdateNetwork();

            if (!Object || Collector) {
                return;
            }

            if (GameManager.Instance && GameManager.Instance.GameEnded) {
                body.Velocity = Vector2.zero;
                body.Freeze = true;
                return;
            }

            if (FollowPlayer) {
                // Attached to a player. Don't interact, and follow the player.
                body.Position = new(FollowPlayer.body.Position.x, FollowPlayer.cameraController.CurrentPosition.y + 1.68f);

                if (SpawnAnimationTimer.ExpiredOrNotRunning(Runner)) {
                    SpawnAnimationTimer = TickTimer.None;
                    FollowPlayer = null;
                    body.Freeze = false;
                    sRenderer.sortingOrder = OriginalSortingOrder;
                    sRenderer.gameObject.transform.localScale = Vector3.one;
                    if (childAnimator) {
                        childAnimator.enabled = true;
                    }
                } else {
                    return;
                }
            } else if (BlockSpawn) {
                // Spawning from a block. Lerp between origin & destination.
                float remaining = SpawnAnimationTimer.RemainingTime(Runner) ?? 0f;
                float t = 1f - (remaining / BlockSpawnAnimationLength);
                body.Position = Vector2.Lerp(BlockSpawnOrigin, BlockSpawnDestination, t);

                if (SpawnAnimationTimer.ExpiredOrNotRunning(Runner)) {

                    if (Utils.Utils.IsTileSolidAtWorldLocation(body.Position + hitbox.offset)) {
                        DespawnEntity();
                        return;
                    }

                    SpawnAnimationTimer = TickTimer.None;
                    BlockSpawn = false;
                    sRenderer.sortingOrder = OriginalSortingOrder;
                    body.Freeze = false;
                } else {
                    //sRenderer.enabled = true;
                    return;
                }

                return;
            } else if (LaunchSpawn) {
                // Back to normal layers
                if (Runner.Tick - SpawnTick > 5) {
                    sRenderer.sortingOrder = OriginalSortingOrder;
                    gameObject.layer = Layers.LayerEntityNoGroundEntity;
                    LaunchSpawn = false;
                }
            }

            Vector2 size = hitbox.size * transform.lossyScale * 0.7f;
            Vector2 origin = body.Position + hitbox.offset * transform.lossyScale;

            //// TODO: bug here somewhere. Client powerups jitter
            if (Utils.Utils.IsAnyTileSolidBetweenWorldBox(origin, size) || Runner.GetPhysicsScene2D().OverlapBox(origin, size, 0, GroundMask)) {
                gameObject.layer = Layers.LayerHitsNothing;
                return;
            } else {
                gameObject.layer = Layers.LayerEntityNoGroundEntity;
                HandleCollision();
            }

            if (avoidPlayers && body.Data.OnGround) {
                PlayerController closest =
                    GameManager.Instance.AlivePlayers
                    .OrderBy(player => Utils.Utils.WrappedDistance(body.Position, player.body.Position))
                    .FirstOrDefault();

                if (closest) {
                    FacingRight = Utils.Utils.WrappedDirectionSign(closest.body.Position, body.Position) == -1;
                }
            }

            body.Velocity = new(body.Velocity.x, Mathf.Max(-terminalVelocity, body.Velocity.y));
        }

        private void HandleSpawningAnimation() {

            if (FollowPlayer && SpawnAnimationTimer.IsActive(Runner)) {

                float timeRemaining = SpawnAnimationTimer.RemainingRenderTime(Runner) ?? 0f;
                float adjustment = Mathf.PingPong(timeRemaining, scaleRate) / scaleRate * scaleSize;
                sRenderer.transform.localScale = Vector3.one * (1 + adjustment);
                transform.position = new(FollowPlayer.transform.position.x, FollowPlayer.cameraController.TargetCamera.transform.position.y + 1.68f);

                if (!disableSpawnAnimation) {
                    mpb.SetFloat("WaveEnabled", 0);
                    sRenderer.SetPropertyBlock(mpb);

                    disableSpawnAnimation = true;
                }

            } else if (disableSpawnAnimation) {

                sRenderer.transform.localScale = Vector3.one;
                disableSpawnAnimation = false;

                mpb.SetFloat("WaveEnabled", 1);
                sRenderer.SetPropertyBlock(mpb);
            }
        }

        private void HandleDespawningBlinking() {
            float despawnTimeRemaining = DespawnTimer.RemainingTime(Runner) ?? 0f;
            if (despawnTimeRemaining < 1) {
                sRenderer.enabled = (despawnTimeRemaining * blinkingRate % 1) > 0.5f;
            }
        }

        public void HandleCollision() {

            PhysicsDataStruct data = body.Data;

            if (data.HitLeft || data.HitRight) {
                FacingRight = data.HitLeft;
                body.Velocity = new(speed * (FacingRight ? 1 : -1), body.Velocity.y);
            }

            if (data.OnGround) {
                body.Velocity = new(speed * (FacingRight ? 1 : -1), Mathf.Max(body.Velocity.y, bouncePower));

                if (data.HitRoof || (data.HitLeft && data.HitRight)) {
                    DespawnEntity();
                    return;
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState) {
            if (!Collector) {
                GameManager.Instance.particleManager.Play(Enums.Particle.Generic_Puff, body.Position + hitbox.offset);
            }
        }

        //---IBlockBumpable overrides
        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractionDirection direction) {
            if (direction == InteractionDirection.Down || FollowPlayer) {
                return;
            }

            body.Velocity = new(body.Velocity.x, 5f);
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {

            // Fixes players hitting multiple colliders at once (propeller)
            if (!Object || !Object.IsValid) {
                return;
            }

            // Don't be collectable if someone already collected us
            if (Collector) {
                return;
            }

            // Don't be collectable if we're following a player / spawning
            if (BlockSpawn && (SpawnAnimationTimer.RemainingTime(Runner) ?? 0f) > 0.1f) {
                return;
            }

            if (!BlockSpawn && SpawnAnimationTimer.IsActive(Runner)) {
                return;
            }

            // Don't collect if we're ignoring players (usually, after blue shell spawns from a blue koopa,
            // so we dont collect it instantly)
            if (IgnorePlayerTimer.IsActive(Runner)) {
                return;
            }

            Collector = player;

            // Change the player's powerup state
            Enums.PowerupState oldState = player.State;
            PowerupScriptable newPowerup = powerupScriptable;
            Enums.PowerupState newState = newPowerup.state;

            PowerupReserveResult result = collectScript.OnPowerupCollect(player, newPowerup);

            switch (result) {
            case PowerupReserveResult.ReserveOldPowerup: {
                if (oldState != Enums.PowerupState.NoPowerup) {
                    player.SetReserveItem(oldState);
                }

                break;
            }
            case PowerupReserveResult.ReserveNewPowerup: {
                player.SetReserveItem(newState);
                break;
            }
            }

            DespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            IsActive = false;

            if (HasStateAuthority) {
                Rpc_CollectedPowerup(player, result);
            }
        }

        //---CollectableEntity overrides
        public override void OnCollectedChanged() {
            sRenderer.enabled = !Collector;

            if (particles) {
                if (Collector) {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                } else {
                    particles.Play(true);
                }
            }
        }

        //---RPCs
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_CollectedPowerup(PlayerController collector, PowerupReserveResult result) {
            switch (result) {
            case PowerupReserveResult.ReserveOldPowerup:
            case PowerupReserveResult.NoneButPlaySound: {
                // Just play the collect sound
                if (powerupScriptable.soundPlaysEverywhere) {
                    collector.PlaySoundEverywhere(powerupScriptable.soundEffect);
                } else {
                    collector.PlaySound(powerupScriptable.soundEffect);
                }
                break;
            }
            case PowerupReserveResult.ReserveNewPowerup: {
                // Reserve the new powerup
                collector.PlaySound(Enums.Sounds.Player_Sound_PowerupReserveStore);
                break;
            }
            }
        }
    }
}
