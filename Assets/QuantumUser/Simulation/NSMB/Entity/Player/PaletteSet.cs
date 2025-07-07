using Quantum;
using System;
using UnityEngine;
using UnityEngine.Serialization;

public class PaletteSet : AssetObject {

    public CharacterSpecificPalette[] colors = { new() };
    public string translationKey;

    public CharacterSpecificPalette GetPaletteForCharacter(AssetRef<CharacterAsset> player) {
        CharacterSpecificPalette nullPlayer = null;
        foreach (CharacterSpecificPalette color in colors) {
            if (player.Equals(color.Character)) {
                return color;
            }

            if (color.Character == null) {
                nullPlayer = color;
            }
        }
        return nullPlayer ?? colors[0];
    }
}

[Serializable]
public class CharacterSpecificPalette {

    [FormerlySerializedAs("character")] public AssetRef<CharacterAsset> Character;
    public ColorRGBA ShirtColor;
    public ColorRGBA OverallsColor;
    [FormerlySerializedAs("hatUsesOverallsColor")] public bool HatUsesOverallsColor;

}