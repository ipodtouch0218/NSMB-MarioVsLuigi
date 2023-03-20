using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerColor", menuName = "ScriptableObjects/PlayerColorSet")]
public class PlayerColorSet : ScriptableObject {

    public PlayerColors[] colors = { new() };
    public string ColorText = "Placeholder";
    public PlayerColors GetPlayerColors(CharacterData player) {

        PlayerColors nullPlayer = null;
        foreach (PlayerColors color in colors) {
            if (player == color.player) {
                ColorText = color.Name;
                return color;
            }

            if (color.player == null)
                nullPlayer = color;
        }
        ColorText = nullPlayer.Name;
        return nullPlayer ?? colors[0];
    }
}

[System.Serializable]
public class PlayerColors {

    public CharacterData player;
    public Color hatColor = Color.black, overallsColor = Color.black;
    public string Name = "Placeholder";

}