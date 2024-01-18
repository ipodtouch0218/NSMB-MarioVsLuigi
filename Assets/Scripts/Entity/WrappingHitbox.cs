using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Game;

public class WrappingHitbox : NetworkBehaviour {

    //---Components
    [SerializeField] private EntityMover body;

    //---Private Variables
    private BoxCollider2D[] ourColliders, childColliders;
    private Vector2 offset;

    public void OnValidate() {
        this.SetIfNull(ref body, UnityExtensions.GetComponentType.Parent);
    }

    public void Awake() {
        ourColliders = GetComponents<BoxCollider2D>();

        // Null propagation is ok w/ GameManager.Instance
        if (!GameManager.Instance || !GameManager.Instance.loopingLevel) {
            enabled = false;
            return;
        }

        childColliders = new BoxCollider2D[ourColliders.Length];
        for (int i = 0; i < ourColliders.Length; i++) {
            childColliders[i] = gameObject.AddComponent<BoxCollider2D>();
        }

        offset = new(GameManager.Instance.LevelWidth, 0);
    }

    public override void FixedUpdateNetwork() {
        for (int i = 0; i < ourColliders.Length; i++) {
            UpdateChildColliders(i);
        }
    }

    private void UpdateChildColliders(int index) {
        BoxCollider2D ourCollider = ourColliders[index];
        BoxCollider2D childCollider = childColliders[index];

        childCollider.autoTiling = ourCollider.autoTiling;
        childCollider.edgeRadius = ourCollider.edgeRadius;
        childCollider.enabled = ourCollider.enabled;
        childCollider.isTrigger = ourCollider.isTrigger;
        childCollider.offset = ourCollider.offset + (((body.Position.x < GameManager.Instance.LevelMiddleX) ? offset : -offset) / body.transform.lossyScale);
        childCollider.sharedMaterial = ourCollider.sharedMaterial;
        childCollider.size = ourCollider.size;
        childCollider.usedByComposite = ourCollider.usedByComposite;
        childCollider.usedByEffector = ourCollider.usedByEffector;
    }
}
