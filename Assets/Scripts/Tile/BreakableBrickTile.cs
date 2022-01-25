using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "BreakableBrickTile", menuName = "ScriptableObjects/Tiles/BreakableBrickTile", order = 0)]
public class BreakableBrickTile : InteractableTile {
    [ColorUsage(false)]
    public Color particleColor;
    public bool breakableBySmallMario = false, breakableByLargeMario = true, breakableByGiantMario = true, breakableByShells = true, breakableByBombs = true, bumpIfNotBroken = true, bumpIfBroken = true;
    protected bool BreakBlockCheck(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        bool doBump = false, doBreak = false;
        if (interacter is PlayerController) {
            PlayerController pl = (PlayerController) interacter;
            if (pl.state <= Enums.PowerupState.Small) {
                doBreak = breakableBySmallMario;
                doBump = true;
            } else if (pl.state == Enums.PowerupState.Giant) {
                doBreak = breakableByGiantMario;
                doBump = false;
            } else if (pl.state >= Enums.PowerupState.Large) {
                doBreak = breakableByLargeMario;
                doBump = true;
            }
        } else if (interacter is KoopaWalk) {
            doBump = true;
            doBreak = breakableByShells;
        } else if (interacter is BobombWalk) {
            doBump = false;
            doBreak = breakableByBombs;
        }
        if (doBump && doBreak && bumpIfBroken) Bump(interacter, direction, worldLocation);
        if (doBump && !doBreak && bumpIfNotBroken) BumpWithAnimation(interacter, direction, worldLocation);
        if (doBreak) Break(interacter, direction, worldLocation);
        return doBreak;
    }
    public void Break(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        //Tilemap
        object[] parametersTile = new object[]{tileLocation.x, tileLocation.y, null};
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SetTile, parametersTile, ExitGames.Client.Photon.SendOptions.SendReliable);

        //Particle
        object[] parametersParticle = new object[]{tileLocation.x, tileLocation.y, "BrickBreak", new Vector3(particleColor.r, particleColor.g, particleColor.b)};
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SpawnParticle, parametersParticle, ExitGames.Client.Photon.SendOptions.SendUnreliable);
        
        if (interacter is MonoBehaviourPun) {
            ((MonoBehaviourPun) interacter).photonView.RPC("PlaySound", RpcTarget.All, "player/brick_break");
        }
    }
    public void BumpWithAnimation(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        Bump(interacter, direction, worldLocation);
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        //Bump
        object[] parametersBump = new object[]{tileLocation.x, tileLocation.y, direction == InteractionDirection.Down, "SpecialTiles/" + this.name, BlockBump.SpawnResult.Nothing};
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.BumpTile, parametersBump, ExitGames.Client.Photon.SendOptions.SendReliable);
    }
    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        //Breaking block check.
        return BreakBlockCheck(interacter, direction, worldLocation);
    }
}
