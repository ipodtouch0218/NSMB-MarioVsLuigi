using UnityEngine;

public class PhysicsEntity : MonoBehaviour {

    private static LayerMask GROUND_LAYERMASK;
    
    public bool goUpSlopes;

    public bool onGround, hitRoof, hitRight, hitLeft, crushableGround;
    public float floorAngle = 0;
    public Collider2D currentCollider;
    public float floorAndRoofCutoff = 0.3f;
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[32];

    private void Start() {
        if (GROUND_LAYERMASK == default)
            GROUND_LAYERMASK = LayerMask.GetMask("Ground", "IceBlock");
    }

    public void UpdateCollisions() {
        int hitRightCount = 0, hitLeftCount = 0;
        float previousHeightY = float.MaxValue;
        crushableGround = false;
        onGround = false;
        hitRoof = false;
        floorAngle = 0;
        if (!currentCollider)
            return;
        
        int c = currentCollider.GetContacts(contacts);
        for (int i = 0; i < c; i++) {
            ContactPoint2D point = contacts[i];
            if (point.collider.gameObject == gameObject)
                continue;

            if (Vector2.Dot(point.normal, Vector2.up) > floorAndRoofCutoff) {
                //touching floor
                onGround = true;
                crushableGround |= !point.collider.gameObject.CompareTag("platform");
                floorAngle = Vector2.SignedAngle(Vector2.up, point.normal);
            } else if (GROUND_LAYERMASK == (GROUND_LAYERMASK | (1 << point.collider.gameObject.layer))) {
                if (Vector2.Dot(point.normal, Vector2.down) > floorAndRoofCutoff) {
                    //touching roof
                    hitRoof = true;
                } else {
                    //touching a wall
                    if (Mathf.Abs(previousHeightY - point.point.y) < 0.2f) {
                        continue;
                    }
                    previousHeightY = point.point.y;

                    if (point.normal.x < 0) {
                        hitRightCount++;
                    } else {
                        hitLeftCount++;
                    }
                }
            }
        }
        hitRight = hitRightCount >= 1;
        hitLeft = hitLeftCount >= 1;
    }
}
