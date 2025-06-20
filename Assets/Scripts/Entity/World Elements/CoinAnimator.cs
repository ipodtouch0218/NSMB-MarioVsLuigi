using NSMB.Extensions;
using NSMB.UI.Game;
using Quantum;
using Quantum.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public unsafe class CoinAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private LegacyAnimateSpriteRenderer defaultCoinAnimate, dottedCoinAnimate;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private ParticleSystem sparkles;
    [SerializeField] private bool looseCoin, objectiveCoin;
    [SerializeField] private float spinSpeed = 120, minimumSpinSpeed = 300;

    //---Private Variables
    private float smoothDampVelocity;
    private bool alreadyBounced;

    public void OnValidate() {
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer);
        this.SetIfNull(ref sparkles, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventCoinChangedType>(this, OnCoinChangedType, onlyIfEntityViewBound: true);
        QuantumEvent.Subscribe<EventCoinChangeCollected>(this, OnCoinChangedCollected, onlyIfEntityViewBound: true);
        QuantumEvent.Subscribe<EventCoinBounced>(this, OnCoinBounced, NetworkHandler.FilterOutReplayFastForward, onlyIfEntityViewBound: true);
        RenderPipelineManager.beginCameraRendering += URPOnPreRender;
    }

    public override void OnActivate(Frame f) {
        var coin = f.Unsafe.GetPointer<Coin>(EntityRef);

        bool dotted = coin->IsCurrentlyDotted;
        defaultCoinAnimate.isDisplaying = !dotted;
        dottedCoinAnimate.isDisplaying = dotted;
        sRenderer.enabled = true;
        alreadyBounced = false;

        if (looseCoin) {
            defaultCoinAnimate.frame = Random.Range(0, defaultCoinAnimate.frames.Length);
            dottedCoinAnimate.frame = Random.Range(0, dottedCoinAnimate.frames.Length);
        }
    }

    public override void OnDeactivate() {
        sRenderer.enabled = false;

        if (looseCoin) {
            ParticleSystem newSparkles = Instantiate(sparkles, sRenderer.transform.position, Quaternion.identity);
            newSparkles.gameObject.SetActive(true);
            newSparkles.Play();
            Destroy(newSparkles.gameObject, 0.5f);
        }
    }

    public void OnDestroy() {
        RenderPipelineManager.beginCameraRendering -= URPOnPreRender;
    }

    public override void OnUpdateView() {
        using var profilerScope = HostProfiler.Start("CoinAnimator.OnUpdateView");
        Frame f = PredictedFrame;
        if (!f.Exists(EntityRef)) {
            return;
        }

        var coin = f.Unsafe.GetPointer<Coin>(EntityRef);
        if (coin->CoinType.HasFlag(CoinType.BakedInStage)) {
            // Bodge: OnCoinChangedCollected doesnt work when collecting a coin at the exact same time as a level reset 
            sRenderer.enabled = !coin->IsCollected;
        } else {
            float despawnTimeRemaining = coin->Lifetime / 60f;
            sRenderer.enabled = !(despawnTimeRemaining < 3 && despawnTimeRemaining % 0.3f >= 0.15f);

            if (objectiveCoin && f.Unsafe.TryGetPointer(EntityRef, out PhysicsObject* physicsObject)) {
                float xSpeed = physicsObject->Velocity.X.AsFloat;
                if (physicsObject->IsTouchingGround) {
                    sRenderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.SmoothDampAngle(sRenderer.transform.eulerAngles.z, 0, ref smoothDampVelocity, 0.2f));
                } else {
                    sRenderer.transform.rotation *= Quaternion.Euler(0, 0, Mathf.Max(xSpeed * -spinSpeed, Mathf.Sign(xSpeed) * -minimumSpinSpeed) * Time.deltaTime);
                }
            }
        }
    }

    private unsafe void URPOnPreRender(ScriptableRenderContext context, Camera camera) {
        if (PredictedFrame == null || !PredictedFrame.Unsafe.TryGetPointer(EntityRef, out Coin* coin)) {
            return;
        }

        Color newColor = sRenderer.color;
        if (!coin->CoinType.HasFlag(CoinType.Objective) || coin->UncollectableByTeam == 0) {
            newColor.a = 1;
        } else {
            newColor.a = IsSameTeamAsCamera(coin->UncollectableByTeam - 1, camera) ? 0.5f : 1f;
        }
        sRenderer.color = newColor;
    }

    private bool IsSameTeamAsCamera(int team, Camera camera) {
        Frame f = PredictedFrame;
        foreach (var playerElement in PlayerElements.AllPlayerElements) {
            if (camera == playerElement.Camera || camera == playerElement.ScrollCamera || camera == playerElement.UICamera) {
                if (!f.Unsafe.TryGetPointer(playerElement.Entity, out MarioPlayer* mario)) {
                    return false;
                }

                return (mario->GetTeam(f) ?? -1) == team;
            }
        }
        return false;
    }

    private void OnCoinBounced(EventCoinBounced e) {
        if (e.Entity != EntityRef) {
            return;
        }

        if (alreadyBounced) {
            return;
        }

        var coin = PredictedFrame.Unsafe.GetPointer<Coin>(EntityRef);
        sfx.pitch = coin->CoinType.HasFlag(CoinType.Objective) ? Random.Range(1.35f, 1.45f) : 1f;
        sfx.volume = coin->CoinType.HasFlag(CoinType.Objective) ? 0.1f : 1f;
        sfx.PlayOneShot(SoundEffect.World_Coin_Drop);
        alreadyBounced = true;
    }

    private void OnCoinChangedCollected(EventCoinChangeCollected e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sRenderer.enabled = !e.Collected;
        if (e.Collected && !NetworkHandler.IsReplayFastForwarding) {
            if (looseCoin) {
                sparkles.transform.SetParent(transform.parent);
                sparkles.gameObject.SetActive(true);
                sparkles.transform.position = sRenderer.transform.position;
            }

            sparkles.Play();
        }
    }

    private void OnCoinChangedType(EventCoinChangedType e) {
        if (e.Entity != EntityRef) {
            return;
        }

        bool dotted = e.Coin.IsCurrentlyDotted;
        defaultCoinAnimate.isDisplaying = !dotted;
        dottedCoinAnimate.isDisplaying = dotted;

        if (!dotted && !NetworkHandler.IsReplayFastForwarding) {
            sfx.PlayOneShot(SoundEffect.World_Coin_Dotted_Spawn);
        }
    }
}