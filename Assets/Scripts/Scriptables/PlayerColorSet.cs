using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewPlayerColor", menuName = "ScriptableObjects/PlayerColorSet")]
public class PlayerColorSet : ScriptableObject {

    public PlayerColors[] colors = { new() };
    public string translationKey;

    public string Name => GlobalController.Instance.translationManager.GetTranslation(translationKey);

    public PlayerColors GetPlayerColors(CharacterAsset player) {
        PlayerColors nullPlayer = null;
        foreach (PlayerColors color in colors) {
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
public class PlayerColors {

    public CharacterAsset character;
    [FormerlySerializedAs("hatColor")] public Color shirtColor = Color.black;
    public Color overallsColor = Color.black;
    public bool hatUsesOverallsColor;

}