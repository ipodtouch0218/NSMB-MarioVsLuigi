using UnityEngine;
using TMPro;

public class NumberParticle : MonoBehaviour {

    public TMP_Text text;
    public Vector3 colorOffset;
    public Color overlay;

    private MaterialPropertyBlock mpb;
    private MeshRenderer mr;
    private bool ignoreKill;

    public void Update() {
        mpb.SetVector("_ColorOffset", colorOffset);
        mpb.SetColor("_Overlay", overlay);
        mr.SetPropertyBlock(mpb);
    }

    public void ApplyColorAndText(string newText, Color color, bool final) {
        if (!text)
            text = GetComponentInChildren<TMP_Text>();

        text.text = newText;
        text.ForceMeshUpdate();
        mr = GetComponentsInChildren<MeshRenderer>()[1];
        mpb = new();
        mpb.SetColor("_Color", color);
        mr.SetPropertyBlock(mpb);

        Animator anim = GetComponent<Animator>();
        anim.SetBool("final", final);

        enabled = final;
        ignoreKill = final;
    }

    public void Kill() {
        if (ignoreKill) {
            ignoreKill = false;
            return;
        }

        Destroy(gameObject.transform.parent.gameObject);
    }
}
