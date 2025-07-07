using System;

namespace Quantum {
    public unsafe partial struct PhysicsContact : IEquatable<PhysicsContact> {

        public override bool Equals(object obj) {
            if (obj is PhysicsContact other) {
                // Custom equals- fixes phantom hits
                return Equals(other);
            }

            return false;            
        }

        public bool Equals(PhysicsContact other) {
            return Tile == other.Tile
                && Entity == other.Entity
                && Frame == other.Frame;
        }
    }
}
