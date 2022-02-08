using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsEntity : MonoBehaviour {

    private static int GROUND_LAYERID;

    public bool onGround, hitRoof, hitRight, hitLeft; 
    public Collider2D currentCollider;
    public float floorAndRoofCutoff = 0.3f;
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[32];

    private void Awake() {
        GROUND_LAYERID = LayerMask.NameToLayer("Ground");
    }

    public void Update() {
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
                    if (point.normal.x < 0) {
                        //normal points to the left, so touching RIGHT wall
                        hitRight = true;
                    } else {
                        //normal points to the right, so touching LEFT wall
                        hitLeft = true;
                    }
                }
            }
        }
    }
}
