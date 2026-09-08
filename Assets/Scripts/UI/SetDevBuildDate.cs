using TMPro;
using UnityEngine;

namespace NSMB.UI {
    public class SetDevBuildDate : MonoBehaviour {
        private void Start() {
            TMP_Text text = GetComponent<TMP_Text>();
            text.text = $"Development Build ({Application.version}) [{BuildInfo.BUILD_TIME}]";
        }
    }
}