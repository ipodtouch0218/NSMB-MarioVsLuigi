using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "ScriptableObjects/CharacterData", order = 0)]
public class CharacterData : ScriptableObject {
    // TODO public NetworkPrefabRef prefab;
    public string soundFolder, uistring, translationString;
    public Sprite loadingSmallSprite, loadingBigSprite, readySprite;
    public RuntimeAnimatorController smallOverrides, largeOverrides;
}