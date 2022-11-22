using UnityEngine;

using Fusion;
using NSMB.Utils;

[CreateAssetMenu(fileName = "RouletteTile", menuName = "ScriptableObjects/Tiles/RouletteTile")]
public class RouletteTile : BreakableBrickTile {

    //---Serialized Variables
    [SerializeField] private string resultTile;
    [SerializeField] private Vector2 topSpawnOffset, bottomSpawnOffset;

    public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (base.Interact(interacter, direction, worldLocation))
            return true;

        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        NetworkPrefabRef spawnResult = PrefabList.Instance.Powerup_Mushroom;

        if ((interacter is PlayerController) || (interacter is KoopaWalk koopa && koopa.PreviousHolder != null)) {
            PlayerController player = interacter is PlayerController controller ? controller : ((KoopaWalk) interacter).PreviousHolder;
            if (player.State == Enums.PowerupState.MegaMushroom) {
                //Break

                //Tilemap
                GameManager.Instance.tilemap.SetTile(tileLocation, null);

                //Particles
                for (int x = 0; x < 2; x++) {
                    for (int y = 0; y < 2; y++) {
                        GameManager.Instance.particleManager.Play(Enums.Particle.Entity_BrickBreak, Utils.TilemapToWorldPosition(tileLocation + new Vector3Int(x, y, 0)) + Vector3.one * 0.25f, particleColor);
                    }
                }

                player.PlaySound(Enums.Sounds.World_Block_Break);
                return true;
            }

            spawnResult = Utils.GetRandomItem(player).prefab;
        }

        Bump(interacter, direction, worldLocation);

        if (GameManager.Instance.Object.HasStateAuthority) {
            bool downwards = direction == InteractionDirection.Down;
            Vector2 offset = downwards ? bottomSpawnOffset + (spawnResult == PrefabList.Instance.Powerup_MegaMushroom ? Vector2.down * 0.5f : Vector2.zero) : topSpawnOffset;

            GameManager.Instance.rpcs.Rpc_BumpBlock((short) tileLocation.x, (short) tileLocation.y, "",
                resultTile, downwards, offset, false, spawnResult);
        }

        interacter.PlaySound(Enums.Sounds.World_Block_Powerup);

        return false;
    }
}