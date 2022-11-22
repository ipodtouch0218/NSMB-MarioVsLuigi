using UnityEngine;

[CreateAssetMenu(fileName = "New Team", menuName = "ScriptableObjects/Team")]
public class Team : ScriptableObject {

    public byte id;
    public string displayName, textSprite;

    [ColorUsage(false)] public Color color;

}
