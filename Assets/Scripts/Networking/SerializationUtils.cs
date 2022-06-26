using System.Collections.Generic;
using UnityEngine;

public static class SerializationUtils {

    #region INT PRECISION
    public static void PackToInt(List<byte> buffer, Vector2 input, float xMin, float xMax, float? yMin = null, float? yMax = null) {
        SetIfNull(ref yMin, xMin);
        SetIfNull(ref yMax, xMax);

        PackToShort(buffer, input.x, xMin, xMax);
        PackToShort(buffer, input.y, (float) yMin, (float) yMax);
    }

    public static void UnpackFromInt(List<byte> buffer, ref int index, float xMin, float xMax, out Vector2 output, float? yMin = null, float? yMax = null) {
        SetIfNull(ref yMin, xMin);
        SetIfNull(ref yMax, xMax);

        UnpackFromShort(buffer, ref index, xMin, xMax, out float x);
        UnpackFromShort(buffer, ref index, (float) yMin, (float) yMax, out float y);

        output = new(x, y);
    }
    #endregion

    #region SHORT PRECISION
    public static void PackToShort(List<byte> buffer, float input, float min, float max) {
        float range = max - min;
        short shortValue = (short) ((input - min) / range * short.MaxValue);
        ShortToBuffer(buffer, shortValue);
    }

    public static void UnpackFromShort(List<byte> buffer, ref int index, float min, float max, out float output) {
        float range = max - min;
        ushort shortValue = BufferToShort(buffer, ref index);
        output = ((float) shortValue / ushort.MaxValue * range * 2f) + min;
    }

    public static void UnpackFromShort(List<byte> buffer, ref int index, float xMin, float xMax, out Vector2 output, float? yMin = null, float? yMax = null) {
        SetIfNull(ref yMin, xMin);
        SetIfNull(ref yMax, xMax);

        UnpackFromByte(buffer, ref index, xMin, xMax, out float x);
        UnpackFromByte(buffer, ref index, (float) yMin, (float) yMax, out float y);

        output = new(x, y);
    }

    public static void PackToShort(List<byte> buffer, Vector2 input, float xMin, float xMax, float? yMin = null, float? yMax = null) {
        SetIfNull(ref yMin, xMin);
        SetIfNull(ref yMax, xMax);

        PackToByte(buffer, input.x, xMin, xMax);
        PackToByte(buffer, input.y, (float) yMin, (float) yMax);
    }
    #endregion

    public static void PackToByte(List<byte> buffer, float input, float min, float max) {
        float range = max - min;
        byte byteValue = (byte) ((input - min) / range * byte.MaxValue);
        buffer.Add(byteValue);
    }

    public static void UnpackFromByte(List<byte> buffer, ref int index, float min, float max, out float output) {

        float range = max - min;
        byte byteValue = BufferToByte(buffer, ref index);
        output = ((float) byteValue / byte.MaxValue * range) + min;
    }

    public static void PackToByte(List<byte> buffer, params bool[] flags) {
        byte byteValue = 0;
        for (int i = 0; i < flags.Length; i++)
            byteValue |= (byte) ((flags[i] ? 1 : 0) << i);

        buffer.Add(byteValue);
    }

    public static void UnpackFromByte(List<byte> buffer, ref int index, out bool[] output) {
        output = new bool[8];
        byte flags = BufferToByte(buffer, ref index);
        for (int i = 0; i < 8; i++)
            output[i] = Utils.BitTest(flags, i);
    }


    #region helpers
    private static void ShortToBuffer(List<byte> buffer, short input) {
        buffer.Add((byte) (input >> 8));
        buffer.Add((byte) input);
    }

    private static ushort BufferToShort(List<byte> buffer, ref int index) {
        ushort value = 0;

        byte high = buffer[index++];
        byte low = buffer[index++];

        value |= (ushort) (high << 8);
        value |= (ushort) low;

        return value;
    }

    private static byte BufferToByte(List<byte> buffer, ref int index) {
        return buffer[index++];
    }

    private static void SetIfNull<T>(ref T checkValue, T setValue) {
        if (checkValue == null)
            checkValue = setValue;
    }
    #endregion
}