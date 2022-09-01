using UnityEngine;
using Photon.Pun;

using NSMB.Utils;

public class RespawningInvisibleBlock : MonoBehaviour {

    private double bumpTime;

    public void OnTriggerEnter2D(Collider2D collision) {
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(transform.position);

        if (PhotonNetwork.Time - bumpTime < 0)
            return;

        if (Utils.GetTileAtTileLocation(tileLocation) != null)
            return;

        if (collision.gameObject.GetComponent<PlayerController>() is not PlayerController player)
            return;

        if (!player.photonView.IsMine)
            return;

        Rigidbody2D body = collision.attachedRigidbody;
        if (player.previousFrameVelocity.y <= 0)
            return;

        BoxCollider2D bc = collision as BoxCollider2D;
        if (bc == null)
            return;
        if (body.position.y + (bc.size.y * body.transform.lossyScale.y) - (player.previousFrameVelocity.y * Time.fixedDeltaTime) > transform.position.y)
            return;

        DoBump(tileLocation, collision.gameObject.GetPhotonView());
        bumpTime = PhotonNetwork.Time + 0.25d;
        collision.attachedRigidbody.velocity = new(body.velocity.x, 0);
    }

    public void DoBump(Vector3Int tileLocation, PhotonView player) {
        player.RPC(nameof(PlayerController.AttemptCollectCoin), RpcTarget.All, -1, (Vector2) Utils.TilemapToWorldPosition(tileLocation) + Vector2.one / 4f);

        object[] parametersBump = new object[] { tileLocation.x, tileLocation.y, false, "SpecialTiles/EmptyYellowQuestion", "Coin" };
        GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SetThenBumpTile, parametersBump, ExitGames.Client.Photon.SendOptions.SendReliable);
    }

    public void OnDrawGizmos() {
        Gizmos.DrawIcon(transform.position, "HiddenBlock", true, new Color(1, 1, 1, 0.5f));
    }
}