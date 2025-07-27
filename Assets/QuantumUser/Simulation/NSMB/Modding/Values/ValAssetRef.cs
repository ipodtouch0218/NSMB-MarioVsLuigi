using Miniscript;
using Photon.Deterministic;
using Quantum;

public class ValAssetRef : Value {

    public AssetRef Asset;

    public ValAssetRef(AssetRef asset) {
        Asset = asset;
    }

    public override FP Equality(Value rhs) {
        if (rhs is not ValAssetRef rhsAssetObject) {
            return 0;
        }
        return (Asset == rhsAssetObject.Asset) ? 1 : 0;
    }

    public override int Hash() {
        return Asset.GetHashCode();
    }

    public override string ToString(TAC.Machine vm) {
        return Asset.ToString();
    }
}