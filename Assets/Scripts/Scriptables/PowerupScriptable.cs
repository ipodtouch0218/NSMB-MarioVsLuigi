using UnityEngine;

using Fusion;

[CreateAssetMenu(fileName = "Powerup", menuName = "ScriptableObjects/Powerup", order = 0)]
public class PowerupScriptable : ScriptableObject {

    public Enums.PowerupState state;
    public NetworkPrefabRef prefab;
    public bool soundPlaysEverywhere;
    public Enums.Sounds soundEffect = Enums.Sounds.Player_Sound_PowerupCollect;
    public Enums.Sounds powerupBlockEffect = Enums.Sounds.World_Block_Powerup;
    public float spawnChance = 0.1f, losingSpawnBonus = 0f;
    public bool big, vertical, custom, lives;
    public Sprite reserveSprite;

    public sbyte statePriority = -1, itemPriority = -1;

    public float GetModifiedChance(float starsToWin, float leaderStars, float ourStars) {
        float starDifference = leaderStars - ourStars;
        float bonus = losingSpawnBonus * Mathf.Log(starDifference + 1) * (1f - ((starsToWin - leaderStars) / starsToWin));
        return Mathf.Max(0, spawnChance + bonus);
    }
}
