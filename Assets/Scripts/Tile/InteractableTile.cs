using UnityEngine;
using UnityEngine.Tilemaps;
using Photon.Pun;

public abstract class InteractableTile : AnimatedTile {

    private static Vector3 bumpOffset = new(0.25f, 0.5f, 0), bumpSize = new(0.45f, 0.1f, 0);

    public abstract bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation);
    public static void Bump(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {

        if (direction == InteractionDirection.Down)
            return;

        //check for entities above to bump
        foreach (Collider2D collider in Physics2D.OverlapBoxAll(worldLocation + bumpOffset, bumpSize, 0f)) {
            GameObject obj = collider.gameObject;
            if (obj == interacter.gameObject)
                continue;

            if (obj.GetComponent<MovingPowerup>() is MovingPowerup powerup) {
                powerup.photonView.RPC("Bump", RpcTarget.All);
                continue;
            }

            switch (obj.tag) {
            case "Player": {
                PlayerController player = obj.GetComponent<PlayerController>();

                /*
                // fall on ass when bumped from below
                if (player.state == Enums.PowerupState.MegaMushroom)
                    player.photonView.RPC("KnockbackMegaMushroom", RpcTarget.All);
                */

                player.photonView.RPC("Knockback", RpcTarget.All, obj.transform.position.x < interacter.transform.position.x, 1, false, (interacter as MonoBehaviourPun)?.photonView.ViewID ?? -1);
                continue;
            }
            case "koopa": {
                if (!obj.GetPhotonView())
                    continue;
                obj.GetPhotonView().RPC("Bump", RpcTarget.All);
                continue;
            }
            case "goomba": {
                if (!obj.GetPhotonView())
                    continue;
                obj.GetPhotonView().RPC("SpecialKill", RpcTarget.All, obj.transform.position.x < worldLocation.x, false, 0);
                continue;
            }
            case "loosecoin":
            case "coin": {
                if (!obj)
                    continue;

                if (interacter is PlayerController pl)
                    pl.photonView.RPC("CollectCoin", RpcTarget.All, obj.GetComponentInParent<PhotonView>().ViewID, obj.transform.position);
                continue;
            }
            case "MainStar":
            case "bigstar":
                continue;
            case "frozencube":
                obj.GetPhotonView().RPC("Kill", RpcTarget.All);
                continue;
            default: {
                if (obj.layer != LayerMask.NameToLayer("Entity"))
                    continue;
                Rigidbody2D body = obj.GetComponentInParent<Rigidbody2D>();
                if (!body) {
                    body = obj.GetComponent<Rigidbody2D>();
                    if (!body)
                        continue;
                }
                body.velocity = new Vector2(body.velocity.x, 3f);
                continue;
            }
            }
        }
    }
    public enum InteractionDirection {
        Up, Down, Left, Right
    }
}