using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewPaletteSet", menuName = "ScriptableObjects/PaletteSet")]
public class PaletteSet : ScriptableObject {

    public CharacterSpecificPalette[] colors = { new() };
    public string translationKey;

    public string Name => GlobalController.Instance.translationManager.GetTranslation(translationKey);

    public CharacterSpecificPalette GetPaletteForCharacter(CharacterAsset player) {
        CharacterSpecificPalette nullPlayer = null;
        foreach (CharacterSpecificPalette color in colors) {
            if (player == color.character) {
                return color;
            }

            if (color.character == null) {
                nullPlayer = color;
            }
        }
        return nullPlayer ?? colors[0];
    }
}

[Serializable]
public class CharacterSpecificPalette {

    public CharacterAsset character;
    [FormerlySerializedAs("hatColor")] public Color shirtColor = Color.black;
    public Color overallsColor = Color.black;
    public bool hatUsesOverallsColor;

}