using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Physics2D;

namespace Quantum {
    public unsafe class BetterPhysicsObjectSystem : SystemMainThreadFilterStage<BetterPhysicsObjectSystem.Filter> {

        public static readonly FP GroundMaxAngle = FP.FromString("0.07612"); // 1 - cos(22.5 degrees)
        private const int SolverIterations = 4;
        private static FP Penetration = FP._0_01;
        private static FP CorrectionRate = FP._0_50;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public BetterPhysicsObject* PhysicsObject;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;

            physicsObject->Shape = Shape2D.CreateBox(FPVector2.One / 2);
            physicsObject->Gravity = FPVector2.Down * FP._0_33;
            physicsObject->Velocity += physicsObject->Gravity;

            // Solver
            FPVector2 finalVelocity = physicsObject->Velocity * f.DeltaTime;
            transform->Position += finalVelocity;

            var contacts = f.ResolveList(physicsObject->Contacts);
            contacts.Clear();
            FindContacts(f, ref filter, contacts);

            if (contacts.Count > 0) {
                for (int i = 0; i < SolverIterations; i++) {
                    if (SolverIteration(f, ref filter, contacts)) {
                        break;
                    }
                }
            }
        }

        public static void FindContacts(Frame f, ref Filter filter, in QList<BetterPhysicsContact> contacts) {
            var hits = f.Physics2D.OverlapShape(filter.Transform->Position, 0, filter.PhysicsObject->Shape, f.Context.ExcludeEntityAndPlayerMask, QueryOptions.HitAll);
            for (int i = 0; i < hits.Count; i++) {
                var hit = hits[i];
                if (hit.IsTrigger) {
                    continue;
                }

                if (hit.Entity == filter.Entity) {
                    continue;
                }

                var contact = new BetterPhysicsContact() {
                    Hit = hit
                };
                contacts.Add(contact);
            }
        }

        public static bool SolverIteration(Frame f, ref Filter filter, in QList<BetterPhysicsContact> contacts) {
            bool corrected = false;
            var transform = filter.Transform;

            // for each contact
            for (int i = 0; i < contacts.Count; i++) {
                // compute penetration
                BetterPhysicsContact* contact = contacts.GetPointer(i);

                /*
                // already ignored on previous iteration
                if (result.Ignore) {
                    // continue to next contact (do not apply anything about this)
                    continue;
                }
                */
                

                // contact.ContactType = KCCContactType.NONE;
                contact->HasOverlap = ComputePenetration(f, transform, &(filter.PhysicsObject->Shape), contact);
                // contact.SurfaceTangent = FPVector2.Rotate(contact.Contact.Normal, -FP.Rad_90);
                // contact.ContactAngle = FPVector2.Angle(FPVector2.Up, contact.Contact.Normal);

                // can be used to modify normals, etc
                // _context.Frame.Signals.OnKCC2DSolverCollision(_context.Entity, _context.KCC, &contact, iteration);
                // _contacts[i] = contact;

                /*
                // identify contact type
                if (contact.ContactAngle < _context.Settings.MaxSlopeAngle) {
                    contact.ContactType = KCCContactType.GROUND;
                } else {
                    if (contact.ContactAngle > 90 + _context.Settings.MaxSlopeAngle) {
                        contact.ContactType = KCCContactType.CEIL;
                    } else {
                        if (_context.Settings.WallJumpEnabled && contact.ContactAngle > _context.Settings.MinWallAngle && contact.ContactAngle < _context.Settings.MaxWallAngle) {
                            contact.ContactType = KCCContactType.WALL;
                        } else {
                            contact.ContactType = KCCContactType.SLOPE;
                        }
                    }
                }

                // priority for "closest" contact (ground -> wall -> slope -> ceil)
                if (_context.KCC->Closest.ContactType == KCCContactType.NONE || _context.KCC->Closest.ContactType > contact.ContactType) {
                    _context.KCC->Closest = contact;
                }
                */

                // apply correction
                if (contact->Hit.OverlapPenetration > Penetration) {
                    var fullCorrection = contact->Hit.Normal * contact->Hit.OverlapPenetration;
                    Draw.Ray(transform->Position, fullCorrection, ColorRGBA.Red);
                    var correction = fullCorrection * CorrectionRate;
                    transform->Position += correction;
                    corrected = true;
                }
            }
            return corrected;
        }


        private static bool ComputePenetration(Frame f, Transform2D* transform, Shape2D* shape, BetterPhysicsContact* contact) {
            Hit h = contact->Hit;
            var hits = f.Physics2D.CheckOverlap(shape, transform, &h);
            if (hits.Count > 0) {
                contact->Hit = hits[0];
                return true;
            }
            return false;
        }
    }
}
