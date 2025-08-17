using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities;

namespace NSMB.Entities.World {
    public unsafe class IceBlockAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private AudioSource sfx;
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private SpriteRenderer nitroRenderer;
        [SerializeField] private GameObject[] breakPrefab;
        [SerializeField] private GameObject nitroSmoke, isNitro, isNotNitro;

        [SerializeField] private float shakeSpeed = 120, shakeAmount = 0.03f;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref nitroRenderer, UnityExtensions.GetComponentType.Children);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventIceBlockSinking>(this, OnIceBlockSinking, FilterOutReplayFastForward);
        }

        public override void OnActivate(Frame f) {
            var cube = f.Unsafe.GetPointer<IceBlock>(EntityRef);

            if (!IsReplayFastForwarding) {
                sfx.PlayOneShot(SoundEffect.Enemy_Generic_Freeze);
            }

            sRenderer.size = cube->Size.ToUnityVector2() * 2;
            nitroRenderer.size = cube->Size.ToUnityVector2() * 2;

            Vector3 position = transform.position;
            position.z = -4.25f;
            transform.position = position;
        }

        public override void OnUpdateView() {
            Frame f = PredictedFrame;
            if (!f.Exists(EntityRef)) {
                return;
            }

            var cube = f.Unsafe.GetPointer<IceBlock>(EntityRef);
            if (cube->IsNitro) {
                isNitro.SetActive(true);
                isNotNitro.SetActive(false);
            } else {
                isNitro.SetActive(false);
                isNotNitro.SetActive(true);
            }
            if (Utils.GetStageTheme() != StageTheme.NSMB) {
                nitroSmoke.SetActive(false);
            }

            if (cube->AutoBreakFrames > 0 && cube->AutoBreakFrames < 60
                && cube->TimerEnabled(f, EntityRef)) {

                Vector3 position = transform.position;
                float time = (cube->AutoBreakFrames - Game.InterpolationFactor) / 60f;
                position.x += Mathf.Sin(time * shakeSpeed) * shakeAmount;
                transform.position = position;
            }
        }

        public override void OnDeactivate() {
            if (!IsReplayFastForwarding) {
                Instantiate(breakPrefab[(int)Utils.GetStageTheme()], transform.position, Quaternion.identity);
            }
        }

        private void OnIceBlockSinking(EventIceBlockSinking e) {
            if (e.Entity != EntityRef) {
                return;
            }

            if (e.LiquidType == LiquidType.Lava) {
                sfx.PlayOneShot(SoundEffect.Enemy_Generic_FreezeMelt);
            }
        }
    }
}
