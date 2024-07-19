using NSMB.Extensions;
using Quantum;
using UnityEngine;

public class PowerupAnimator : QuantumCallbacks {

    //---Serialized
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Animator childAnimator;
    [SerializeField] private Animation childAnimation;
    [SerializeField] private float blinkingRate = 4, scaleRate = 0.1333f, scaleSize = 0.3f;

    //---Private
    private int originalSortingOrder;
    private bool inSpawnAnimation;
    private MaterialPropertyBlock mpb;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref childAnimator, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref childAnimation, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventPowerupBecameActive>(this, OnPowerupBecameActive);
        originalSortingOrder = sRenderer.sortingOrder;
    }

    public void Initialize(QuantumGame game) {
        var powerup = game.Frames.Predicted.Get<Powerup>(entity.EntityRef);

        if (powerup.ParentMarioPlayer.IsValid) {
            // Following mario
            sRenderer.sortingOrder = 15;
            if (childAnimator) {
                childAnimator.enabled = false;
            }
            sRenderer.GetPropertyBlock(mpb = new());

        } else if (powerup.BlockSpawn) {
            // Block spawn
            sRenderer.sortingOrder = -1000;
            if (childAnimation) {
                childAnimation.Play();
            }
        } else if (powerup.LaunchSpawn) {
            // Spawn with velocity
            sRenderer.sortingOrder = -1000;
        } else {
            // Spawned by any other means (blue koopa, usually.)
            sRenderer.sortingOrder = originalSortingOrder;
        }
    }

    public override void OnUpdateView(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        var powerup = f.Get<Powerup>(entity.EntityRef);
        var physicsObject = f.Get<PhysicsObject>(entity.EntityRef);

        if (childAnimator) {
            childAnimator.SetBool("onGround", physicsObject.IsTouchingGround);
        }

        HandleSpawningAnimation(f, powerup);
        HandleDespawningBlinking(powerup);
    }

    private void HandleSpawningAnimation(Frame f, Powerup powerup) {
        if (powerup.ParentMarioPlayer.IsValid && powerup.SpawnAnimationFrames > 0) {
            float timeRemaining = powerup.SpawnAnimationFrames / 60f;
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

            mpb.SetFloat("WaveEnabled", 1);
            sRenderer.SetPropertyBlock(mpb);
        }
    }

    private void HandleDespawningBlinking(Powerup powerup) {
        if (powerup.Lifetime <= 60) {
            sRenderer.enabled = ((powerup.Lifetime / 60f * blinkingRate) % 1) > 0.5f;
        }
    }

    private void OnPowerupBecameActive(EventPowerupBecameActive e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        sRenderer.sortingOrder = originalSortingOrder;
        sRenderer.gameObject.transform.localScale = Vector3.one;
        if (childAnimator) {
            childAnimator.enabled = true;
        }
    }
}