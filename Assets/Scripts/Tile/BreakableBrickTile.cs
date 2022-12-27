using UnityEngine;

using NSMB.Utils;
using Fusion;

[CreateAssetMenu(fileName = "BreakableBrickTile", menuName = "ScriptableObjects/Tiles/BreakableBrickTile")]
public class BreakableBrickTile : InteractableTile {

    [ColorUsage(false), SerializeField] protected Color particleColor;
    [SerializeField] public bool breakableBySmallMario = false, breakableByLargeMario = true, breakableByGiantMario = true, breakableByShells = true, breakableByBombs = true, bumpIfNotBroken = true, bumpIfBroken = true;

    protected bool BreakBlockCheck(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
        bool doBump = false, doBreak = false, giantBreak = false;
        if (interacter is PlayerController pl) {
            if (pl.State <= Enums.PowerupState.MiniMushroom && !pl.IsDrilling) {
                doBreak = breakableBySmallMario;
                doBump = true;
            } else if (pl.State == Enums.PowerupState.MegaMushroom) {
                doBreak = breakableByGiantMario;
                giantBreak = true;
                doBump = false;
            } else if (pl.State >= Enums.PowerupState.Mushroom || pl.IsDrilling) {
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

        if (doBump && doBreak && bumpIfBroken)
            Bump(interacter, direction, worldLocation);
        if (doBump && !doBreak && bumpIfNotBroken)
            BumpWithAnimation(interacter, direction, worldLocation);
        if (doBreak)
            Break(interacter, worldLocation, giantBreak ? Enums.Sounds.Powerup_MegaMushroom_Break_Block : Enums.Sounds.World_Block_Break);

        return doBreak;
    }

    public void Break(BasicEntity interacter, Vector3 worldLocation, Enums.Sounds sound) {
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        //Tilemap
        GameManager.Instance.tilemap.SetTile(tileLocation, null);

        //Particle
        GameManager.Instance.particleManager.Play(Enums.Particle.Entity_BrickBreak, Utils.TilemapToWorldPosition(tileLocation) + Vector3.one * 0.25f, particleColor);

        if (interacter)
            interacter.PlaySound(sound);
    }

    public void BumpWithAnimation(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
        Bump(interacter, direction, worldLocation);
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        //Bump
        if (GameManager.Instance.Object.HasStateAuthority) {
            GameManager.Instance.rpcs.Rpc_BumpBlock((short) tileLocation.x, (short) tileLocation.y, "",
                "SpecialTiles/" + name, direction == InteractionDirection.Down, Vector2.zero, false, NetworkPrefabRef.Empty);
        }
    }

    public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
        //Breaking block check.
        return BreakBlockCheck(interacter, direction, worldLocation);
    }
}
