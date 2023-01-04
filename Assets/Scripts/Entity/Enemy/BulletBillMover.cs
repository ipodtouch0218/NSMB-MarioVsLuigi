using UnityEngine;

using NSMB.Utils;

public class BulletBillMover : KillableEntity {

    //---Serialized Variables
    [SerializeField] private float speed, playerSearchRadius = 4, despawnDistance = 8;
    [SerializeField] private ParticleSystem shootParticles, trailParticles;

    //---Misc Variables
    private Vector2 searchVector;
    private new Animation animation;

    public override void Awake() {
        base.Awake();
        animation = GetComponentInChildren<Animation>();

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
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animation.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (IsFrozen)
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
        if (IsFrozen || player.IsFrozen || (player.State == Enums.PowerupState.BlueShell && player.IsCrouching))
            return;

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f;

        if (player.IsStarmanInvincible || player.IsInShell || player.IsSliding
            || ((player.IsGroundpounding || player.IsDrilling) && player.State != Enums.PowerupState.MiniMushroom && attackedFromAbove)
            || player.State == Enums.PowerupState.MegaMushroom) {

            if (player.IsDrilling) {
                player.DoEntityBounce = true;
                player.IsDrilling = false;
            }
            Kill();
            return;
        }
        if (attackedFromAbove) {
            if (!(player.State == Enums.PowerupState.MiniMushroom && !player.IsGroundpounding))
                Kill();

            PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
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
        SpecialKill(FacingRight, false, 0);
    }

    public override void SpecialKill(bool right, bool groundpound, int combo) {
        IsDead = true;
        body.velocity = Vector2.right * 2.5f;
        body.constraints = RigidbodyConstraints2D.None;
        body.angularVelocity = 400f * (right ? 1 : -1);
        body.gravityScale = 1.5f;
        body.isKinematic = false;
        animation.enabled = false;
        hitbox.enabled = false;
        gameObject.layer = Layers.LayerHitsNothing;

        if (groundpound)
            Instantiate(Resources.Load("Prefabs/Particle/EnemySpecialKill"), body.position + Vector2.right * 0.5f, Quaternion.identity);

        PlaySound(Enums.Sounds.Enemy_Shell_Kick);
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
