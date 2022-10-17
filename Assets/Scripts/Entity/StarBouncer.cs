using System.Collections;
using UnityEngine;

using Fusion;
using NSMB.Utils;

public class StarBouncer : CollectableEntity {

    private static int ANY_GROUND_MASK = -1;

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
    private bool canBounce;

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

        graphicTransform = transform.Find("Graphic");

        GameObject trackObject = Instantiate(UIUpdater.Instance.starTrackTemplate, UIUpdater.Instance.starTrackTemplate.transform.parent);
        TrackIcon icon = trackObject.GetComponent<TrackIcon>();
        icon.target = gameObject;
        trackObject.SetActive(true);

        if (IsStationary) {
            //main star
            animator.enabled = true;
            body.isKinematic = true;
            body.velocity = Vector2.zero;
            StartCoroutine(PulseEffect());

            //play star spawn sfx, only IF
            if (GameManager.Instance.musicEnabled)
                GameManager.Instance.sfx.PlayOneShot(Enums.Sounds.World_Star_Spawn.GetClip());

        } else {
            //player dropped star

            trackObject.transform.localScale = new(3f / 4f, 3f / 4f, 1f);
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

        if (ANY_GROUND_MASK == -1)
            ANY_GROUND_MASK = LayerMask.GetMask("Ground", "PassthroughInvalid");
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

        float timeRemaining = DespawnTimer.RemainingTime(Runner) ?? 0;
        sRenderer.enabled = !(timeRemaining < 5 && timeRemaining * 2 % (blinkingSpeed * 2) < blinkingSpeed);

        body.velocity = new(moveSpeed * (FacingRight ? 1 : -1) * (fast ? 2f : 1f), body.velocity.y);

        canBounce |= body.velocity.y < 0;
        Collectable |= body.velocity.y < 0;

        if (HandleCollision())
            return;

        if (passthrough && Collectable && body.velocity.y <= 0 && !Utils.IsAnyTileSolidBetweenWorldBox(body.position + worldCollider.offset, worldCollider.size * transform.lossyScale) && !Physics2D.OverlapBox(body.position, Vector2.one / 3, 0, ANY_GROUND_MASK)) {
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

        if (!passthrough && body.position.y < GameManager.Instance.GetLevelMinY())
            Runner.Despawn(Object, true);
    }

    public override void Render() {
        if (IsStationary || (GameManager.Instance?.gameover ?? false))
            return;

        graphicTransform.Rotate(new(0, 0, rotationSpeed * 30 * (FacingRight ? -1 : 1) * Time.deltaTime), Space.Self);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (!GameManager.Instance.gameover && !Collector)
            GameManager.Instance.particleManager.Play(Enums.Particle.Generic_Puff, transform.position);
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

        if (physics.hitLeft || physics.hitRight) {
            FacingRight = physics.hitLeft;
            body.velocity = new(moveSpeed * (FacingRight ? 1 : -1), body.velocity.y);
        }

        if (physics.onGround && canBounce) {
            body.velocity = new(body.velocity.x, bounceAmount);
            if (physics.hitRoof) {
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
        player.Stars = (byte) Mathf.Min(player.Stars + 1, LobbyData.Instance.StarRequirement);

        //game mechanics
        if (IsStationary)
            GameManager.Instance.Rpc_ResetTilemap();
        GameManager.Instance.CheckForWinner();

        //despawn
        DespawnTimer = TickTimer.CreateFromTicks(Runner, 1);
    }

    //---CollectableEntity overrides
    public override void OnCollectedChanged() {
        //play fx
        graphicTransform.gameObject.SetActive(false);
        Collector.PlaySoundEverywhere(Collector.Object.HasInputAuthority ? Enums.Sounds.World_Star_Collect_Self : Enums.Sounds.World_Star_Collect_Enemy);
        Instantiate(PrefabList.Instance.Particle_StarCollect, transform.position, Quaternion.identity);
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
        //do nothing when bumped
    }
}
