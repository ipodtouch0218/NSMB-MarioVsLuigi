using static IInteractableTile;
using Quantum;
using UnityEngine;

public unsafe class PowerupTile : PowerupTileBase {

    //---Serialized Variables
    [SerializeField] private PowerupAsset smallPowerup, largePowerup;

    public override PowerupAsset GetPowerupAsset(Frame f, EntityRef marioEntity, MarioPlayer* mario) {
        return mario->CurrentPowerupState < PowerupState.Mushroom ? smallPowerup : largePowerup;
    }
}
