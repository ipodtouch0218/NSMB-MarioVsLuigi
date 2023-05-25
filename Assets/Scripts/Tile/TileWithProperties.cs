using UnityEngine;

namespace NSMB.Tiles {

    [CreateAssetMenu(fileName = "TileWithProperties", menuName = "ScriptableObjects/Tiles/TileWithProperties", order = 3)]
    public class TileWithProperties : SiblingRuleTile {
        public bool isBackgroundTile = false, iceSkidding = false, isSlope = false;
        public Enums.Sounds footstepSound = Enums.Sounds.Player_Walk_Grass;
        public Enums.Particle footstepParticle = Enums.Particle.None;
    }
}
