using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class KillableEntity : MonoBehaviourPun {
    public bool dead;
    protected Rigidbody2D body;
    protected BoxCollider2D hitbox;
    protected Animator animator;
    protected new SpriteRenderer renderer;
    protected AudioSource audioSource;
    protected PhysicsEntity physics;

    public void Start() {
        body = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        renderer = GetComponent<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();
    }

    public void Update() {
        if (renderer.enabled) {
            if (!renderer.isVisible) {
                audioSource.volume = 0;
            } else {
                audioSource.volume = 1;
            }
        }
    }

    [PunRPC]
    public virtual void SpecialKill(bool right = true, bool groundpound = false) {
        body.velocity = new Vector2(2.5f * (right ? 1 : -1), 2.5f);
        body.constraints = RigidbodyConstraints2D.None;
        body.angularVelocity = 400f * (right ? 1 : -1);
        body.gravityScale = 1.5f;
        hitbox.enabled = false;
        animator.speed = 0;
        gameObject.layer = LayerMask.NameToLayer("HitsNothing");
        dead = true;
        photonView.RPC("PlaySound", RpcTarget.All, "enemy/shell_kick");
        if (groundpound)
            GameObject.Instantiate(Resources.Load("Prefabs/Particle/EnemySpecialKill"), transform.position + new Vector3(0, 0.5f, -5), Quaternion.identity);
        
        if (photonView.IsMine) {
            PhotonNetwork.InstantiateRoomObject("Prefabs/LooseCoin", transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity);
        }
    } 
    [PunRPC]
    public void PlaySound(string sound) {
        audioSource.PlayOneShot((AudioClip) Resources.Load("Sound/" + sound));
    }
}
