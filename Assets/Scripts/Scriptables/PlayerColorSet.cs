using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewPlayerColor", menuName = "ScriptableObjects/PlayerColorSet")]
public class PlayerColorSet : ScriptableObject {

    public PlayerColors[] colors = { new() };
    public string translationKey;

    public string Name => GlobalController.Instance.translationManager.GetTranslation(translationKey);

    public PlayerColors GetPlayerColors(CharacterData player) {

        PlayerColors nullPlayer = null;
        foreach (PlayerColors color in colors) {
            if (player == color.player) {
                return color;
            }

            if (color.player == null)
                nullPlayer = color;
        }
        return nullPlayer ?? colors[0];
    }
}

[Serializable]
public class PlayerColors {

    public CharacterData player;
    [FormerlySerializedAs("hatColor")] public Color shirtColor = Color.black;
    public Color overallsColor = Color.black;
    public bool hatUsesOverallsColor;

}