using NSMB.Extensions;
using Quantum;
using UnityEngine;

public unsafe class PowerupAnimator : QuantumEntityViewComponent {

    //---Serialized
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Animator childAnimator;
    [SerializeField] private Animation childAnimation;
    [SerializeField] private float blinkingRate = 4, scaleRate = 0.1333f, scaleSize = 0.3f;
    [SerializeField] private AudioSource sfx;

    //---Private
    private int originalSortingOrder;
    private bool inSpawnAnimation;
    private MaterialPropertyBlock mpb;

    public void OnValidate() {
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref childAnimator, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref childAnimation, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref sfx);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventPowerupBecameActive>(this, OnPowerupBecameActive);
    }

    public override void OnActivate(Frame f) {
        originalSortingOrder = sRenderer.sortingOrder;
        sRenderer.GetPropertyBlock(mpb = new());
        var powerup = f.Unsafe.GetPointer<Powerup>(EntityRef);
        var scriptable = QuantumUnityDB.GetGlobalAsset(powerup->Scriptable);

        if (powerup->ParentMarioPlayer.IsValid) {
            // Following mario
            sRenderer.sortingOrder = 15;
            if (childAnimator) {
                childAnimator.enabled = false;
            }

        } else if (powerup->BlockSpawn) {
            // Block spawn
            sRenderer.sortingOrder = -1000;
            if (!NetworkHandler.IsReplayFastForwarding) {
                sfx.PlayOneShot(scriptable.BlockSpawnSoundEffect);
            }
            if (childAnimation) {
                childAnimation.Play();
            }
        } else if (powerup->LaunchSpawn) {
            // Spawn with velocity
            sRenderer.sortingOrder = -1000;
            if (!NetworkHandler.IsReplayFastForwarding) {
                sfx.PlayOneShot(scriptable.BlockSpawnSoundEffect);
            }
        } else {
            // Spawned by any other means (blue koopa, usually.)
            if (!NetworkHandler.IsReplayFastForwarding) {
                sfx.PlayOneShot(scriptable.BlockSpawnSoundEffect);
            }
            if (childAnimation) {
                childAnimation.Play();
            }
            sRenderer.sortingOrder = originalSortingOrder;
        }
    }

    public override void OnUpdateView() {
        Frame f = PredictedFrame;
        if (!f.Exists(EntityRef)) {
            return;
        }

        var powerup = f.Unsafe.GetPointer<Powerup>(EntityRef);
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);

        if (childAnimator) {
            childAnimator.SetBool("onGround", physicsObject->IsTouchingGround);
        }

        HandleSpawningAnimation(f, powerup);
        HandleDespawningBlinking(powerup);
    }

    private void HandleSpawningAnimation(Frame f, Powerup* powerup) {
        if (f.Exists(powerup->ParentMarioPlayer) && powerup->SpawnAnimationFrames > 0) {
            float timeRemaining = powerup->SpawnAnimationFrames / 60f;
            float adjustment = Mathf.PingPong(timeRemaining, scaleRate) / scaleRate * scaleSize;
            sRenderer.transform.localScale = Vector3.one * (1 + adjustment);

            if (!inSpawnAnimation) {
                mpb.SetFloat("WaveEnabled", 0);
                sRenderer.SetPropertyBlock(mpb);
                inSpawnAnimation = true;
            }
        } else if (inSpawnAnimation) {
            sRenderer.transform.localScale = Vector3.one;
            inSpawnAnimation = false;
            sRenderer.sortingOrder = 15;

            mpb.SetFloat("WaveEnabled", 1);
            sRenderer.SetPropertyBlock(mpb);
        }
    }

    private void HandleDespawningBlinking(Powerup* powerup) {
        if (powerup->Lifetime <= 60) {
            sRenderer.enabled = ((powerup->Lifetime / 60f * blinkingRate) % 1) > 0.5f;
        }
    }

    private void OnPowerupBecameActive(EventPowerupBecameActive e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sRenderer.sortingOrder = originalSortingOrder;
        sRenderer.gameObject.transform.localScale = Vector3.one;
        if (childAnimator) {
            childAnimator.enabled = true;
        }
    }
}