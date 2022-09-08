using UnityEngine;

[CreateAssetMenu(fileName = "Powerup", menuName = "ScriptableObjects/Powerup", order = 0)]
public class Powerup : ScriptableObject {

    public Enums.PowerupState state;
    public string prefab;
    public Enums.Sounds soundEffect = Enums.Sounds.Player_Sound_PowerupCollect;
    public float spawnChance = 0.1f, losingSpawnBonus = 0f;
    public bool big, vertical, custom, lives;
    public Sprite reserveSprite;

    public float GetModifiedChance(float starsToWin, float leaderStars, float ourStars) {
        float starDifference = leaderStars - ourStars;
        float bonus = losingSpawnBonus * Mathf.Log(starDifference + 1) * (1f - ((starsToWin - leaderStars) / starsToWin));
        Debug.Log(state + ", leader at " + leaderStars + ", win at " + starsToWin + ", we're at " + ourStars + " = " + (spawnChance + bonus));
        return Mathf.Max(0, spawnChance + bonus);
    }
}