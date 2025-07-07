using UnityEngine;
using TMPro;

public class SetDevBuildDate : MonoBehaviour {
    private void Start() {
        TMP_Text text = GetComponent<TMP_Text>();
        text.text = $"Development Build (v1.8.0.0-beta) [{BuildInfo.BUILD_TIME}]";
    }
}
