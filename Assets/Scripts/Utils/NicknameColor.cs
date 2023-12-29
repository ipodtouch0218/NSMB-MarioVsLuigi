using System;
using UnityEngine;

using Fusion;

public class NicknameColor {
    public readonly Color color;
    public readonly bool isRainbow;

    public NicknameColor(Color? color = null, bool isRainbow = false) {
        this.color = color ?? Color.white;
        this.isRainbow = isRainbow;
    }

    private static readonly NicknameColor White = new();
    public static NicknameColor FromConnectionToken(ConnectionToken data) {

        if (!data.HasValidSignature())
            return White;

        NetworkString<_8> colorString = data.signedData.NicknameColor;
        if (colorString == "")
            return White;

        NicknameColor ret;
        if (colorString.StartsWith("#")) {
            string colorStringValue = data.signedData.NicknameColor.Value;
            byte r = Convert.ToByte(colorStringValue[1..3], 16);
            byte g = Convert.ToByte(colorStringValue[3..5], 16);
            byte b = Convert.ToByte(colorStringValue[5..7], 16);
            ret = new(color: new Color32(r, g, b, 255));
        } else if (colorString.Equals("rainbow")) {
            ret = new(isRainbow: true);
        } else {
            return White;
        }

        return ret;
    }
}
