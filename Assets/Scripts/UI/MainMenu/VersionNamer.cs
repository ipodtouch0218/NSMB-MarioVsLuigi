using UnityEngine;
using TMPro;

public class VersionNamer : MonoBehaviour {

    public void Start() {
        GetComponent<TMP_Text>().text = "v" + Application.version;
    }
}
