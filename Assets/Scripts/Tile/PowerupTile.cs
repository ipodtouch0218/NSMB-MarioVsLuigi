using UnityEngine;

using NSMB.Utils;
using Fusion;

[CreateAssetMenu(fileName = "PowerupTile", menuName = "ScriptableObjects/Tiles/PowerupTile")]
public class PowerupTile : BreakableBrickTile {

    public string resultTile;

    public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (base.Interact(interacter, direction, worldLocation))
            return true;

        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        NetworkPrefabRef spawnResult = PrefabList.Instance.Powerup_Mushroom;

        if ((interacter is PlayerController) || (interacter is KoopaWalk koopa && koopa.PreviousHolder != null)) {
            PlayerController player = interacter is PlayerController controller ? controller : ((KoopaWalk)interacter).PreviousHolder;
            if (player.State == Enums.PowerupState.MegaMushroom) {
                //Break

                //Tilemap
                GameManager.Instance.tilemap.SetTile(tileLocation, null);

                //Particle
                //TODO:
                //object[] parametersParticle = new object[]{tileLocation.x, tileLocation.y, "BrickBreak", new Vector3(particleColor.r, particleColor.g, particleColor.b)};
                //GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SpawnParticle, parametersParticle, ExitGames.Client.Photon.SendOptions.SendUnreliable);

                interacter.PlaySound(Enums.Sounds.World_Block_Break);
                return true;
            }

            if (player.State > Enums.PowerupState.MiniMushroom)
                spawnResult = PrefabList.Instance.Powerup_FireFlower;
        }

        Bump(interacter, direction, worldLocation);

        if (GameManager.Instance.Object.HasStateAuthority) {
            GameManager.Instance.rpcs.Rpc_BumpBlock((short) tileLocation.x, (short) tileLocation.y, "",
                resultTile, direction == InteractionDirection.Down, Vector2.zero, false, spawnResult);
        }

        interacter.PlaySound(Enums.Sounds.World_Block_Powerup);
        return false;
    }
}
