using NSMB.Utilities.Extensions;
using UnityEngine;

namespace NSMB.Utilities.Components {
    public class LockScale : MonoBehaviour {
        public void LateUpdate() {
            Vector3 parentScale = transform.parent.localScale;
            if (parentScale.x == 0) {
                parentScale.x = 1;
            }
            if (parentScale.y == 0) {
                parentScale.y = 1;
            }
            if (parentScale.z == 0) {
                parentScale.z = 1;
            }

            transform.localScale = Vector3.one.Divide(parentScale);
        }
    }
}
