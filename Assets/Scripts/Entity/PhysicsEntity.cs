using UnityEngine;

using Fusion;

public class PhysicsEntity : NetworkBehaviour {

    //---Staic Variables
    private static LayerMask GroundMask = 0;
    private static readonly ContactPoint2D[] ContactBuffer = new ContactPoint2D[32];

    //---Networked Variables
    [Networked] public NetworkBool OnGround { get; set; }
    [Networked] public NetworkBool HitRoof { get; set; }
    [Networked] public NetworkBool HitLeft { get; set; }
    [Networked] public NetworkBool HitRight { get; set; }
    [Networked] public NetworkBool CrushableGround { get; set; }
    [Networked] public float FloorAngle { get; set; }

    //---Public Variables
    public Collider2D currentCollider;

    //---Serialized Variables
    [SerializeField] private bool goUpSlopes;
    [SerializeField] private float floorAndRoofCutoff = 0.3f;

    private void Start() {
        if (GroundMask == 0)
            GroundMask = LayerMask.GetMask("Ground", "IceBlock");
    }

    public void UpdateCollisions() {
        int hitRightCount = 0, hitLeftCount = 0;
        float previousHeightY = float.MaxValue;
        CrushableGround = false;
        OnGround = false;
        HitRoof = false;
        FloorAngle = 0;
        if (!currentCollider)
            return;

        //Runner.GetPhysicsScene2D().Simulate(0f);
        int c = currentCollider.GetContacts(ContactBuffer);
        for (int i = 0; i < c; i++) {
            ContactPoint2D point = ContactBuffer[i];
            if (point.collider.gameObject == gameObject)
                continue;

            if (Vector2.Dot(point.normal, Vector2.up) > floorAndRoofCutoff) {
                //touching floor
                OnGround = true;
                CrushableGround |= !point.collider.gameObject.CompareTag("platform");
                FloorAngle = Vector2.SignedAngle(Vector2.up, point.normal);
            } else if (GroundMask == (GroundMask | (1 << point.collider.gameObject.layer))) {
                if (Vector2.Dot(point.normal, Vector2.down) > floorAndRoofCutoff) {
                    //touching roof
                    HitRoof = true;
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
        HitRight = hitRightCount >= 1;
        HitLeft = hitLeftCount >= 1;
    }
}
