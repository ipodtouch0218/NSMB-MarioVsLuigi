using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Powerup", menuName = "ScriptableObjects/Powerup", order = 0)]
public class Powerup : ScriptableObject {
    public string prefab;
    public float spawnChance = 0.1f, starModifier = 0f;

    public float GetModifiedChance(float starPercentage) {
        return Mathf.Max(0, spawnChance + (starModifier * starPercentage));
    }
}