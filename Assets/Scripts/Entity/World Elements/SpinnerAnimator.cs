using UnityEngine;

using Fusion;
using NSMB.Entities.Player;

namespace NSMB.Entities.World {

    [OrderAfter(typeof(PlayerController), typeof(NetworkPhysicsSimulation2D))]
    public class SpinnerAnimator : NetworkBehaviour {

        //---Static Variables
        private static readonly ContactPoint2D[] ContactBuffer = new ContactPoint2D[32];

        //---Networked Variables
        [Networked] public float ArmPosition { get; set; }
        [Networked] private NetworkBool HasPlayer { get; set; }

        //---Serialized Variables
        [SerializeField] private Transform topArmBone;
        [SerializeField] private BoxCollider2D hitbox;
        [SerializeField] private float idleSpinSpeed = -100, fastSpinSpeed = -1800, pressSpeed = 0.5f;

        //---Public Variables
        public float spinSpeed;

        public override void Render() {
            spinSpeed = Mathf.MoveTowards(spinSpeed, HasPlayer ? fastSpinSpeed : idleSpinSpeed, Time.deltaTime * 1350f);

            topArmBone.eulerAngles += spinSpeed * Time.deltaTime * Vector3.up;
        }

        public override void FixedUpdateNetwork() {

            HasPlayer = false;

            //Runner.GetPhysicsScene2D().Simulate(0);
            int count = hitbox.GetContacts(ContactBuffer);
            for (int i = 0; i < count; i++) {
                ContactPoint2D contact = ContactBuffer[i];
                if (contact.normal != Vector2.down)
                    continue;

                if (contact.rigidbody.gameObject.CompareTag("Player")) {
                    HasPlayer = true;
                    break;
                }
            }

            ArmPosition = Mathf.MoveTowards(ArmPosition, HasPlayer ? 1 : 0, pressSpeed * Runner.DeltaTime);
            hitbox.transform.localPosition = topArmBone.localPosition = new Vector3(0, ArmPosition * -0.084f, 0);
        }
    }
}
