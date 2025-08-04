using Miniscript;
using Photon.Deterministic;
using Quantum;

public class ValPhysicsQueryRef : Value {

    public PhysicsQueryRef PhysicsQuery;

    public override unsafe void Serialize(FrameSerializer serializer) {
        fixed (void* ptr = &PhysicsQuery) {
            PhysicsQueryRef.Serialize(ptr, serializer);
        }
    }

    public override FP Equality(Value rhs) {
        if (rhs is not ValPhysicsQueryRef rhsEntityPrototype) {
            return 0;
        }

        return PhysicsQuery.Equals(rhsEntityPrototype.PhysicsQuery) ? 1 : 0;
    }

    public override int Hash() {
        return PhysicsQuery.GetHashCode();
    }

    public override string ToString(TAC.Machine vm) {
        return PhysicsQuery.ToString();
    }
}