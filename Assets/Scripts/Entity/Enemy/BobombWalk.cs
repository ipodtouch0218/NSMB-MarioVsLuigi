using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Tiles;
using NSMB.Utils;

public class BobombWalk : HoldableEntity {

    //---Static Variables
    private static readonly List<Collider2D> DetonationHits = new();

    //---Networked Variables
    [Networked(OnChanged = nameof(OnDetonationTimerChanged))] public TickTimer DetonationTimer { get; set; }
    [Networked(OnChanged = nameof(OnIsDetonatedChanged))] private NetworkBool IsDetonated { get; set; }

    //---Serialized Variables
    [SerializeField] private GameObject explosionPrefab;
#pragma warning disable CS0414
    [SerializeField] private float walkSpeed = 0.6f, kickSpeed = 4.5f, detonationTime = 4f;
#pragma warning restore CS0414
    [SerializeField] private int explosionTileSize = 1;

    //---Misc Variables
    private MaterialPropertyBlock mpb;

    //---Properties
    public bool Lit => !DetonationTimer.ExpiredOrNotRunning(Runner);

    public override void Spawned() {
        base.Spawned();
        body.velocity = new(walkSpeed * (FacingRight ? 1 : -1), body.velocity.y);
        mpb ??= new();
    }

    public override void Render() {
        base.Render();

        if (!Lit) {
            mpb.SetFloat("FlashAmount", 0);
            sRenderer.SetPropertyBlock(mpb);
            return;
        }

        float timeUntilDetonation = DetonationTimer.RemainingTime(Runner) ?? 0f;
        float redOverlayPercent = 5.39f / (timeUntilDetonation + 2.695f) * 10f % 1f;

        mpb.SetFloat("FlashAmount", redOverlayPercent);
        sRenderer.SetPropertyBlock(mpb);
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (GameManager.Instance && GameManager.Instance.GameEnded) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (!Object || IsFrozen || IsDead)
            return;

        if (HandleCollision())
            return;

        if (DetonationTimer.Expired(Runner)) {
            Detonate();
            return;
        }

        if (!Lit)
            body.velocity = new(walkSpeed * (FacingRight ? 1 : -1), body.velocity.y);
    }

    private bool HandleCollision() {
        if (Holder)
            return false;

        PhysicsEntity.PhysicsDataStruct data = physics.UpdateCollisions();

        if (Lit && data.OnGround) {
            //apply friction
            body.velocity -= body.velocity * (Runner.DeltaTime * 3.5f);
            if (Mathf.Abs(body.velocity.x) < 0.05) {
                body.velocity = new(0, body.velocity.y);
            }
        }

        if (data.HitRight && FacingRight) {
            Turnaround(false);
        } else if (data.HitLeft && !FacingRight) {
            Turnaround(true);
        }

        if (data.OnGround && data.HitRoof) {
            Detonate();
            return true;
        }

        return false;
    }

    public void Light() {
        if (Lit)
            return;

        DetonationTimer = TickTimer.CreateFromSeconds(Runner, detonationTime);
        body.velocity = Vector2.zero;
    }

    public void Detonate() {
        IsDead = true;
        IsDetonated = true;

        //damage entities in range. TODO: change to nonalloc?
        DetonationHits.Clear();
        Runner.GetPhysicsScene2D().OverlapCircle(body.position + hitbox.offset, 1f, default, DetonationHits);

        //use distinct to only damage enemies once
        foreach (GameObject hitObj in DetonationHits.Select(c => c.gameObject).Distinct()) {
            //don't interact with ourselves
            if (hitObj == gameObject)
                continue;

            //interact with players by powerdown-ing them
            if (hitObj.GetComponentInParent<PlayerController>() is PlayerController player) {
                player.Powerdown(false);
                continue;
            }

            //interact with other entities by special killing htem
            if (hitObj.GetComponentInParent<KillableEntity>() is KillableEntity en) {
                en.SpecialKill(transform.position.x < hitObj.transform.position.x, false, 0);
                continue;
            }
        }

        //(sort or) 'splode tiles in range.
        Vector2Int tileLocation = Utils.WorldToTilemapPosition(body.position);
        TileManager tm = GameManager.Instance.tileManager;
        for (int x = -explosionTileSize; x <= explosionTileSize; x++) {
            for (int y = -explosionTileSize; y <= explosionTileSize; y++) {
                //use taxi-cab distance to make a somewhat circular explosion
                if (Mathf.Abs(x) + Mathf.Abs(y) > explosionTileSize)
                    continue;

                Vector2Int ourLocation = tileLocation + new Vector2Int(x, y);
                Utils.WrapTileLocation(ref ourLocation);

                TileBase tile = tm.GetTile(ourLocation);
                if (tile is InteractableTile iTile)
                    iTile.Interact(this, InteractableTile.InteractionDirection.Up, Utils.TilemapToWorldPosition(ourLocation));
            }
        }

        //suicide ourselves
        DespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
    }

    public void Turnaround(bool hitWallOnLeft) {
        FacingRight = hitWallOnLeft;
        body.velocity = new((Lit ? Mathf.Abs(physics.previousTickVelocity.x) : walkSpeed) * (FacingRight ? 1 : -1), body.velocity.y);

        if (Runner.IsForward)
            animator.SetTrigger("turnaround");
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {

        // Temporary invincibility, we dont want to spam the kick sound
        if (PreviousHolder == player && !ThrowInvincibility.ExpiredOrNotRunning(Runner))
            return;

        // Special insta-kill cases
        if (player.InstakillsEnemies) {
            SpecialKill(player.body.velocity.x > 0, false, player.StarCombo++);
            return;
        }

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;

        // Normal interactions
        if (Lit) {
            if (!Holder && player.CanPickupItem) {
                // pickup by player
                Pickup(player);
            } else {
                // kicked by player
                Kick(player, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
            }
        } else {
            if (attackedFromAbove) {
                // light
                bool mini = player.State == Enums.PowerupState.MiniMushroom;
                if (!mini || player.IsGroundpounding)
                    Light();

                if (!mini && player.IsGroundpounding) {
                    Kick(player, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
                } else {
                    player.DoEntityBounce = true;
                    player.IsGroundpounding = false;
                }

                player.IsDrilling = false;
            } else if (player.IsCrouchedInShell) {
                // Bounce off blue shell crouched player
                FacingRight = damageDirection.x < 0;
                return;
            } else if (player.IsDamageable) {
                // damage
                player.Powerdown(false);
                FacingRight = damageDirection.x > 0;
            }
        }
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction) {
        //Light if we get bumped
        Light();
    }

    //---IFireballInteractable overrides
    public override bool InteractWithFireball(FireballMover fireball) {
        if (!Lit) {
            Light();
        } else {
            Kick(null, fireball.FacingRight, 0f, false);
        }
        return true;
    }

    //---KillableEntity overrides
    public override void Kill() {
        Light();
    }

    public override void RespawnEntity() {
        if (IsActive)
            return;

        base.RespawnEntity();
        IsDetonated = false;
        DetonationTimer = TickTimer.None;
    }

    public override void OnIsDeadChanged() {
        base.OnIsDeadChanged();

        if (!IsDead) {
            sfx.Stop();
        }
    }

    public override void SpecialKill(bool right, bool groundpound, int combo) {
        base.SpecialKill(right, groundpound, combo);

        //stop the fuse sound if we were killed early (by shell, for example).
        sfx.Stop();
    }

    protected override void CheckForEntityCollisions() {
        base.CheckForEntityCollisions();
        if (IsDead || !Lit || Mathf.Abs(body.velocity.x) < 1f)
            return;

        int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + hitbox.offset, hitbox.size, 0, default, CollisionBuffer);

        for (int i = 0; i < count; i++) {
            GameObject obj = CollisionBuffer[i].gameObject;

            if (obj == gameObject)
                continue;

            //killable entities
            if (obj.TryGetComponent(out KillableEntity killable)) {
                if (killable.IsDead)
                    continue;

                //kill entity we ran into
                killable.SpecialKill(killable.body.position.x > body.position.x, false, ComboCounter++);

                //kill ourselves if we're being held too
                if (Holder)
                    SpecialKill(killable.body.position.x < body.position.x, false, 0);

                continue;
            }
        }
    }

    //---ThrowableEntity overrides
    public override void Kick(PlayerController kicker, bool toRight, float speed, bool groundpound) {
        //always do a groundpound variant kick
        base.Kick(kicker, toRight, speed, true);
    }

    //---OnChangeds
    private GameObject explosion;
    public static void OnIsDetonatedChanged(Changed<BobombWalk> changed) {
        BobombWalk bomb = changed.Behaviour;

        if (bomb.IsDetonated) {
            //spawn explosion
            if (!bomb.explosion)
                bomb.explosion = Instantiate(bomb.explosionPrefab, bomb.transform.position, Quaternion.identity);

            bomb.sRenderer.enabled = false;
            bomb.sfx.Pause();
        } else {
            bomb.sRenderer.enabled = true;
            bomb.sfx.UnPause();
        }
    }

    public static void OnDetonationTimerChanged(Changed<BobombWalk> changed) {
        BobombWalk bomb = changed.Behaviour;
        bool lit = bomb.Lit;
        bomb.animator.SetBool("lit", lit);

        if (!lit)
            return;

        bomb.PlaySound(Enums.Sounds.Enemy_Bobomb_Fuse);
    }
}
