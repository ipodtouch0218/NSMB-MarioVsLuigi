using NSMB.Extensions;
using Quantum;
using UnityEngine;

public unsafe class GoombaAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Sprite deadSprite;
    [SerializeField] private GameObject specialKillParticle;
    [SerializeField] private LegacyAnimateSpriteRenderer legacyAnimation;
    [SerializeField] private AudioSource sfx;

    public void OnValidate() {
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref legacyAnimation, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref sfx);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, NetworkHandler.FilterOutReplayFastForward);
    }

    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }

        if (f.Global->GameState >= GameState.Ended) {
            legacyAnimation.enabled = false;
            return;
        }

        var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
        var goomba = f.Unsafe.GetPointer<Goomba>(EntityRef);
        var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);

        sRenderer.enabled = enemy->IsActive;
        legacyAnimation.enabled = enemy->IsAlive && !freezable->IsFrozen(f);
        sRenderer.flipX = enemy->FacingRight;

        if (enemy->IsDead) {
            if (goomba->DeathAnimationFrames > 0) {
                // Stomped
                sRenderer.sprite = deadSprite;
            } else {
                // Special killed
                transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy->FacingRight ? -1 : 1) * Time.deltaTime);
            }
        } else {
            transform.rotation = Quaternion.identity;
        }
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
    }

    private void OnEnemyKilled(EventEnemyKilled e) {
        if (e.Enemy != EntityRef) {
            return;
        }

        if (e.IsSpecialKill) {
            if (e.Frame.Unsafe.TryGetPointer(e.Killer, out MarioPlayer* mario) && mario->IsGroundpoundActive) {
                Instantiate(specialKillParticle, transform.position + Vector3.up * 0.2f, Quaternion.identity);
            }
        } else {
            // Play death sound effect
            // sfx.PlayOneShot(SoundEffect.Enemy_Generic_Stomp);
        }
    }
}
