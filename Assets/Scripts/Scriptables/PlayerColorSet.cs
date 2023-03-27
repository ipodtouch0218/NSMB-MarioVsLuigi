using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewPlayerColor", menuName = "ScriptableObjects/PlayerColorSet")]
public class PlayerColorSet : ScriptableObject {

    public PlayerColors[] colors = { new() };
    [HideInInspector, NonSerialized] public string currentColorName = "Placeholder";

    public PlayerColors GetPlayerColors(CharacterData player) {

        PlayerColors nullPlayer = null;
        foreach (PlayerColors color in colors) {
            if (player == color.player) {
                currentColorName = color.name;
                return color;
            }

            if (color.player == null)
                nullPlayer = color;
        }
        currentColorName = nullPlayer.name;
        return nullPlayer ?? colors[0];
    }
}

[Serializable]
public class PlayerColors {

    public CharacterData player;
    [FormerlySerializedAs("hatColor")] public Color shirtColor = Color.black;
    public Color overallsColor = Color.black;
    [FormerlySerializedAs("Name")] public string name = "Placeholder";
    public bool hatUsesOverallsColor;

}