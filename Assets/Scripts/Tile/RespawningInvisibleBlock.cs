using UnityEngine;
using Photon.Pun;
using NSMB.Utils;

public class RespawningInvisibleBlock : MonoBehaviour {

    public void OnTriggerEnter2D(Collider2D collision) {
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(transform.position);

        if (!collision.CompareTag("Player") || Utils.GetTileAtTileLocation(tileLocation) != null)
            return;

        if (!collision.gameObject.GetPhotonView().IsMine)
            return;

        Rigidbody2D body = collision.attachedRigidbody;
        if (body.velocity.y <= 0)
            return;

        BoxCollider2D bc = collision as BoxCollider2D;
        if (bc == null)
            return;
        if (body.position.y + (bc.size.y * body.transform.lossyScale.y) - (body.velocity.y * Time.fixedDeltaTime) > transform.position.y)
            return;

        DoBump(tileLocation, collision.gameObject.GetPhotonView());
        collision.attachedRigidbody.velocity = new(body.velocity.x, 0);
    }

    public void DoBump(Vector3Int tileLocation, PhotonView player) {
        player.RPC("CollectCoin", RpcTarget.All, -1, Utils.TilemapToWorldPosition(tileLocation) + Vector3.one / 4f);

        object[] parametersBump = new object[] { tileLocation.x, tileLocation.y, false, "SpecialTiles/EmptyYellowQuestion", "Coin" };
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SetThenBumpTile, parametersBump, ExitGames.Client.Photon.SendOptions.SendReliable);
    }
}