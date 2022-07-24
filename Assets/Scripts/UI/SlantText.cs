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

    private bool set;

    public void OnValidate() {
        set = false;
        Update();
    }

    public void Update() {
        if (set)
            return;

        if (cr == null || mat == null) {
            try {
                cr = GetComponentsInChildren<CanvasRenderer>()[1];
                cr.SetMaterial(mat = new(cr.GetMaterial()), 0);
            } catch {
                // TMPro didn't generate submesh yet. oh well.
                return;
            }
        }
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_VerticalOffsetX", slopeAmount);
        mat.SetFloat("_FirstCharOffset", firstChar);
        mat.SetFloat("_SecondCharOffset", secondChar);

        if (slopeAmount < 0) {
            text = GetComponent<TMP_Text>();
            int chars = text.GetTextInfo(text.text).characterCount;
            child = transform.GetChild(0).GetComponent<RectTransform>();
            child.offsetMax = new(0, (chars - 1) * 4);
            child.offsetMin = new(0, (chars - 1) * 4);
        }

        set = true;
    }
}
