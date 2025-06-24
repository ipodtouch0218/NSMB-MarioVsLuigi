using UnityEngine;
using TMPro;

namespace NSMB.UI.Elements {
    [RequireComponent(typeof(TMP_Text))]
    public class ForceDisableWrapping : MonoBehaviour {
        public void Start() {
            GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.NoWrap;
        }
    }
}