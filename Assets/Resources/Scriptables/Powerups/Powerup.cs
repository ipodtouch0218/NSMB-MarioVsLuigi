using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Powerup", menuName = "ScriptableObjects/Powerup", order = 0)]
public class Powerup : ScriptableObject {

    public Enums.PowerupState state;
    public string prefab;
    public Enums.Sounds soundEffect = Enums.Sounds.Player_Sound_PowerupCollect;
    public float spawnChance = 0.1f, starModifier = 0f;
    public bool big, vertical, custom;
    public float GetModifiedChance(float starPercentage) {
        return Mathf.Max(0, spawnChance + (starModifier * starPercentage));
    }
}