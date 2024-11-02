using NSMB.Extensions;
using Quantum;
using UnityEngine;

public unsafe class BulletBillAnimator : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private ParticleSystem trailParticles;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private LegacyAnimateSpriteRenderer legacyAnimation;
    [SerializeField] private GameObject specialKillParticles;

    [SerializeField] private float fireballScaleSize = 0.075f;

    //---Private Variables
    private float fireballScaleTimer;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref legacyAnimation, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventBulletBillHitByProjectile>(this, OnBulletBillHitByProjectile, NetworkHandler.FilterOutReplayFastForward);
    }

    public void Initialize(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        var enemy = f.Unsafe.GetPointer<Enemy>(entity.EntityRef);

        sRenderer.flipX = enemy->FacingRight;
        Vector2 pos = trailParticles.transform.localPosition;
        pos.x = Mathf.Abs(pos.x) * (enemy->FacingRight ? -1 : 1);
        trailParticles.transform.localPosition = pos;
        trailParticles.Play();

        if (!NetworkHandler.IsReplayFastForwarding) {
            sfx.Play();
        }
        legacyAnimation.enabled = true;
    }

    public void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Predicted;

        if (!f.Exists(entity.EntityRef)) {
            return;
        }

        var enemy = f.Unsafe.GetPointer<Enemy>(entity.EntityRef);
        var freezable = f.Unsafe.GetPointer<Freezable>(entity.EntityRef);
        bool frozen = freezable->IsFrozen(f);

        sRenderer.enabled = enemy->IsActive;

        var emission = trailParticles.emission;
        emission.enabled = enemy->IsActive && !frozen;
        legacyAnimation.enabled = !frozen;

        if (enemy->IsDead) {
            transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy->FacingRight ? -1 : 1) * Time.deltaTime);
        } else {
            transform.rotation = Quaternion.identity;
        }

        float scale = 1 + Mathf.Abs(Mathf.Sin(fireballScaleTimer * 10 * Mathf.PI)) * fireballScaleSize;
        transform.localScale = Vector3.one * scale;
        fireballScaleTimer = Mathf.Max(0, fireballScaleTimer - Time.deltaTime);
    }

    private void OnBulletBillHitByProjectile(EventBulletBillHitByProjectile e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        fireballScaleTimer = 0.3f;
    }

    private void OnEnemyKilled(EventEnemyKilled e) {
        if (e.Enemy != entity.EntityRef) {
            return;
        }

        if (e.IsSpecialKill) {
            Instantiate(specialKillParticles, transform.position, Quaternion.identity);
        } else {
            // sfx.PlayOneShot(SoundEffect.Enemy_Generic_Stomp);
        }
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
    }
}
