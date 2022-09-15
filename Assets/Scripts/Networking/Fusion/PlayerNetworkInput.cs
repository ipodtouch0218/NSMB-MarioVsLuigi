using Fusion;
using NSMB.Utils;

public struct PlayerNetworkInput : INetworkInput {

    /// <summary>
    /// Controls byte.
    /// Bit 7: Up
    /// Bit 6: Down
    /// Bit 5: Left
    /// Bit 4: Right
    /// Bit 3: Jump
    /// Bit 2: Sprint
    /// Bit 1:
    /// Bit 0:
    /// </summary>
    public byte inputs;

    public bool Up {
        get => Utils.BitTest(inputs, 7);
        set => Utils.BitSet(ref inputs, 7, value);
    }
    public bool Down {
        get => Utils.BitTest(inputs, 6);
        set => Utils.BitSet(ref inputs, 6, value);
    }
    public bool Left {
        get => Utils.BitTest(inputs, 5);
        set => Utils.BitSet(ref inputs, 5, value);
    }
    public bool Right {
        get => Utils.BitTest(inputs, 4);
        set => Utils.BitSet(ref inputs, 4, value);
    }
    public bool Jump {
        get => Utils.BitTest(inputs, 3);
        set => Utils.BitSet(ref inputs, 3, value);
    }
    public bool Sprint {
        get => Utils.BitTest(inputs, 2);
        set => Utils.BitSet(ref inputs, 2, value);
    }
}