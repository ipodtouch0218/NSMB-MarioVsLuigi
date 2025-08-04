using Miniscript;
using Photon.Deterministic;
using Quantum;

public class ValEntityRef : Value {

    public EntityRef EntityRef;

    public override unsafe void Serialize(FrameSerializer serializer) {
        fixed (void* ptr = &EntityRef) {
            EntityRef.Serialize(ptr, serializer);
        }
    }

    public override FP Equality(Value rhs) {
        if (rhs is not ValEntityRef rhsEntityPrototype) {
            return 0;
        }

        return EntityRef.Equals(rhsEntityPrototype.EntityRef) ? 1 : 0;
    }

    public override int Hash() {
        return EntityRef.GetHashCode();
    }

    public override string ToString(TAC.Machine vm) {
        return EntityRef.ToString();
    }
}