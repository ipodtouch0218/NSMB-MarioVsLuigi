using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class PhysicsEntity : NetworkBehaviour {

    //---Staic Variables
    private static readonly ContactPoint2D[] ContactBuffer = new ContactPoint2D[32];

    //---Networked Variables
    [Networked] public ref PhysicsDataStruct Data => ref MakeRef<PhysicsDataStruct>();
    [Networked] public Vector3 PreviousTickVelocity { get; set; }

    //---Public Variables
    public Collider2D currentCollider;

    //---Serialized Variables
    [SerializeField] private bool goUpSlopes, getCrushedByGroundEntities = true;
    [SerializeField] private float floorAndRoofCutoff = 0.5f;

    public PhysicsDataStruct UpdateCollisions() {
        int hitRightCount = 0, hitLeftCount = 0;
        float previousHeightY = float.MaxValue;

        bool previousOnGround = Data.OnGround;

        Data.CrushableGround = false;
        Data.OnGround = false;
        Data.HitRoof = false;
        Data.FloorAngle = 0;

        if (!currentCollider)
            return Data;

        int c = currentCollider.GetContacts(ContactBuffer);
        for (int i = 0; i < c; i++) {
            ContactPoint2D point = ContactBuffer[i];
            GameObject obj = point.collider.gameObject;
            if (obj == gameObject)
                continue;

            // Has to at least collide with the AnyGround layer...
            if (!Layers.MaskAnyGround.ContainsLayer(obj.layer))
                continue;

            if (Vector2.Dot(point.normal, Vector2.up) > floorAndRoofCutoff) {
                // Touching floor
                // If we're moving upwards, don't touch the floor.
                // Most likely, we're inside a semisolid.
                if (!previousOnGround && PreviousTickVelocity.y > 0.1f) {
                    continue;
                }

                // Make sure that we're also above the floor, so we don't
                // get crushed when inside a semisolid.
                if (point.point.y > currentCollider.bounds.min.y + 0.1f) {
                    continue;
                }

                Data.OnGround = true;
                Data.CrushableGround |= !obj.CompareTag("platform");
                Data.FloorAngle = Vector2.SignedAngle(Vector2.up, point.normal);

            } else if (Layers.MaskSolidGround.ContainsLayer(obj.layer)) {
                if (Vector2.Dot(point.normal, Vector2.down) > floorAndRoofCutoff) {
                    if (getCrushedByGroundEntities || obj.layer != Layers.LayerGroundEntity) {
                        // Touching roof
                        Data.HitRoof = true;
                    }
                } else {
                    // Touching a wall
                    if (Mathf.Abs(previousHeightY - point.point.y) < 0.2f)
                        continue;

                    previousHeightY = point.point.y;

                    if (point.normal.x < 0) {
                        hitRightCount++;
                    } else {
                        hitLeftCount++;
                    }
                }
            }
        }
        Data.HitRight = hitRightCount >= 1;
        Data.HitLeft = hitLeftCount >= 1;

        return Data;
    }
}
