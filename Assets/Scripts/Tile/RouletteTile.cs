using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;

namespace NSMB.Tiles {

    [CreateAssetMenu(fileName = "RouletteTile", menuName = "ScriptableObjects/Tiles/RouletteTile")]
    public class RouletteTile : BreakableBrickTile, IHaveTileDependencies {

        //---Serialized Variables
        [SerializeField] private TileBase resultTile;
        [SerializeField] private Vector2 topSpawnOffset;

        public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation, out bool bumpSound) {
            if (base.Interact(interacter, direction, worldLocation, out bumpSound))
                return true;

            bumpSound = true;

            Vector2Int tileLocation = Utils.Utils.WorldToTilemapPosition(worldLocation);
            NetworkPrefabRef spawnResult = PrefabList.Instance.Powerup_Mushroom;

            if ((interacter is PlayerController) || (interacter is KoopaWalk koopa && koopa.PreviousHolder != null)) {
                PlayerController player = interacter is PlayerController controller ? controller : ((KoopaWalk) interacter).PreviousHolder;
                if (player.State == Enums.PowerupState.MegaMushroom) {
                    //Break

                    //Tilemap
                    GameManager.Instance.tileManager.SetTile(tileLocation, null);

                    //Particles
                    for (int x = 0; x < 2; x++) {
                        for (int y = 0; y < 2; y++) {
                            GameManager.Instance.particleManager.Play(Enums.Particle.Entity_BrickBreak, Utils.Utils.TilemapToWorldPosition(tileLocation + new Vector2Int(x, y)) + Vector3.one * 0.25f, particleColor);
                        }
                    }

                    player.PlaySound(Enums.Sounds.World_Block_Break);
                    return true;
                }

                spawnResult = Utils.Utils.GetRandomItem(player).prefab;
            }

            Bump(interacter, direction, worldLocation);

            bool downwards = direction == InteractionDirection.Down;
            GameManager.Instance.rpcs.BumpBlock((short) tileLocation.x, (short) tileLocation.y, this,
                resultTile, downwards, topSpawnOffset, false, spawnResult);

            return false;
        }

        public TileBase[] GetTileDependencies() {
            return new TileBase[] { resultTile };
        }
    }
}