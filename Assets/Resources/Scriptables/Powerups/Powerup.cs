using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Powerup", menuName = "ScriptableObjects/Powerup", order = 0)]
public class Powerup : ScriptableObject {

    public Enums.PowerupState state;
    public string prefab, soundEffect = "powerup";
    public float spawnChance = 0.1f, starModifier = 0f;
    public bool big, tall, custom;
    public float GetModifiedChance(float starPercentage) {
        return Mathf.Max(0, spawnChance + (starModifier * starPercentage));
    }
}