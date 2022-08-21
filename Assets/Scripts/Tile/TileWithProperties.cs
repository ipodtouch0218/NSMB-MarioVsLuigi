using UnityEngine;

[CreateAssetMenu(fileName = "TileWithProperties", menuName = "ScriptableObjects/Tiles/TileWithProperties", order = 3)]
public class TileWithProperties : SiblingRuleTile {
    public bool isBackgroundTile = false, iceSkidding = false;
    public Enums.Sounds footstepSound = Enums.Sounds.Player_Walk_Grass;
}
