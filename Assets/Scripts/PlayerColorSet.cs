using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerColor", menuName = "ScriptableObjects/PlayerColorSet")]
public class PlayerColorSet : ScriptableObject {

    public PlayerColors[] colors = { new() };

    public PlayerColors GetPlayerColors(PlayerData player) {

        PlayerColors nullPlayer = null;
        foreach (PlayerColors color in colors) {
            if (player == color.player)
                return color;

            if (color.player == null)
                nullPlayer = color;
        }

        return nullPlayer ?? colors[0];
    }
}

[System.Serializable]
public class PlayerColors {

    public PlayerData player;
    public Color hatColor = Color.black, overallsColor = Color.black;

}