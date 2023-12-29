using UnityEngine;

using Fusion;

namespace NSMB.Entities.World {

    public class SpinnerAnimator : NetworkBehaviour, IBeforeTick {

        //---Networked Variables
        [Networked] public float ArmPosition { get; set; }
        [Networked] public NetworkBool HasPlayer { get; set; }

        //---Serialized Variables
        [SerializeField] private Transform topArmBone;
        [SerializeField] private BoxCollider2D hitbox;
        [SerializeField] private float idleSpinSpeed = -100, fastSpinSpeed = -1800, pressSpeed = 0.5f;

        //---Public Variables
        public float spinSpeed;

        //---Private Variables
        private PropertyReader<float> armPositionPropertyReader;

        public override void Spawned() {
            armPositionPropertyReader = GetPropertyReader<float>(nameof(ArmPosition));
            Runner.SetIsSimulated(Object, true);
        }

        public void BeforeTick() {
            HasPlayer = false;
            hitbox.transform.localPosition = new(0, ArmPosition * -0.084f, 0);
        }

        public override void Render() {
            float armRenderPosition;
            if (TryGetSnapshotsBuffers(out var from, out var to, out float alpha)) {
                (float fromPosition, float toPosition) = armPositionPropertyReader.Read(from, to);
                armRenderPosition = Mathf.Lerp(fromPosition, toPosition, alpha);
            } else {
                armRenderPosition = ArmPosition;
            }

            spinSpeed = Mathf.MoveTowards(spinSpeed, HasPlayer ? fastSpinSpeed : idleSpinSpeed, Time.deltaTime * 1350f);
            topArmBone.eulerAngles += spinSpeed * Time.deltaTime * Vector3.up;
            topArmBone.localPosition = new(0, armRenderPosition * -0.084f, 0);
        }

        public override void FixedUpdateNetwork() {
            ArmPosition = Mathf.MoveTowards(ArmPosition, HasPlayer ? 1 : 0, pressSpeed * Runner.DeltaTime);
            hitbox.transform.localPosition = new(0, ArmPosition * -0.084f, 0);
        }
    }
}
