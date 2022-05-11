using System.Collections;
using System.Collections.Generic;
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
            switch (obj.tag) {
            case "Player": {
                PlayerController player = obj.GetComponent<PlayerController>();
                if (player.state == Enums.PowerupState.MegaMushroom)
                    return;

                player.photonView.RPC("Knockback", RpcTarget.All, obj.transform.position.x < interacter.transform.position.x, 1, false, null);
                player.photonView.RPC("SpawnParticle", RpcTarget.All, "Prefabs/Particle/PlayerBounce", player.body.position);
                continue;
            }
            case "koopa": {
                if (!obj.GetPhotonView() || obj.GetComponent<KillableEntity>().dead)
                    continue;
                obj.GetPhotonView().RPC("Bump", RpcTarget.All);
                continue;
            }
            case "goomba": {
                if (!obj.GetPhotonView() || obj.GetComponent<KillableEntity>().dead)
                    continue;
                obj.GetPhotonView().RPC("SpecialKill", RpcTarget.All, obj.transform.position.x < worldLocation.x, false);
                continue;
            }
            case "coin": {
                if (interacter is PlayerController pl)
                    pl.photonView.RPC("CollectCoin", RpcTarget.All, obj.GetComponentInParent<PhotonView>().ViewID, obj.transform.position);
                continue;
            }
            case "MainStar":
            case "bigstar":
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