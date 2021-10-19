using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MarioLoader : MonoBehaviour {
    
    public Sprite small, large;
    public int scale = 0, previousScale;
    public float scaleTimer, blinkSpeed = 0.5f;
    private Image image;
    void Start() {
        image = GetComponent<Image>();
    }

    void Update() {
        int scaleDisplay = scale;
        
        if ((scaleTimer += Time.deltaTime) < 0.5f) {
            if (scaleTimer % blinkSpeed < blinkSpeed / 2f)
                scaleDisplay = previousScale;
        } else {
            previousScale = scale;
        }

        if (scaleDisplay == 0) {
            transform.localScale = Vector3.one;
            image.sprite = small;
        } else if (scaleDisplay == 1) {
            transform.localScale = Vector3.one;
            image.sprite = large;
        } else {
            transform.localScale = Vector3.one * 2;
            image.sprite = large;
        }
    }
}
