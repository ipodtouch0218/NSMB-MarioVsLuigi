using UnityEngine;


[CreateAssetMenu(fileName = "Powerup", menuName = "ScriptableObjects/Powerup", order = 0)]
public class PowerupScriptable : ScriptableObject {

    // public Enums.PowerupState state;
    // TODO public NetworkPrefabRef prefab;
    public bool soundPlaysEverywhere;
    public SoundEffect SoundEffectEffect = SoundEffect.Player_Sound_PowerupCollect;
    public SoundEffect powerupBlockEffect = SoundEffect.World_Block_Powerup;
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
