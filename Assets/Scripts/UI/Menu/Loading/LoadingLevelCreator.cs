using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LoadingLevelCreator : MonoBehaviour {

    private TMP_Text text;

    public void Start() {
        text = GetComponent<TMP_Text>();
    }

    public void Update() {
        if (!GameManager.Instance || GameManager.Instance.levelDesigner == "")
            return;

        text.text = $"Level designed by: {GameManager.Instance.levelDesigner}"; 
    }

}