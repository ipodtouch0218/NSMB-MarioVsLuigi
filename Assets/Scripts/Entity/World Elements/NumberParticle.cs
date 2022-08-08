using System.Collections;

using UnityEngine;
using TMPro;

public class NumberParticle : MonoBehaviour {

    public TMP_Text text;
    public Color color;

    public void Start() {
        StartCoroutine(ChangeColor());
    }

    IEnumerator ChangeColor() {
        yield return new WaitForSeconds(0.001f);

        MeshRenderer mr = GetComponentsInChildren<MeshRenderer>()[1];
        MaterialPropertyBlock mpb = new();
        mpb.SetColor("_Color", color);
        mr.SetPropertyBlock(mpb);
    }

    public void Kill() {
        Destroy(transform.parent.gameObject);
    }
}