using UnityEngine;

using Fusion;
using NSMB.Utils;

public class PhysicsEntity : NetworkBehaviour, IBeforeTick {

    //---Staic Variables
    private static LayerMask GroundMask = default;
    private static readonly ContactPoint2D[] ContactBuffer = new ContactPoint2D[32];

    //---Networked Variables
    [Networked] public ref PhysicsDataStruct Data => ref MakeRef<PhysicsDataStruct>();

    //---Public Variables
    public Collider2D currentCollider;
    public Vector2 previousTickVelocity;

    //---Serialized Variables
    [SerializeField] private bool goUpSlopes;
    [SerializeField] private float floorAndRoofCutoff = 0.5f;

    //---Components
    [SerializeField] private Rigidbody2D body;

    public void Awake() {
        if (!body) body = GetComponent<Rigidbody2D>();
    }

    public void Start() {
        if (GroundMask == default)
            GroundMask = 1 << Layers.LayerGround | 1 << Layers.LayerGroundEntity;
    }

    public void BeforeTick() {
        previousTickVelocity = body.velocity;
    }

    public PhysicsDataStruct UpdateCollisions() {
        int hitRightCount = 0, hitLeftCount = 0;
        float previousHeightY = float.MaxValue;

        Data.CrushableGround = false;
        Data.OnGround = false;
        Data.HitRoof = false;
        Data.FloorAngle = 0;

        if (!currentCollider)
            return Data;

        int c = currentCollider.GetContacts(ContactBuffer);
        for (int i = 0; i < c; i++) {
            ContactPoint2D point = ContactBuffer[i];
            if (point.collider.gameObject == gameObject)
                continue;

            if (Vector2.Dot(point.normal, Vector2.up) > floorAndRoofCutoff) {
                // touching floor
                // If we're moving upwards, don't touch the floor.
                // Most likely, we're inside a semisolid.
                if (previousTickVelocity.y > 0)
                    continue;

                // Make sure that we're also above the floor, so we don't
                // get crushed when inside a semisolid.
                if (point.point.y > currentCollider.bounds.min.y + 0.01f)
                    continue;

                Data.OnGround = true;
                Data.CrushableGround |= !point.collider.gameObject.CompareTag("platform");
                Data.FloorAngle = Vector2.SignedAngle(Vector2.up, point.normal);

            } else if (GroundMask == (GroundMask | (1 << point.collider.gameObject.layer))) {
                if (Vector2.Dot(point.normal, Vector2.down) > floorAndRoofCutoff) {
                    // touching roof
                    Data.HitRoof = true;
                } else {
                    // touching a wall
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

    public struct PhysicsDataStruct : INetworkStruct {

        public byte Flags;
        public float FloorAngle;

        public bool OnGround {
            get => Utils.BitTest(Flags, 0);
            set => Utils.BitSet(ref Flags, 0, value);
        }
        public bool CrushableGround {
            get => Utils.BitTest(Flags, 1);
            set => Utils.BitSet(ref Flags, 1, value);
        }
        public bool HitRoof {
            get => Utils.BitTest(Flags, 2);
            set => Utils.BitSet(ref Flags, 2, value);
        }
        public bool HitLeft {
            get => Utils.BitTest(Flags, 3);
            set => Utils.BitSet(ref Flags, 3, value);
        }
        public bool HitRight {
            get => Utils.BitTest(Flags, 4);
            set => Utils.BitSet(ref Flags, 4, value);
        }
    }
}
