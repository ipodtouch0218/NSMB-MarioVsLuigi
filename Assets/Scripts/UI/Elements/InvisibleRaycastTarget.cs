using UnityEngine.UI;

namespace NSMB.UI.Elements {
    public class InvisibleRaycastTarget : Graphic {
        public override void SetMaterialDirty() { return; }
        public override void SetVerticesDirty() { return; }

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            return;
        }
    }
}
