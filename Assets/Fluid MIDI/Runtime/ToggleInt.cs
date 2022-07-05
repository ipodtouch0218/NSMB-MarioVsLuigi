using System;

namespace FluidMidi
{
    [Serializable]
    public struct ToggleInt
    {
        public bool Enabled;
        public int Value;

        public ToggleInt(bool enabled, int value)
        {
            Enabled = enabled;
            Value = value;
        }
    }
}