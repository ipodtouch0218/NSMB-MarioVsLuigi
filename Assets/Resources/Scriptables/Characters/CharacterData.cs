using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "ScriptableObjects/CharacterData", order = 0)]
public class CharacterData : ScriptableObject {
    public string soundFolder, prefab, uistring;
    public Sprite loadingSmallSprite, loadingBigSprite, readySprite;
    public RuntimeAnimatorController smallOverrides, largeOverrides;
}