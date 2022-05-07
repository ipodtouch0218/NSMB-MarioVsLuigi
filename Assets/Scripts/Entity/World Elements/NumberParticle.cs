using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NumberParticle : MonoBehaviour {

    [SerializeField] Sprite[] sprites;
    SpriteRenderer spriteRenderer;

    public void SetSprite(int number) {
        if (!spriteRenderer)
            spriteRenderer = GetComponent<SpriteRenderer>();

        spriteRenderer.sprite = sprites[number];
    }

}