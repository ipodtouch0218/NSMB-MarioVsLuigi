using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[ExecuteInEditMode]
[RequireComponent(typeof(TMP_Text))]
public class SlantText : MonoBehaviour {
    [SerializeField] float slopeAmount = 0;
    private CanvasRenderer cr;
    private TMP_Text text;
    private RectTransform child;
    private bool set = false;
    void Update() {
        if (cr == null) {
            try {
                cr = GetComponentsInChildren<CanvasRenderer>()[1];
            } catch { }
        } else if (!set) {
            cr.SetMaterial(new(cr.GetMaterial()), 0);
            cr.GetMaterial().SetFloat("_VerticalOffsetX", slopeAmount);
            set = true;
        }
        if (text == null) {
            text = GetComponent<TMP_Text>();
        } else {
            int chars = text.GetTextInfo(text.text).characterCount;
            if (slopeAmount < 0) {
                child = transform.GetChild(0).GetComponent<RectTransform>();
                child.offsetMax = new(0, chars * 10);
                child.offsetMin = new(0, chars * 10);
            }
        }
    }
}
