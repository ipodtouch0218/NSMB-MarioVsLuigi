using UnityEngine;

using NSMB.Utils;
using Fusion;

public class BulletBillMover : KillableEntity {

    //---Serialized Variables
    [SerializeField] private float speed, playerSearchRadius = 4, despawnDistance = 8;
    [SerializeField] private ParticleSystem shootParticles, trailParticles;

    //---Components
    [SerializeField] private Animation spriteAnimation;

    //---Private Variables
    private Vector2 searchVector;

    public override void OnValidate() {
        base.OnValidate();
        if (!spriteAnimation) spriteAnimation = GetComponent<Animation>();
    }

    public void Awake() {
        searchVector = new(playerSearchRadius * 2, 100);
    }

    public void OnBeforeSpawned(bool shootRight) {
        FacingRight = shootRight;
    }

    public override void Spawned() {
        base.Spawned();
        body.velocity = new(speed * (FacingRight ? 1 : -1), body.velocity.y);

        if (FacingRight) {
            trailParticles.transform.localPosition *= new Vector2(-1, 1);
            ParticleSystem.ShapeModule shape = shootParticles.shape;
            shape.rotation = new Vector3(0, 0, -33);
        }

        shootParticles.Play();
        sRenderer.flipX = FacingRight;
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (!Object.IsValid)
            return;

        if (GameManager.Instance && GameManager.Instance.GameEnded) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            spriteAnimation.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (IsFrozen || IsDead)
            return;

        body.velocity = new(speed * (FacingRight ? 1 : -1), body.velocity.y);
        DespawnCheck();
    }

    private void DespawnCheck() {
        foreach (PlayerController player in GameManager.Instance.AlivePlayers) {
            if (!player)
                continue;

            if (Utils.WrappedDistance(player.body.position, body.position) < despawnDistance)
                return;
        }

        Runner.Despawn(Object);
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {
        if (IsDead || IsFrozen || player.IsFrozen || (player.State == Enums.PowerupState.BlueShell && player.IsCrouching))
            return;

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;

        if (player.IsStarmanInvincible || player.IsInShell || player.IsSliding
            || ((player.IsGroundpounding || player.IsDrilling) && player.State != Enums.PowerupState.MiniMushroom && attackedFromAbove)
            || player.State == Enums.PowerupState.MegaMushroom) {

            if (player.IsDrilling) {
                player.DoEntityBounce = true;
                player.IsDrilling = false;
            }
            SpecialKill(false, true, player.StarCombo++);
            return;
        }
        if (attackedFromAbove) {
            if (!(player.State == Enums.PowerupState.MiniMushroom && !player.IsGroundpounding))
                Kill();

            player.IsDrilling = false;
            player.IsGroundpounding = false;
            player.DoEntityBounce = true;
            return;
        }

        player.Powerdown(false);
    }

    //---IFireballInteractable overrides
    public override bool InteractWithFireball(FireballMover fireball) {
        //don't die to fireballs, but still destroy them.
        return true;
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
        //do nothing
    }

    //---KillableEntity overrides
    public override void Kill() {
        IsDead = true;
        body.velocity = Vector2.zero;
        body.constraints = RigidbodyConstraints2D.None;
        body.angularVelocity = 400f * (FacingRight ? -1 : 1);
        body.gravityScale = 1.5f;
        body.isKinematic = false;
    }

    public override void SpecialKill(bool right, bool groundpound, int combo) {
        IsDead = true;
        WasSpecialKilled = true;
        WasGroundpounded = groundpound;
        body.velocity = Vector2.zero;
        DespawnTimer = TickTimer.CreateFromSeconds(Runner, 1f);
    }

    public override void OnIsDeadChanged() {
        base.OnIsDeadChanged();

        if (IsDead) {
            trailParticles.Stop();
            if (WasSpecialKilled) {
                sRenderer.enabled = false;
            }
        } else {
            sRenderer.enabled = true;
            trailParticles.Play();
        }
    }

#if UNITY_EDITOR
    //---Debug
    private static readonly Color RedHalfAlpha = new(1f, 0f, 0f, 0.5f);
    private Vector2? boxOffset;
    public void OnDrawGizmosSelected() {
        if (!GameManager.Instance)
            return;

        boxOffset ??= new Vector2(GameManager.Instance.LevelWidth, 0f);

        Gizmos.color = RedHalfAlpha;
        Gizmos.DrawCube(body.position, searchVector);
        //left border check
        if (body.position.x - playerSearchRadius < GameManager.Instance.LevelMinX)
            Gizmos.DrawCube(body.position + boxOffset.Value, searchVector);
        //right border check
        if (body.position.x + playerSearchRadius > GameManager.Instance.LevelMaxX)
            Gizmos.DrawCube(body.position - boxOffset.Value, searchVector);
    }
#endif
}
