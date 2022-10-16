using UnityEngine;

using Fusion;
using NSMB.Utils;

public class BulletBillLauncher : NetworkBehaviour {

    //---Networked Variables
    [Networked] TickTimer ShootTimer { get; set; }
    [Networked, Capacity(3)] NetworkArray<BulletBillMover> BulletBills => default;

    //---Serialized Variables
    [SerializeField] private float playerSearchRadius = 7, playerCloseCutoff = 1, initialShootTimer = 5;

    //---Private Variables
    private Vector2 searchBox, closeSearchPosition, closeSearchBox = new(1.5f, 1f);

    private Vector2 leftSearchPosition, rightSearchPosition;
    private Vector2 leftSpawnPosition, rightSpawnPosition;

    public void Awake() {
        searchBox = new(playerSearchRadius, playerSearchRadius);
        closeSearchPosition = transform.position + new Vector3(0, 0.25f);

        Vector2 searchOffset = new(playerSearchRadius / 2 + playerCloseCutoff, 0);
        leftSearchPosition = (Vector2) transform.position - searchOffset;
        rightSearchPosition = (Vector2) transform.position + searchOffset;

        leftSpawnPosition = (Vector2) transform.position + new Vector2(-0.25f, -0.2f);
        rightSpawnPosition = (Vector2) transform.position + new Vector2(0.25f, -0.2f);
    }

    public override void Spawned() {
        ShootTimer = TickTimer.CreateFromSeconds(Runner, initialShootTimer);
    }

    public override void FixedUpdateNetwork() {

        if (ShootTimer.Expired(Runner)) {
            TryToShoot();
            ShootTimer = TickTimer.CreateFromSeconds(Runner, initialShootTimer);
        }
    }

    private void TryToShoot() {
        if (!Utils.IsTileSolidAtWorldLocation(transform.position))
            return;

        byte activeBills = 0;
        foreach (BulletBillMover bill in BulletBills) {
            if (bill)
                activeBills++;
        }
        if (activeBills >= 3)
            return;

        //Check for close players
        if (IntersectsPlayer(closeSearchPosition, closeSearchBox))
            return;

        //Shoot left
        if (IntersectsPlayer(leftSearchPosition, searchBox)) {
            SpawnBill(leftSpawnPosition, false);
            return;
        }

        //Shoot right
        if (IntersectsPlayer(rightSearchPosition, searchBox)) {
            SpawnBill(rightSpawnPosition, true);
            return;
        }
    }

    private void SpawnBill(Vector2 spawnpoint, bool facingRight) {
        NetworkObject obj = Runner.Spawn(PrefabList.Instance.BulletBill, spawnpoint, onBeforeSpawned: (runner, obj) => {
            obj.GetComponent<BulletBillMover>().OnBeforeSpawned(facingRight);
        });
        BulletBillMover bbm = obj.GetComponent<BulletBillMover>();

        for (int i = 0; i < BulletBills.Length; i++) {
            if (!BulletBills[i]) {
                BulletBills.Set(i, bbm);
                break;
            }
        }
    }

    private bool IntersectsPlayer(Vector2 origin, Vector2 searchBox) {
        return Runner.GetPhysicsScene2D().OverlapBox(origin, searchBox, 0, Layers.MaskOnlyPlayers);
    }

    public void OnDrawGizmosSelected() {
        Gizmos.color = new(1, 0, 0, 0.5f);
        Gizmos.DrawCube(closeSearchPosition, closeSearchBox);
        Gizmos.color = new(0, 0, 1, 0.5f);
        Gizmos.DrawCube(leftSearchPosition, searchBox);
        Gizmos.DrawCube(rightSearchPosition, searchBox);
    }
}