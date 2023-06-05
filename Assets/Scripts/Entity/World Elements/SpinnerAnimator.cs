using UnityEngine;

using Fusion;
using NSMB.Entities.Player;

namespace NSMB.Entities.World {

    [OrderAfter(typeof(PlayerController))]
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
        private Interpolator<float> armPositionInterpolator;

        public override void Spawned() {
            armPositionInterpolator = GetInterpolator<float>(nameof(ArmPosition));
        }

        public void BeforeTick() {
            HasPlayer = false;
        }

        public override void Render() {
            spinSpeed = Mathf.MoveTowards(spinSpeed, HasPlayer ? fastSpinSpeed : idleSpinSpeed, Time.deltaTime * 1350f);
            topArmBone.eulerAngles += spinSpeed * Time.deltaTime * Vector3.up;
            hitbox.transform.localPosition = topArmBone.localPosition = new Vector3(0, armPositionInterpolator.Value * -0.084f, 0);
        }

        public override void FixedUpdateNetwork() {
            ArmPosition = Mathf.MoveTowards(ArmPosition, HasPlayer ? 1 : 0, pressSpeed * Runner.DeltaTime);
            hitbox.transform.localPosition = topArmBone.localPosition = new Vector3(0, ArmPosition * -0.084f, 0);
        }
    }
}
