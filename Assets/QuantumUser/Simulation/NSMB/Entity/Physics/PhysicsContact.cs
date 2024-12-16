using UnityEngine;

namespace Quantum {
    public partial struct PhysicsContact {
        public override bool Equals(object obj) {
            if (obj is not PhysicsContact other) {
                return false;
            }

            // Custom equals- fixes phantom hits
            return TileX == other.TileX
                && TileY == other.TileY
                && Entity == other.Entity
                && Frame == other.Frame;
        }
    }
}
