using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WrappingHitbox : MonoBehaviour {
    
    private Rigidbody2D body;
    private BoxCollider2D[] ourColliders, childColliders;
    private float levelMiddle, levelWidth;
    private Vector2 offset = Vector2.zero;
    void Awake() {
        body = GetComponent<Rigidbody2D>();
        if (!body)
            body = GetComponentInParent<Rigidbody2D>();
        ourColliders = GetComponents<BoxCollider2D>();
        LateUpdate();
    }
    public void LateUpdate() {
        if (!GameManager.Instance) 
            return;
        if (!GameManager.Instance.loopingLevel) {
            enabled = false;
            return;
        }
        
        if (offset == Vector2.zero) {
            childColliders = new BoxCollider2D[ourColliders.Length];
            for (int i = 0; i < ourColliders.Length; i++)
                childColliders[i] = gameObject.AddComponent<BoxCollider2D>();
            levelWidth = GameManager.Instance.levelWidthTile/2f;
            levelMiddle = GameManager.Instance.GetLevelMinX() + levelWidth/2f;
            offset = new Vector2(levelWidth, 0);
        }

        for (int i = 0; i < ourColliders.Length; i++)
            UpdateChildColliders(i);
    }
    
    void UpdateChildColliders(int index) {
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
