using System;

namespace Quantum {
    public partial struct PhysicsContact : IEquatable<PhysicsContact> {
        public override bool Equals(object obj) {
            if (obj is not PhysicsContact other) {
                return false;
            }

            // Custom equals- fixes phantom hits
            return Equals(obj);
        }

        public bool Equals(PhysicsContact other) {
            return TileX == other.TileX
                && TileY == other.TileY
                && Entity == other.Entity
                && Frame == other.Frame;
        }
    }
}
