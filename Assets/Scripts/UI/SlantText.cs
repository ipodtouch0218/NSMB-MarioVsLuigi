using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class SlantText : MonoBehaviour {

    [SerializeField] private float slopeAmount = 0, firstChar = 0, secondChar = 0;

    private TMP_Text text;
    private TMP_SubMeshUI subtext;
    private Mesh mesh;
    private Vector3[] verts;
    private bool set;


    public void OnValidate() {
        set = false;
        LateUpdate();
    }

    public void LateUpdate() {
        if (!text || !subtext) {
            text = GetComponent<TMP_Text>();
            subtext = GetComponentInChildren<TMP_SubMeshUI>();

            if (subtext && !set) {
                subtext.material = new(subtext.material);
                set = true;
            }
            return;
        }

        text.ForceMeshUpdate();
        mesh = subtext.mesh;
        verts = mesh.vertices;

        Vector3 adjust = slopeAmount < 0 ? -slopeAmount * text.textInfo.characterCount * Vector3.up : Vector3.zero;
        for (int i = 0; i < text.textInfo.characterCount; i++) {
            TMP_CharacterInfo c = text.textInfo.characterInfo[i];

            int index = c.vertexIndex;
            Vector3 offset = adjust;

            if (i == 0) {
                offset.y += firstChar;
            } else if (i == 1) {
                offset.y += secondChar;
            }
            offset.y += i * slopeAmount;

            verts[index] += offset;
            verts[index + 1] += offset;
            verts[index + 2] += offset;
            verts[index + 3] += offset;
        }

        mesh.vertices = verts;
        subtext.canvasRenderer.SetMesh(mesh);
    }
}
