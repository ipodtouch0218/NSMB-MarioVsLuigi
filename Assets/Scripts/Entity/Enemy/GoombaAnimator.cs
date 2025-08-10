using NSMB.Utilities;
using NSMB.Utilities.Components;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.Enemies {
    public unsafe class GoombaAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private Sprite[] deadSprite;
        [SerializeField] private GameObject specialKillParticle;
        [SerializeField] private LegacyAnimateSpriteRenderer legacyAnimation;
        [SerializeField] private AudioSource sfx;

        public void OnValidate() {
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref legacyAnimation, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref sfx);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
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
                    sRenderer.sprite = deadSprite[(int)Utils.GetStageTheme()];
                } else {
                    // Special killed
                    switch((int) Utils.GetStageTheme()) {
                    case 0:
                        transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy->FacingRight ? -1 : 1) * Time.deltaTime);
                        break;
                    default:
                        transform.localScale = new Vector3(1, -1, 1);
                        break;
                    }
                }
            } else {
                transform.rotation = Quaternion.identity;
                transform.localScale = Vector3.one;
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

            if (e.KillReason == KillReason.Groundpounded) {
                Instantiate(specialKillParticle, transform.position + Vector3.up * 0.2f, Quaternion.identity);
            }
        }
    }
}
