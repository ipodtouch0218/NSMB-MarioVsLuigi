using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class SlantText : MonoBehaviour {
    [SerializeField] float slopeAmount = 0, firstChar = 0, secondChar = 0;
    private CanvasRenderer cr;
    private TMP_Text text;
    private RectTransform child;
    private Material mat;
    void Update() {
        if (cr == null) {
            try {
                cr = GetComponentsInChildren<CanvasRenderer>()[1];
            } catch {
                return;
            }
        }
        if (mat == null) {
            cr.SetMaterial(mat = new(cr.GetMaterial()), 0);
        }
        mat.SetFloat("_VerticalOffsetX", slopeAmount);
        mat.SetFloat("_FirstCharOffset", firstChar);
        mat.SetFloat("_SecondCharOffset", secondChar);
        if (text == null) {
            text = GetComponent<TMP_Text>();
        }
        if (slopeAmount < 0) {
            int chars = text.GetTextInfo(text.text).characterCount;
            child = transform.GetChild(0).GetComponent<RectTransform>();
            child.offsetMax = new(0, chars * 10);
            child.offsetMin = new(0, chars * 10);
        }
    }
}
