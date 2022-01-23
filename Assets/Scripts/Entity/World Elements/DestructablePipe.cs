using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class DestructablePipe : MonoBehaviourPun {
    public float currentSize;
    private SpriteRenderer spriteRenderer;
    void Start() {
        spriteRenderer = GetComponent<SpriteRenderer>();
        
    }
}