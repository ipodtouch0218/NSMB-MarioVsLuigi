using UnityEngine;
using UnityEngine.Tilemaps;

using NSMB.Utils;

[CreateAssetMenu(fileName = "BreakableBulletBillLauncher", menuName = "ScriptableObjects/Tiles/BreakableBulletBillLauncher", order = 5)]
public class BreakableBulletBillLauncher : InteractableTile {
    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (!(interacter is PlayerController))
            return false;

        PlayerController player = (PlayerController)interacter;
        if (player.state != Enums.PowerupState.MegaMushroom)
            return false;
        if (direction == InteractionDirection.Down || direction == InteractionDirection.Up)
            return false;

        Vector3Int ourLocation = Utils.WorldToTilemapPosition(worldLocation);
        int height = GetLauncherHeight(ourLocation);
        Vector3Int origin = GetLauncherOrigin(ourLocation);

        string[] tiles = new string[height];

        for (int i = 0; i < tiles.Length; i++)
            //photon doesn't like serializing nulls
            tiles[i] = "";

        object[] parametersParticle = new object[] { (Vector2) worldLocation, direction == InteractionDirection.Right, false, new Vector2(1, height), "DestructableBulletBillLauncher" };
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SpawnResizableParticle, parametersParticle, ExitGames.Client.Photon.SendOptions.SendUnreliable);

        BulkModifyTilemap(origin, new Vector2Int(1, height), tiles);
        return true;
    }

    private Vector3Int GetLauncherOrigin(Vector3Int ourLocation) {
        Tilemap tilemap = GameManager.Instance.tilemap;
        Vector3Int searchDirection = Vector3Int.down;
        Vector3Int searchVector = Vector3Int.down;
        while (tilemap.GetTile<BreakableBulletBillLauncher>(ourLocation + searchVector))
            searchVector += searchDirection;
        return ourLocation + searchVector - searchDirection;
    }

    private int GetLauncherHeight(Vector3Int ourLocation) {
        int height = 1;
        Tilemap tilemap = GameManager.Instance.tilemap;
        Vector3Int searchVector = Vector3Int.up;
        while (tilemap.GetTile<BreakableBulletBillLauncher>(ourLocation + searchVector)) {
            height++;
            searchVector += Vector3Int.up;
        }
        searchVector = Vector3Int.down;
        while (tilemap.GetTile<BreakableBulletBillLauncher>(ourLocation + searchVector)) {
            height++;
            searchVector += Vector3Int.down;
        }
        return height;
    }

    private void BulkModifyTilemap(Vector3Int tileOrigin, Vector2Int tileDimensions, string[] tilenames) {
        object[] parametersTile = new object[] { tileOrigin.x, tileOrigin.y, tileDimensions.x, tileDimensions.y, tilenames };
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SetTileBatch, parametersTile, ExitGames.Client.Photon.SendOptions.SendReliable);
    }
}
