using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsEntity : MonoBehaviour {

    private static int GROUND_LAYERID = -1;

    public bool onGround, hitRoof, hitRight, hitLeft;
    public float floorAngle = 0;
    public Collider2D currentCollider;
    public float floorAndRoofCutoff = 0.3f;
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[32];

    private void Start() {
        if (GROUND_LAYERID == -1)
            GROUND_LAYERID = LayerMask.NameToLayer("Ground");
    }

    public void UpdateCollisions() {
        int hitRightCount = 0, hitLeftCount = 0;
        float previousHeightY = float.MaxValue;
        onGround = false;
        hitRoof = false;
        floorAngle = 0;
        if (!currentCollider)
            return;
        
        int c = currentCollider.GetContacts(contacts);
        for (int i = 0; i < c; i++) {
            ContactPoint2D point = contacts[i];
            if (Vector2.Dot(point.normal, Vector2.up) > floorAndRoofCutoff) {
                //touching floor
                onGround = true;
                floorAngle = Vector2.SignedAngle(Vector2.up, point.normal);
            } else if (point.collider.gameObject.layer == GROUND_LAYERID) {
                if (Vector2.Dot(point.normal, Vector2.down) > floorAndRoofCutoff) {
                    //touching roof
                    hitRoof = true;
                } else {
                    //touching a wall
                    if (Mathf.Abs(previousHeightY - point.point.y) < 0.2f) {
                        Debug.Log("AAAA");
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
        hitRight = hitRightCount >= 2;
        hitLeft = hitLeftCount >= 2;
    }
}
