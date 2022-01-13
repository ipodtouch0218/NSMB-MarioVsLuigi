using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TileWithProperties", menuName = "ScriptableObjects/Tiles/TileWithProperties", order = 3)]
public class TileWithProperties : RuleTile {
    public bool isBackgroundTile = false, iceSkidding = false;
    public string footstepMaterial = "";
    [Range(0,1)] public float frictionFactor = 1;
}
