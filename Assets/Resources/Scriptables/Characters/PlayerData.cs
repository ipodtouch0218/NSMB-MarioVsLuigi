using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "ScriptableObjects/PlayerData", order = 0)]
public class PlayerData : ScriptableObject {
    public string soundFolder, prefab, uistring;
    public Sprite buttonSprite, trackSprite, loadingSmallSprite, loadingBigSprite, readySprite;
}