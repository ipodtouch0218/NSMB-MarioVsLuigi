using NSMB.Extensions;
using Quantum;
using System.Collections;
using UnityEngine;

public unsafe class BulletBillAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private ParticleSystem trailParticles;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private LegacyAnimateSpriteRenderer legacyAnimation;
    [SerializeField] private GameObject specialKillParticles;

    [SerializeField] private float fireballScaleSize = 0.075f;

    //---Private Variables
    private float fireballScaleTimer;

    public void OnValidate() {
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref legacyAnimation, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, NetworkHandler.FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventBulletBillHitByProjectile>(this, OnBulletBillHitByProjectile, NetworkHandler.FilterOutReplayFastForward);
    }

    public override void OnActivate(Frame f) {
        if (!NetworkHandler.IsReplayFastForwarding) {
            sfx.Play();
        }
        legacyAnimation.enabled = true;
        StartCoroutine(ChangeSpriteSortingOrder());
        trailParticles.Play();
    }

    public override void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }

        var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
        var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);
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

        sRenderer.flipX = enemy->FacingRight;
        Vector2 pos = trailParticles.transform.localPosition;
        pos.x = Mathf.Abs(pos.x) * (enemy->FacingRight ? -1 : 1);
        trailParticles.transform.localPosition = pos;
    }

    private static WaitForSeconds wait = new(0.33f);
    private IEnumerator ChangeSpriteSortingOrder() {
        int originalSortingOrder = sRenderer.sortingOrder;
        sRenderer.sortingOrder = -1001;
        yield return wait;
        sRenderer.sortingOrder = originalSortingOrder;
    }

    private void OnBulletBillHitByProjectile(EventBulletBillHitByProjectile e) {
        if (e.Entity != EntityRef) {
            return;
        }

        fireballScaleTimer = 0.3f;
    }

    private void OnEnemyKilled(EventEnemyKilled e) {
        if (e.Enemy != EntityRef) {
            return;
        }

        if (e.IsSpecialKill) {
            Instantiate(specialKillParticles, transform.position, Quaternion.identity);
        } else {
            // sfx.PlayOneShot(SoundEffect.Enemy_Generic_Stomp);
        }
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
    }
}
