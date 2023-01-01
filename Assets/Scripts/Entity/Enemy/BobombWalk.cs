using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Utils;

public class BobombWalk : HoldableEntity {

    //---Networked Variables
    [Networked] public TickTimer DetonationTimer { get; set; }
    [Networked] private Vector3 PreviousFrameVelocity { get; set; }

    //---Serialized Variables
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float walkSpeed = 0.6f, kickSpeed = 4.5f, detonationTime = 4f;
    [SerializeField] private int explosionTileSize = 1;

    //---Misc Variables
    private MaterialPropertyBlock mpb;
    private GameObject explosion;

    //---Properties
    public bool Lit => !DetonationTimer.ExpiredOrNotRunning(Runner);

    public override void Spawned() {
        base.Spawned();
        body.velocity = new(walkSpeed * (FacingRight ? 1 : -1), body.velocity.y);
    }

    public override void Render() {
        base.Render();

        if (!Lit)
            return;

        float timeUntilDetonation = DetonationTimer.RemainingTime(Runner) ?? 0f;
        float redOverlayPercent = 5.39f / (timeUntilDetonation + 2.695f) * 10f % 1f;

        if (mpb == null)
            sRenderer.GetPropertyBlock(mpb = new());

        mpb.SetFloat("FlashAmount", redOverlayPercent);
        sRenderer.SetPropertyBlock(mpb);
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (IsFrozen || IsDead)
            return;

        if (HandleCollision())
            return;

        sRenderer.flipX = !FacingRight;

        if (DetonationTimer.Expired(Runner)) {
            Detonate();
            return;
        }

        if (!Lit)
            body.velocity = new(walkSpeed * (FacingRight ? 1 : -1), body.velocity.y);

        PreviousFrameVelocity = body.velocity;
    }

    private bool HandleCollision() {
        if (Holder)
            return false;

        physics.UpdateCollisions();
        if (Lit && physics.OnGround) {
            //apply friction
            body.velocity -= body.velocity * (Runner.DeltaTime * 3.5f);
            if (Mathf.Abs(body.velocity.x) < 0.05) {
                body.velocity = new(0, body.velocity.y);
            }
        }

        if (physics.HitRight && FacingRight) {
            Turnaround(false);
        } else if (physics.HitLeft && !FacingRight) {
            Turnaround(true);
        }

        if (physics.OnGround && physics.HitRoof) {
            Detonate();
            return true;
        }

        return false;
    }

    public void Light() {
        if (Lit)
            return;

        animator.SetTrigger("lit");
        DetonationTimer = TickTimer.CreateFromSeconds(Runner, detonationTime);
        body.velocity = Vector2.zero;
        PlaySound(Enums.Sounds.Enemy_Bobomb_Fuse);
    }

    public void Detonate() {
        IsDead = true;

        //disable hitbox and sprite
        sRenderer.enabled = false;
        hitbox.enabled = false;

        //spawn explosion
        if (!explosion)
            explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        //damage entities in range. TODO: change to nonalloc?
        List<Collider2D> hits = new();
        Runner.GetPhysicsScene2D().OverlapCircle(body.position + hitbox.offset, 1f, default, hits);

        //use distinct to only damage enemies once
        foreach (GameObject hitObj in hits.Select(c => c.gameObject).Distinct()) {
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
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(body.position);
        Tilemap tm = GameManager.Instance.tilemap;
        for (int x = -explosionTileSize; x <= explosionTileSize; x++) {
            for (int y = -explosionTileSize; y <= explosionTileSize; y++) {
                //use taxi-cab distance to make a somewhat circular explosion
                if (Mathf.Abs(x) + Mathf.Abs(y) > explosionTileSize)
                    continue;

                Vector3Int ourLocation = tileLocation + new Vector3Int(x, y, 0);
                Utils.WrapTileLocation(ref ourLocation);

                TileBase tile = tm.GetTile(ourLocation);
                if (tile is InteractableTile iTile)
                    iTile.Interact(this, InteractableTile.InteractionDirection.Up, Utils.TilemapToWorldPosition(ourLocation));
            }
        }

        //suicide ourselves
        Runner.Despawn(Object);
    }

    public void Turnaround(bool hitWallOnLeft) {
        FacingRight = hitWallOnLeft;
        body.velocity = new((Lit ? Mathf.Abs(PreviousFrameVelocity.x) : walkSpeed) * (FacingRight ? 1 : -1), body.velocity.y);

        if (Runner.IsForward)
            animator.SetTrigger("turnaround");
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {

        //temporary invincibility, we dont want to spam the kick sound
        if (PreviousHolder == player && !ThrowInvincibility.ExpiredOrNotRunning(Runner))
            return;

        //TODO: rewrite?
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f;

        if (!attackedFromAbove && player.State == Enums.PowerupState.BlueShell && player.IsCrouching && !player.IsInShell) {
            FacingRight = damageDirection.x < 0;

        } else if (player.IsSliding || player.IsInShell || player.IsStarmanInvincible) {
            SpecialKill(player.body.velocity.x > 0, false, player.StarCombo++);
            return;

        } else if (attackedFromAbove && !Lit) {
            if (player.State != Enums.PowerupState.MiniMushroom || (player.IsGroundpounding && attackedFromAbove))
                Light();

            PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
            if (player.IsGroundpounding && player.State != Enums.PowerupState.MiniMushroom) {
                Kick(player, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
            } else {
                player.DoEntityBounce = true;
                player.IsGroundpounding = false;
            }
            player.IsDrilling = false;
        } else {
            if (Lit) {
                if (!Holder) {
                    if (player.CanPickupItem) {
                        Pickup(player);
                    } else {
                        Kick(player, player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
                    }
                }
            } else if (player.IsDamageable) {
                player.Powerdown(false);
                FacingRight = damageDirection.x > 0;
            }
        }
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
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
}
