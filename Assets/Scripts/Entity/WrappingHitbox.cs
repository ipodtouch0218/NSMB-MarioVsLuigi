using UnityEngine;

public class WrappingHitbox : MonoBehaviour {

    private Rigidbody2D body;
    private BoxCollider2D[] ourColliders, childColliders;
    private Vector2 offset;
    private float levelMiddle, levelWidth;

    public void Awake() {
        body = GetComponent<Rigidbody2D>();
        if (!body)
            body = GetComponentInParent<Rigidbody2D>();
        ourColliders = GetComponents<BoxCollider2D>();

        // null propagation is ok w/ GameManager.Instance
        if (!(GameManager.Instance?.loopingLevel ?? false)) {
            enabled = false;
            return;
        }

        childColliders = new BoxCollider2D[ourColliders.Length];
        for (int i = 0; i < ourColliders.Length; i++)
            childColliders[i] = gameObject.AddComponent<BoxCollider2D>();
        levelWidth = GameManager.Instance.levelWidthTile / 2f;
        levelMiddle = GameManager.Instance.GetLevelMinX() + levelWidth / 2f;
        offset = new(levelWidth, 0);

        LateUpdate();
    }

    public void LateUpdate() {
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
        childCollider.offset = ourCollider.offset + (((body.position.x < levelMiddle) ? offset : -offset) / body.transform.lossyScale);
        childCollider.sharedMaterial = ourCollider.sharedMaterial;
        childCollider.size = ourCollider.size;
        childCollider.usedByComposite = ourCollider.usedByComposite;
        childCollider.usedByEffector = ourCollider.usedByComposite;
    }
}
