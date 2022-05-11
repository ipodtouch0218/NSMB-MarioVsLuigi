using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class VersionNamer : MonoBehaviour {
    void Start() {
        GetComponent<TMP_Text>().text = "v" + Application.version;
    }
}
