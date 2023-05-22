using Fusion;
using NSMB.Utils;

public struct NetworkBitArray : INetworkStruct
{
    private ulong bits;

    public bool this[int index] {
        get {
            return Utils.BitTest(bits, index);
        }
        set {
            Utils.BitSet(ref bits, index, value);
        }
    }

    public void RawSet(ulong value) {
        bits = value;
    }

    public ulong RawGet() {
        return bits;
    }


    public int Count => 64;

    public int SetBitCount() {
        int count = 0;
        ulong n = bits;
        while (n != 0) {
            n = n & (n-1);
            count ++;
        }
        return count;
    }

    public int UnsetBitCount() {
        return Count - SetBitCount();
    }

    public bool GetNthSetBitIndex(int n, out int result) {
        int setBits = 0;
        for (int i = 0; i < Count; i++) {
            if (!this[i])
                continue;

            if (++setBits >= n) {
                result = i;
                return true;
            }
        }
        result = -1;
        return false;
    }
}
