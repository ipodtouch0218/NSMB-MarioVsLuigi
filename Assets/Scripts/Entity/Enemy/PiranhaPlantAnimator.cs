using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.Enemies {
    public unsafe class PiranhaPlantAnimator : QuantumEntityViewComponent {

        //---Static Variables
        private static readonly int ParamActive = Animator.StringToHash("active");
        private static readonly int ParamChomping = Animator.StringToHash("chomping");

        //---Serialized Variables
        [SerializeField] private AudioSource sfx;
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private Animator animator;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, FilterOutReplayFastForward);
        }

        public override void OnUpdateView() {
            Frame f = PredictedFrame;

            if (!f.Exists(EntityRef)) {
                return;
            }

            if (f.Global->GameState == GameState.Ended) {
                animator.speed = 0;
                return;
            }

            var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);
            animator.speed = freezable->IsFrozen(f) ? 0 : 1;

            var piranhaPlant = f.Unsafe.GetPointer<PiranhaPlant>(EntityRef);
            animator.SetBool(ParamActive, piranhaPlant->ChompFrames > 0);
            animator.SetBool(ParamChomping, piranhaPlant->PopupAnimationTime == 1);
            sRenderer.enabled = piranhaPlant->PopupAnimationTime != 0;
        }

        public void PlayChompSound() {
            if (!IsReplayFastForwarding) {
                sfx.PlayOneShot(SoundEffect.Enemy_PiranhaPlant_Chomp);
            }
        }

        private void OnEnemyKilled(EventEnemyKilled e) {
            if (e.Enemy != EntityRef) {
                return;
            }

            sfx.PlayOneShot(SoundEffect.Enemy_PiranhaPlant_Death);
            sfx.PlayOneShot(SoundEffect.Enemy_Shell_Kick);

            Instantiate(Enums.PrefabParticle.Enemy_KillPoof.GetGameObject(), e.EnemyCenter.ToUnityVector3(), Quaternion.identity);
        }
    }
}
