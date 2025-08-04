using Miniscript;
using Photon.Deterministic;
using Quantum;

public class ValEntityRef : Value {

    public EntityRef EntityRef;

    public ValEntityRef(EntityRef entityRef) {
        EntityRef = entityRef;
    }

    public override FP Equality(Value rhs) {
        if (rhs is not ValEntityRef rhsEntityPrototype) {
            return 0;
        }

        return EntityRef == rhsEntityPrototype.EntityRef ? 1 : 0;
    }

    public override int Hash() {
        return EntityRef.GetHashCode();
    }

    public override string ToString(TAC.Machine vm) {
        return EntityRef.ToString();
    }
}