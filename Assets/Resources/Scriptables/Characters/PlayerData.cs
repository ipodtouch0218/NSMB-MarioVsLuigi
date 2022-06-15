using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "ScriptableObjects/PlayerData", order = 0)]
public class PlayerData : ScriptableObject {
    public string soundFolder, prefab, uistring;
    public Sprite loadingSmallSprite, loadingBigSprite, readySprite;
    public RuntimeAnimatorController smallOverrides, largeOverrides;
}