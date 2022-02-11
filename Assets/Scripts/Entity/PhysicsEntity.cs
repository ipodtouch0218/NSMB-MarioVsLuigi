using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsEntity : MonoBehaviour {

    private static int GROUND_LAYERID = -1;

    public bool onGround, hitRoof, hitRight, hitLeft; 
    public Collider2D currentCollider;
    public float floorAndRoofCutoff = 0.3f;
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[32];

    private void Start() {
        if (GROUND_LAYERID == -1)
            GROUND_LAYERID = LayerMask.NameToLayer("Ground");
    }

    public void UpdateCollisions() {
        onGround = false;
        hitRoof = false;
        hitRight = false;
        hitLeft = false;
        if (!currentCollider)
            return;
        
        int c = currentCollider.GetContacts(contacts);
        for (int i = 0; i < c; i++) {
            ContactPoint2D point = contacts[i];
            if (Vector2.Dot(point.normal, Vector2.up) > floorAndRoofCutoff) {
                //touching floor
                onGround = true;
            } else if (point.collider.gameObject.layer == GROUND_LAYERID) {
                if (Vector2.Dot(point.normal, Vector2.down) > floorAndRoofCutoff) {
                    //touching roof
                    hitRoof = true;
                } else {
                    //touching a wall
                    hitRight |= point.normal.x < 0;
                    hitLeft |= point.normal.x > 0;
                }
            }
        }
    }
}
