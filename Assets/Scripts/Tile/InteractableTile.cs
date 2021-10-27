using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

using Photon.Pun;

public abstract class InteractableTile : AnimatedTile {

    protected Vector3 bumpOffset = new Vector3(0, 0.5f, 0), bumpSize = new Vector3(0.65f, 0.1f, 0);
    public abstract bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation);
    protected void Bump(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        if (direction != InteractionDirection.Down) {
            //check for entities above to bump
            foreach (Collider2D collider in Physics2D.OverlapBoxAll(worldLocation + bumpOffset, bumpSize, 0f)) {
                GameObject obj = collider.gameObject;
                if (obj == interacter.gameObject) continue;
                switch (obj.tag) {
                case "Player": {
                    PlayerController player = obj.GetComponent<PlayerController>();
                    player.photonView.RPC("Knockback", RpcTarget.All, obj.transform.position.x > worldLocation.x, 1);
                    break;
                }
                case "koopa":
                case "goomba": {
                    if (!obj.GetPhotonView())
                        break;
                    if (obj.GetComponent<KillableEntity>().dead)
                        break;
                    obj.GetPhotonView().RPC("SpecialKill", RpcTarget.All, obj.transform.position.x < worldLocation.x, false);
                    break;
                }
                case "coin": {
                    if (interacter && interacter is PlayerController) {
                        ((PlayerController) interacter).photonView.RPC("CollectCoin", RpcTarget.All, obj.GetComponentInParent<PhotonView>().ViewID, obj.transform.position.x, obj.transform.position.y);
                    }
                    break;
                }
                case "MainStar":
                case "bigstar": break;
                default: {
                    if (obj.layer != LayerMask.NameToLayer("Entity")) break;
                    Rigidbody2D body = obj.GetComponentInParent<Rigidbody2D>();
                    if (!body) {
                        body = obj.GetComponent<Rigidbody2D>();
                        if (!body) {
                            break;
                        }
                    }
                    body.velocity = new Vector2(body.velocity.x, 7f);
                    break;
                }
                }
            }
        }
    }
    public enum InteractionDirection {
        Up, Down, Left, Right
    }
}