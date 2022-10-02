using UnityEngine;
using TMPro;

public class NumberParticle : MonoBehaviour {

    public TMP_Text text;

    public void ApplyColorAndText(string newText, Color color) {
        if (!text)
            text = GetComponentInChildren<TMP_Text>();

        text.text = newText;
        text.ForceMeshUpdate();
        MeshRenderer mr = GetComponentsInChildren<MeshRenderer>()[1];
        MaterialPropertyBlock mpb = new();
        mpb.SetColor("_Color", color);
        mr.SetPropertyBlock(mpb);
    }

    public void Kill() {
        Destroy(gameObject.transform.parent.gameObject);
    }
}