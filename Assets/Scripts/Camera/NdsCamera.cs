using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NdsCamera : MonoBehaviour {
    private RawImage image;
    void Start() {
        image = GetComponent<RawImage>();
    }
    void LateUpdate() {
        SizeToParent(image);
    }
    public Vector2 SizeToParent(RawImage image, float padding = 0) {
        var parent = image.transform.parent.GetComponent<RectTransform>();
        var imageTransform = image.GetComponent<RectTransform>();
        if (!parent) 
            return imageTransform.sizeDelta;
        padding = 1 - padding;
        float w, h;
        float ratio = image.texture.width / (float)image.texture.height;
        var bounds = new Rect(0, 0, parent.rect.width, parent.rect.height);
        if (Mathf.RoundToInt(imageTransform.eulerAngles.z) % 180 == 90)
              //Invert the bounds if the image is rotated
              bounds.size = new Vector2(bounds.height, bounds.width);
        
        //Size by height first
        h = bounds.height * padding;
        w = h * ratio;
        if (w > bounds.width * padding) { //If it doesn't fit, fallback to width;
            w = bounds.width * padding;
            h = w / ratio;
        }
        imageTransform.sizeDelta = new Vector2(w, h);
        return imageTransform.sizeDelta;
    }

}