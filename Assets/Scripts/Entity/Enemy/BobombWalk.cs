using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Utils;

public class BobombWalk : HoldableEntity {

    //---Networked Variables
    [Networked] public TickTimer DetonationTimer { get; set; }

    //---Serialized Variables
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float walkSpeed = 0.6f, kickSpeed = 4.5f, detonationTime = 4f;
    [SerializeField] private int explosionTileSize = 2;

    //---Misc Variables
    private MaterialPropertyBlock mpb;
    private Vector3 previousFrameVelocity;

    //---Properties
    public bool Lit { get => !DetonationTimer.ExpiredOrNotRunning(Runner); }


    #region Unity Methods
    public override void Spawned() {
        body.velocity = new(walkSpeed * (FacingRight ? 1 : -1), body.velocity.y);
    }

    public override void FixedUpdateNetwork() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        base.FixedUpdateNetwork();
        if (IsFrozen || Dead)
            return;

        HandleCollision();

        sRenderer.flipX = !FacingRight;

        if (DetonationTimer.Expired(Runner)) {
            Detonate();
            return;
        }

        if (Lit) {
            float timeUntilDetonation = DetonationTimer.RemainingTime(Runner) ?? 0f;
            float redOverlayPercent = 5.39f / (timeUntilDetonation + 2.695f) * 10f % 1f;

            if (mpb == null)
                sRenderer.GetPropertyBlock(mpb = new());

            mpb.SetFloat("FlashAmount", redOverlayPercent);
            sRenderer.SetPropertyBlock(mpb);
        }

        previousFrameVelocity = body.velocity;
    }
    #endregion

    #region Helper Methods
    public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f;

        if (!attackedFromAbove && player.State == Enums.PowerupState.BlueShell && player.IsCrouching && !player.IsInShell) {
            FacingRight = damageDirection.x < 0;

        } else if (player.sliding || player.IsInShell || player.IsStarmanInvincible) {
            SpecialKill(player.body.velocity.x > 0, false, player.StarCombo++);
            return;

        } else if (attackedFromAbove && !Lit) {
            if (player.State != Enums.PowerupState.MiniMushroom || (player.IsGroundpounding && attackedFromAbove))
                Light();

            PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
            if (player.IsGroundpounding && player.State != Enums.PowerupState.MiniMushroom) {
                Kick(player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
            } else {
                player.bounce = true;
                player.IsGroundpounding = false;
            }
            player.IsDrilling = false;
        } else {
            if (Lit) {
                if (!Holder) {
                    if (player.CanPickup()) {
                        Pickup(player);
                    } else {
                        Kick(player.body.position.x < body.position.x, Mathf.Abs(player.body.velocity.x) / player.RunningMaxSpeed, player.IsGroundpounding);
                    }
                }
            } else if (player.IsDamageable) {
                player.Powerdown(false);
                FacingRight = damageDirection.x > 0;
            }
        }
    }

    public override bool InteractWithFireball(FireballMover fireball) {
        if (!Lit) {
            Light();
        } else {
            Kick(fireball.FacingRight, 0f, false);
        }
        return true;
    }

    private void HandleCollision() {
        if (Holder)
            return;

        physics.UpdateCollisions();
        if (Lit && physics.onGround) {
            body.velocity -= body.velocity * (Runner.DeltaTime * 3f);
            if (Mathf.Abs(body.velocity.x) < 0.05) {
                body.velocity = new(0, body.velocity.y);
            }
        }

        if (physics.hitRight && FacingRight) {
            Turnaround(false);
        } else if (physics.hitLeft && !FacingRight) {
            Turnaround(true);
        }

        if (physics.onGround && physics.hitRoof)
            SpecialKill(false, false, 0);
    }
    #endregion

    #region PunRPCs
    public void Detonate() {

        Dead = true;
        sRenderer.enabled = false;
        hitbox.enabled = false;

        Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        List<Collider2D> hits = new();
        Runner.GetPhysicsScene2D().OverlapCircle(body.position + hitbox.offset, 1f, default, hits);

        foreach (Collider2D hit in hits.Distinct()) {
            GameObject obj = hit.gameObject;
            if (obj == gameObject)
                continue;

            if (hit.CompareTag("Player")) {
                obj.GetComponent<PlayerController>().Powerdown(false);
                continue;
            }

            if (obj.TryGetComponent(out KillableEntity en)) {
                en.SpecialKill(transform.position.x < obj.transform.position.x, false, 0);
                continue;
            }
        }

        Vector3Int tileLocation = Utils.WorldToTilemapPosition(body.position);
        Tilemap tm = GameManager.Instance.tilemap;
        for (int x = -explosionTileSize; x <= explosionTileSize; x++) {
            for (int y = -explosionTileSize; y <= explosionTileSize; y++) {
                if (Mathf.Abs(x) + Mathf.Abs(y) > explosionTileSize) continue;
                Vector3Int ourLocation = tileLocation + new Vector3Int(x, y, 0);
                Utils.WrapTileLocation(ref ourLocation);

                TileBase tile = tm.GetTile(ourLocation);
                if (tile is InteractableTile iTile) {
                    iTile.Interact(this, InteractableTile.InteractionDirection.Up, Utils.TilemapToWorldPosition(ourLocation));
                }
            }
        }
        Runner.Despawn(Object, true);
    }

    public override void Kill() {
        Light();
    }

    public void Light() {
        animator.SetTrigger("lit");
        DetonationTimer = TickTimer.CreateFromSeconds(Runner, detonationTime);
        body.velocity = Vector2.zero;
        PlaySound(Enums.Sounds.Enemy_Bobomb_Fuse);
    }

    public override void Kick(bool fromLeft, float speed, bool groundpound) {
        FacingRight = fromLeft;
        body.velocity = new(kickSpeed * (FacingRight ? 1 : -1), 3f);
        PlaySound(Enums.Sounds.Enemy_Shell_Kick);
    }

    public void Turnaround(bool hitWallOnLeft) {
        FacingRight = hitWallOnLeft;
        body.velocity = new((Lit ? -previousFrameVelocity.x : walkSpeed) * (FacingRight ? 1 : -1), body.velocity.y);
        animator.SetTrigger("turnaround");
    }
    #endregion
}
