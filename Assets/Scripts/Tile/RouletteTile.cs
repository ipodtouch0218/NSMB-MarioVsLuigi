using UnityEngine;

using NSMB.Utils;
using Fusion;

[CreateAssetMenu(fileName = "RouletteTile", menuName = "ScriptableObjects/Tiles/RouletteTile")]
public class RouletteTile : BreakableBrickTile {

    public string resultTile;
    public Vector2 topSpawnOffset, bottomSpawnOffset;

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

                        //TODO:
                        //object[] parametersParticle = new object[] { tileLocation.x + x, tileLocation.y - y, "BrickBreak", new Vector3(particleColor.r, particleColor.g, particleColor.b) };
                        //GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SpawnParticle, parametersParticle, ExitGames.Client.Photon.SendOptions.SendUnreliable);

                    }
                }

                player.PlaySound(Enums.Sounds.World_Block_Break);
                return true;
            }

            spawnResult = Utils.GetRandomItem(NetworkHandler.Instance.runner, player).prefab;
        }

        Bump(interacter, direction, worldLocation);

        Vector2 offset = direction == InteractionDirection.Down ? bottomSpawnOffset + ( spawnResult == PrefabList.Instance.Powerup_MegaMushroom ? Vector2.down * 0.5f : Vector2.zero) : topSpawnOffset;
        GameManager.Instance.CreateBlockBump(tileLocation.x, tileLocation.y, direction == InteractionDirection.Down, resultTile, spawnResult, false, offset);

        interacter.PlaySound(Enums.Sounds.World_Block_Powerup);

        return false;
    }
}