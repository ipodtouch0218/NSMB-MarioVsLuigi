using UnityEngine;
using TMPro;

public class ForceDisableWrapping : MonoBehaviour {

    public void Start() {
        GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.NoWrap;
    }
}