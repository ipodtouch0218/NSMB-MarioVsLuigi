using UnityEngine;

using Fusion;
using NSMB.Game;

public class WrappingHitbox : NetworkBehaviour {

    private EntityMover body;
    private BoxCollider2D[] ourColliders, childColliders;
    private Vector2 offset;

    public void Awake() {
        body = GetComponent<EntityMover>();
        if (!body)
            body = GetComponentInParent<EntityMover>();
        ourColliders = GetComponents<BoxCollider2D>();

        // Null propagation is ok w/ GameManager.Instance
        if (!(GameManager.Instance?.loopingLevel ?? false)) {
            enabled = false;
            return;
        }

        childColliders = new BoxCollider2D[ourColliders.Length];
        for (int i = 0; i < ourColliders.Length; i++)
            childColliders[i] = gameObject.AddComponent<BoxCollider2D>();
        offset = new(GameManager.Instance.LevelWidth, 0);
    }

    public override void FixedUpdateNetwork() {
        for (int i = 0; i < ourColliders.Length; i++)
            UpdateChildColliders(i);
    }

    private void UpdateChildColliders(int index) {
        BoxCollider2D ourCollider = ourColliders[index];
        BoxCollider2D childCollider = childColliders[index];

        childCollider.autoTiling = ourCollider.autoTiling;
        childCollider.edgeRadius = ourCollider.edgeRadius;
        childCollider.enabled = ourCollider.enabled;
        childCollider.isTrigger = ourCollider.isTrigger;
        childCollider.offset = ourCollider.offset + (((body.position.x < GameManager.Instance.LevelMiddleX) ? offset : -offset) / body.transform.lossyScale);
        childCollider.sharedMaterial = ourCollider.sharedMaterial;
        childCollider.size = ourCollider.size;
        childCollider.usedByComposite = ourCollider.usedByComposite;
        childCollider.usedByEffector = ourCollider.usedByComposite;
    }
}
