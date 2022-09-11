using UnityEngine;
using TMPro;

public class ForceDisableWrapping : MonoBehaviour {

    private TMP_Text text;

    public void Start() {
        text = GetComponent<TMP_Text>();
        text.enableWordWrapping = false;
    }
}