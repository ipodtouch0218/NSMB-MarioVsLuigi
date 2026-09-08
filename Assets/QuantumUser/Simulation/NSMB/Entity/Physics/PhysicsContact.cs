using System;

namespace Quantum {
    public unsafe partial struct PhysicsContact : IEquatable<PhysicsContact> {

        public override readonly bool Equals(object obj) {
            if (obj is PhysicsContact other) {
                // Custom equals- fixes phantom hits
                return Equals(other);
            }

            return false;            
        }

        public readonly bool Equals(PhysicsContact other) {
            return Tile == other.Tile
                && Entity == other.Entity
                && Frame == other.Frame;
        }

        public readonly MvLPhysicsProperties GetPhysicsProperties(Frame f, VersusStageData stage = null) {
            if (Entity != EntityRef.None) {
                // Entity
                if (f.Unsafe.TryGetPointer(Entity, out PhysicsCollider2D* collider)
                    && f.TryFindAsset(collider->Material, out PhysicsMaterial physicsMaterial)
                    && physicsMaterial is MvLPhysicsProperties properties) {

                    return properties;
                }
            } else {
                // Tile
                if (stage == null) {
                    stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                }

                if (f.TryFindAsset(stage.GetTileRelative(f, Tile).Tile, out StageTile stageTile)
                    && f.TryFindAsset(stageTile.PhysicsProperties, out MvLPhysicsProperties properties)) {

                    return properties;
                }
            }

            return null;
        }
    }
}
