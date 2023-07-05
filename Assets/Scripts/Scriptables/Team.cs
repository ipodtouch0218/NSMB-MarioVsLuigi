using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "New Team", menuName = "ScriptableObjects/Team")]
public class Team : ScriptableObject {

    public byte id;
    public string displayName;
    [FormerlySerializedAs("textSprite")] public string textSpriteNormal;
    public string textSpriteColorblind, textSpriteColorblindBig;

    [ColorUsage(false)] public Color color;
    public Sprite spriteNormal, spriteColorblind;

}
