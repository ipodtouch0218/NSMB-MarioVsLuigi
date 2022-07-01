using UnityEngine;
using Photon.Pun;

/*public class UnkillableEntity : MonoBehaviourPun {

    public bool dead, left = true, collide = true, iceCarryable = true, flying;
    public Rigidbody2D body;
    public BoxCollider2D hitbox;
    protected Animator animator;
    protected SpriteRenderer sRenderer;
    protected AudioSource audioSource;
    protected PhysicsEntity physics;
    public void Start() {
        body = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        sRenderer = GetComponent<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();
    }

    public virtual void FixedUpdate() {
        if (!(photonView?.IsMine ?? true) || !GameManager.Instance || !photonView.IsMine)
            return;

        if (body && !dead && !Frozen && !body.isKinematic && Utils.IsTileSolidAtWorldLocation(body.position + hitbox.offset * transform.lossyScale))
            photonView.RPC("SpecialKill", RpcTarget.All, left, false);
    }

    public void OnTriggerEnter2D(Collider2D collider) {
        UnkillableEntity entity = collider.GetComponentInParent<UnkillableEntity>();
        if (!collide || !photonView.IsMine || !entity || entity.dead)
            return;

        bool goLeft = body.position.x < collider.attachedRigidbody.position.x;
        if (body.position.x == collider.attachedRigidbody.position.x) {
            goLeft = body.position.y > collider.attachedRigidbody.position.y;
        }
        photonView.RPC("SetLeft", RpcTarget.All, goLeft);
    }

    public virtual void InteractWithPlayer(PlayerController player) {
        if (player.Frozen)
            return;
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f && !player.onGround;

        if (attackedFromAbove) {
            player.photonView.RPC("Powerdown", RpcTarget.All, false);
        } else if (player.hitInvincibilityCounter <= 0) {
            player.photonView.RPC("Powerdown", RpcTarget.All, false);
        }
    }

    [PunRPC]
    public void SetLeft(bool left) {
        this.left = left;
        body.velocity = new Vector2(Mathf.Abs(body.velocity.x) * (left ? -1 : 1), body.velocity.y);
    }

    [PunRPC]
    public virtual void Kill() {
    }

    [PunRPC]
    public virtual void SpecialKill(bool right = true, bool groundpound = false) {
    }
}
*/