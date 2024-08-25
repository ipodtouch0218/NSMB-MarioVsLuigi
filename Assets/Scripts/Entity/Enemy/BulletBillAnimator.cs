using NSMB.Extensions;
using Quantum;
using UnityEngine;

public class BulletBillAnimator : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private ParticleSystem trailParticles, shootParticles;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private LegacyAnimateSpriteRenderer legacyAnimation;
    [SerializeField] private GameObject specialKillParticles;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref legacyAnimation, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound);
    }

    public void Initialize(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        var enemy = f.Get<Enemy>(entity.EntityRef);

        sRenderer.flipX = enemy.FacingRight;
        Vector2 pos = trailParticles.transform.localPosition;
        pos.x = Mathf.Abs(pos.x) * (enemy.FacingRight ? -1 : 1);
        trailParticles.transform.localPosition = pos;

        ParticleSystem.ShapeModule shape = shootParticles.shape;
        shape.rotation = new Vector3(0, 0, enemy.FacingRight ? -33 : 147);

        shootParticles.Play();
        trailParticles.Play();
        sfx.Play();
        legacyAnimation.enabled = true;
    }

    public void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Predicted;

        if (!f.Exists(entity.EntityRef)) {
            return;
        }

        var enemy = f.Get<Enemy>(entity.EntityRef);
        sRenderer.enabled = enemy.IsActive;

        var emission = trailParticles.emission;
        emission.enabled = enemy.IsActive;

        if (enemy.IsDead) {
            transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy.FacingRight ? -1 : 1) * Time.deltaTime);
        } else {
            transform.rotation = Quaternion.identity;
        }
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
