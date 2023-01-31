using UnityEngine;

using Fusion;
using NSMB.Utils;

public class RespawningInvisibleBlock : NetworkBehaviour, IPlayerInteractable {

    private static readonly Vector3 BlockOffset = new(0.25f, 0.25f);
    private static readonly Color GizmoColor = new(1, 1, 1, 0.5f);

    //---Networked Variables
    [Networked] private TickTimer BumpTimer { get; set; }

    //---Serialized Variables
    [SerializeField] private string bumpTile = "SpecialTiles/YellowQuestion";
    [SerializeField] private string resultTile = "SpecialTiles/EmptyYellowQuestion";

    public void InteractWithPlayer(PlayerController player) {
        if (!BumpTimer.ExpiredOrNotRunning(Runner))
            return;

        //no block can be at our location
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(transform.position);
        if (Utils.GetTileAtTileLocation(tileLocation) != null)
            return;

        //player has to be moving upwards
        if (player.body.velocity.y < 0.1f)
            return;

        //player has to bump us from below
        if (player.body.position.y + (player.MainHitbox.size.y * player.body.transform.lossyScale.y) - (player.body.velocity.y * Runner.DeltaTime) > transform.position.y)
            return;

        BumpTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
        DoBump(tileLocation, player);

        //stop player velocity
        player.body.velocity = new(player.body.velocity.x, 0);
    }

    public void DoBump(Vector3Int tileLocation, PlayerController player) {
        Vector3 location = Utils.TilemapToWorldPosition(tileLocation) + BlockOffset;
        Coin.GivePlayerCoin(player, location);

        if (GameManager.Instance.Object.HasStateAuthority) {
            GameManager.Instance.rpcs.BumpBlock((short) tileLocation.x, (short) tileLocation.y, bumpTile,
                resultTile, false, Vector2.zero, true, NetworkPrefabRef.Empty);
        }
    }

    public void OnDrawGizmos() {
        Gizmos.DrawIcon(transform.position, "HiddenBlock", true, GizmoColor);
    }
}
