using System.Collections;
using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class StarBouncer : CollectableEntity {

    private static LayerMask AnyGroundMask;

    //---Networked Variables
    [Networked] public NetworkBool IsStationary { get; set; }
    [Networked] public NetworkBool DroppedByPit { get; set; }
    [Networked] public NetworkBool Collectable { get; set; }
    [Networked] public NetworkBool Fast { get; set; }
    [Networked] public TickTimer DespawnTimer { get; set; }

    //---Serialized Variables
    [SerializeField] private float pulseAmount = 0.2f, pulseSpeed = 0.2f, moveSpeed = 3f, rotationSpeed = 30f, bounceAmount = 4f, deathBoostAmount = 20f, blinkingSpeed = 0.5f, lifespan = 15f;

    public bool passthrough = true, fast = false;

    //---Components
    private SpriteRenderer sRenderer;
    private Transform graphicTransform;
    private PhysicsEntity physics;
    private BoxCollider2D worldCollider;
    private Animator animator;

    //--Private Variables
    private float pulseEffectCounter;
    private TrackIcon icon;

    public override void Awake() {
        base.Awake();
        physics = GetComponent<PhysicsEntity>();
        sRenderer = GetComponentInChildren<SpriteRenderer>();
        worldCollider = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
    }

    public static void OnCollectedChanged(Changed<StarBouncer> changed) {
        StarBouncer star = changed.Behaviour;
        if (star.Collector)
            star.Runner.Despawn(star.Object);
    }

    public void OnBeforeSpawned(byte direction, bool stationary, bool pit) {
        FacingRight = direction >= 2;
        Fast = direction == 0 || direction == 3;
        IsStationary = stationary;
        Collectable = stationary;
        DroppedByPit = pit;

        if (!stationary)
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, lifespan);
    }

    public override void Spawned() {
        base.Spawned();

        graphicTransform = transform.Find("Graphic");
        icon = UIUpdater.Instance.CreateTrackIcon(this);

        if (IsStationary) {
            //main star
            animator.enabled = true;
            body.isKinematic = true;
            body.velocity = Vector2.zero;
            StartCoroutine(PulseEffect());
        } else {
            //player dropped star

            passthrough = true;
            sRenderer.color = new(1, 1, 1, 0.55f);
            gameObject.layer = Layers.LayerHitsNothing;
            body.velocity = new(moveSpeed * (FacingRight ? 1 : -1) * (fast ? 2f : 1f), deathBoostAmount);

            //death via pit boost
            if (DroppedByPit)
                body.velocity += Vector2.up * 3;

            body.isKinematic = false;
            worldCollider.enabled = true;
        }

        if (GameManager.Instance.IsMusicEnabled)
            GameManager.Instance.sfx.PlayOneShot(Enums.Sounds.World_Star_Spawn);

        if (AnyGroundMask == default)
            AnyGroundMask = 1 << Layers.LayerGround | 1 << Layers.LayerPassthrough;
    }

    public override void Render() {
        if (IsStationary || (GameManager.Instance?.gameover ?? false))
            return;

        graphicTransform.Rotate(new(0, 0, rotationSpeed * 30 * (FacingRight ? -1 : 1) * Time.deltaTime), Space.Self);

        float timeRemaining = DespawnTimer.RemainingTime(Runner) ?? 0;
        sRenderer.enabled = !(timeRemaining < 5 && timeRemaining * 2 % (blinkingSpeed * 2) < blinkingSpeed);
    }

    public override void FixedUpdateNetwork() {
        if (GameManager.Instance?.gameover ?? false) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;
        }

        if (DespawnTimer.Expired(Runner)) {
            Runner.Despawn(Object, true);
            return;
        }

        if (IsStationary)
            return;

        body.velocity = new(moveSpeed * (FacingRight ? 1 : -1) * (fast ? 2f : 1f), body.velocity.y);

        Collectable |= body.velocity.y < 0;

        if (HandleCollision())
            return;

        if (passthrough && Collectable && body.velocity.y <= 0 && !Utils.IsAnyTileSolidBetweenWorldBox(body.position + worldCollider.offset, worldCollider.size * transform.lossyScale) && !Physics2D.OverlapBox(body.position, Vector2.one / 3, 0, AnyGroundMask)) {
            passthrough = false;
            gameObject.layer = Layers.LayerEntity;
            sRenderer.color = Color.white;
        }
        if (!passthrough) {
            if (Utils.IsAnyTileSolidBetweenWorldBox(body.position + worldCollider.offset, worldCollider.size * transform.lossyScale)) {
                gameObject.layer = Layers.LayerHitsNothing;
            } else {
                gameObject.layer = Layers.LayerEntity;
            }
        }

        if (!passthrough && body.position.y < GameManager.Instance.LevelMinY)
            Runner.Despawn(Object, true);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (!GameManager.Instance.gameover && !Collector)
            GameManager.Instance.particleManager.Play(Enums.Particle.Generic_Puff, transform.position);

        if (icon)
            Destroy(icon.gameObject);
    }

    private IEnumerator PulseEffect() {
        while (true) {
            pulseEffectCounter += Time.deltaTime;
            float sin = Mathf.Sin(pulseEffectCounter * pulseSpeed) * pulseAmount;
            graphicTransform.localScale = Vector3.one * 3f + new Vector3(sin, sin, 0);

            yield return null;
        }
    }

    private bool HandleCollision() {
        physics.UpdateCollisions();

        if (physics.HitLeft || physics.HitRight) {
            FacingRight = physics.HitLeft;
            body.velocity = new(moveSpeed * (FacingRight ? 1 : -1), body.velocity.y);
        }

        if (physics.OnGround && Collectable) {
            body.velocity = new(body.velocity.x, bounceAmount);
            if (physics.HitRoof) {
                Runner.Despawn(Object, true);
                return true;
            }
        }

        return false;
    }

    public void DisableAnimator() {
        animator.enabled = false;
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {
        if (player.IsDead)
            return;

        if (!Collectable || Collector)
            return;

        Collector = player;

        //we can collect
        player.Stars = (byte) Mathf.Min(player.Stars + 1, SessionData.Instance.StarRequirement);

        //game mechanics
        if (IsStationary && GameManager.Instance.Object.HasStateAuthority)
            GameManager.Instance.rpcs.Rpc_ResetTilemap();

        GameManager.Instance.CheckForWinner();

        //despawn
        DespawnTimer = TickTimer.CreateFromTicks(Runner, 1);
    }

    //---CollectableEntity overrides
    public override void OnCollectedChanged() {
        if (Collector) {
            //play fx
            graphicTransform.gameObject.SetActive(false);
            bool sameTeam = Collector.data.Team == Runner.GetLocalPlayerData().Team;
            Collector.PlaySoundEverywhere(sameTeam ? Enums.Sounds.World_Star_Collect_Self : Enums.Sounds.World_Star_Collect_Enemy);
            Instantiate(PrefabList.Instance.Particle_StarCollect, transform.position, Quaternion.identity);
        } else {
            //oops...
            graphicTransform.gameObject.SetActive(true);
        }
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
        //do nothing when bumped
    }

    //---BasicEntity overrides
    public override void Destroy(DestroyCause cause) {
        if (cause != DestroyCause.Lava)
            base.Destroy(cause);
    }
}
