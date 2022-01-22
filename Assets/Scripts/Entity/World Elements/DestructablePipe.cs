using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[ExecuteInEditMode]
public class DestructablePipe : MonoBehaviourPun {
    public float originalSize = 1, currentSize;
    public Sprite normalSprite, damagedSprite;
    private BoxCollider2D pipeCollider;
    private SpriteRenderer spriteRenderer;
    void Start() {
        currentSize = originalSize;
        spriteRenderer = GetComponent<SpriteRenderer>();
        pipeCollider = GetComponent<BoxCollider2D>();
    }
    void Update() {
        if (!Application.isPlaying)
            currentSize = originalSize;
        pipeCollider.size = spriteRenderer.size = new Vector2(2, currentSize);
        pipeCollider.offset = new Vector2(0, currentSize / 2f);
    }


    [PunRPC]
    public void DamagePipe(Vector2 damagePosition, Vector2 impulseNormal) {
        if (impulseNormal == Vector2.up || impulseNormal == Vector2.down) {
            //stomped on from above/below. shrink down by 1 tile
            currentSize = Mathf.Max(1, currentSize - 1);
            return; 
        }
        //hit from sides, break off if necessary.
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(damagePosition);
        Debug.Log("TODO: DamagePipe() in DestructablePipe");
    }
    public void ResetPipe() {
        spriteRenderer.sprite = normalSprite;
        currentSize = originalSize;
    }
}