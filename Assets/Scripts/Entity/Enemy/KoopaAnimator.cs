using NSMB.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Extensions.UnityExtensions;

public class KoopaAnimator : MonoBehaviour {

    //---Static Variables
    private static readonly int ParamShell = Animator.StringToHash("shell");
    private static readonly int ParamXVel = Animator.StringToHash("xVel");
    private static readonly int ParamDead = Animator.StringToHash("dead");

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Transform rotation;

    [SerializeField] private bool mirrorSprite, dontFlip;

    //---Private Variables
    private float dampVelocity;

    public void OnValidate() {
        this.SetIfNull(ref animator, GetComponentType.Children);
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer, GetComponentType.Children);
    }

    public void Start() {
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        QuantumEvent.Subscribe<EventKoopaEnteredShell>(this, OnKoopaEnteredShell);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound);
        QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled);
        QuantumEvent.Subscribe<EventPlayBumpSound>(this, OnPlayBumpSound);
    }

    private void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Predicted;
        if (!f.Exists(entity.EntityRef)) {
            return;
        }

        var enemy = f.Get<Enemy>(entity.EntityRef);
        var koopa = f.Get<Koopa>(entity.EntityRef);
        var holdable = f.Get<Holdable>(entity.EntityRef);
        var physicsObject = f.Get<PhysicsObject>(entity.EntityRef);
        var freezable = f.Get<Freezable>(entity.EntityRef);

        // Animation
        animator.speed = freezable.IsFrozen(f) ? 0 : 1;
        animator.SetBool(ParamShell, koopa.IsInShell || holdable.Holder.IsValid);
        animator.SetFloat(ParamXVel, (koopa.IsInShell && !koopa.IsKicked) ? 0 : Mathf.Abs(physicsObject.Velocity.X.AsFloat));
        animator.SetBool(ParamDead, enemy.IsDead);

        // "Flip" rotation
        float remainingWakeupTimer = koopa.WakeupFrames / 60f;
        if (enemy.IsDead) {
            transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy.FacingRight ? -1 : 1) * Time.deltaTime);

        } else if (koopa.IsInShell) {
            if (!freezable.IsFrozen(f)) {
                if (koopa.IsFlipped && !dontFlip) {
                    dampVelocity = Mathf.Min(dampVelocity + Time.deltaTime * 3, 1);
                    transform.eulerAngles = new Vector3(
                        rotation.eulerAngles.x,
                        rotation.eulerAngles.y,
                        Mathf.Lerp(rotation.eulerAngles.z, 180f, dampVelocity) + (remainingWakeupTimer < 3 && remainingWakeupTimer > 0 ? (Mathf.Sin(remainingWakeupTimer * 120f) * 15f) : 0));

                } else {
                    dampVelocity = 0;
                    transform.eulerAngles = new Vector3(
                        rotation.eulerAngles.x,
                        rotation.eulerAngles.y,
                        remainingWakeupTimer < 3 && remainingWakeupTimer > 0 ? (Mathf.Sin(remainingWakeupTimer * 120f) * 15f) : 0);
                }
            }
        } else {
            transform.rotation = Quaternion.identity;
        }

        sRenderer.enabled = enemy.IsActive;
        sRenderer.flipX = enemy.FacingRight ^ mirrorSprite;

        Vector3 modifiedZ = transform.position;
        if (f.Exists(holdable.Holder)) {
            modifiedZ.z = -4.1f;
        }
        transform.position = modifiedZ;
    }

    private void OnPlayBumpSound(EventPlayBumpSound e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.World_Block_Bump);
    }

    private void OnEnemyKilled(EventEnemyKilled e) {
        if (e.Enemy != entity.EntityRef) {
            return;
        }
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
    }

    private void OnKoopaEnteredShell(EventKoopaEnteredShell e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        // sfx.PlayOneShot(SoundEffect.Enemy_Generic_Stomp);
    }
}