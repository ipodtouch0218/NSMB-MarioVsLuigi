using UnityEngine;
using TMPro;
using NSMB.Extensions;

namespace NSMB.UI.Game {
    [RequireComponent(typeof(TMP_Text))]
    public class SlantText : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private float slopeAmount = 0, firstChar = 0, secondChar = 0;

        //---Private Variables
        private TMP_Text text;
        private TMP_SubMeshUI subtext;

        public void Awake() {
            this.SetIfNull(ref text);
        }

        public void OnEnable() {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(MoveVerts);
        }

        public void OnDisable() {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(MoveVerts);
        }

        public void MoveVerts(Object obj) {
            if (!text || obj != text) {
                return;
            }

            this.SetIfNull(ref subtext, UnityExtensions.GetComponentType.Children);
            if (!subtext) {
                return;
            }

            TMP_TextInfo info = text.textInfo;
            Mesh mesh = subtext.mesh;
            Vector3[] verts = mesh.vertices;

            Vector3 adjustment = slopeAmount < 0 ? -slopeAmount * text.textInfo.characterCount * Vector3.up : Vector3.zero;
            for (int i = 0; i < info.characterCount; i++) {
                int index = info.characterInfo[i].vertexIndex;
                Vector3 offset = adjustment;

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
}
