using UnityEngine;

using Fusion;
using NSMB.Entities.Enemies;
using NSMB.Game;
using NSMB.Utils;
using System.Linq;

namespace NSMB.Entities.World {

    public class BulletBillLauncher : NetworkBehaviour {

        //---Networked Variables
        [Networked] TickTimer ShootTimer { get; set; }

        //---Serialized Variables
        [SerializeField] private float playerSearchRadius = 7, playerCloseCutoff = 1, initialShootTimer = 5;
        [SerializeField] private BulletBill[] bulletBills;

        //---Private Variables
        private Vector2 searchBox, closeSearchPosition, closeSearchBox = new(1.5f, 1f);

        private Vector2 leftSearchPosition, rightSearchPosition;
        private Vector2 leftSpawnPosition, rightSpawnPosition;

        public void Awake() {
            searchBox = new(playerSearchRadius, playerSearchRadius);
            closeSearchPosition = transform.position + new Vector3(0, 0.25f);

            Vector2 searchOffset = new(playerSearchRadius * 0.5f + playerCloseCutoff, 0);
            leftSearchPosition = (Vector2) transform.position - searchOffset;
            rightSearchPosition = (Vector2) transform.position + searchOffset;

            leftSpawnPosition = (Vector2) transform.position + new Vector2(-0.25f, -0.2f);
            rightSpawnPosition = (Vector2) transform.position + new Vector2(0.25f, -0.2f);
        }

        public override void Spawned() {
            ShootTimer = TickTimer.CreateFromSeconds(Runner, initialShootTimer);
        }

        public override void FixedUpdateNetwork() {
            if (GameManager.Instance.GameEnded) {
                return;
            }

            if (ShootTimer.Expired(Runner)) {
                TryToShoot();
                ShootTimer = TickTimer.CreateFromSeconds(Runner, initialShootTimer);
            }
        }

        private void TryToShoot() {
            if (!Utils.Utils.IsTileSolidAtWorldLocation(transform.position)) {
                return;
            }

            BulletBill bill = FindInactiveBill();
            if (!bill) {
                return;
            }

            // Check for close players
            if (IntersectsPlayer(closeSearchPosition, closeSearchBox)) {
                return;
            }

            // Shoot left
            if (IntersectsPlayer(leftSearchPosition, searchBox)) {
                SpawnBill(bill, leftSpawnPosition, false);
                return;
            }

            // Shoot right
            if (IntersectsPlayer(rightSearchPosition, searchBox)) {
                SpawnBill(bill, rightSpawnPosition, true);
                return;
            }
        }

        private void SpawnBill(BulletBill bill, Vector2 spawnpoint, bool facingRight) {
            if (!bill) {
                return;
            }

            bill.RespawnEntity();
            bill.FacingRight = facingRight;
            bill.body.Position = spawnpoint;
        }

        private bool IntersectsPlayer(Vector2 origin, Vector2 searchBox) {
            return Runner.GetPhysicsScene2D().OverlapBox(origin, searchBox, 0, Layers.MaskOnlyPlayers);
        }

        private BulletBill FindInactiveBill() {
            // Pick out a Bullet Bill that we didnt fire yet.
            return bulletBills.FirstOrDefault(b => !b.IsActive);
        }

#if UNITY_EDITOR
        public void OnDrawGizmosSelected() {
            Gizmos.color = new(1, 0, 0, 0.5f);
            Gizmos.DrawCube(closeSearchPosition, closeSearchBox);
            Gizmos.color = new(0, 0, 1, 0.5f);
            Gizmos.DrawCube(leftSearchPosition, searchBox);
            Gizmos.DrawCube(rightSearchPosition, searchBox);
        }
#endif
    }
}
