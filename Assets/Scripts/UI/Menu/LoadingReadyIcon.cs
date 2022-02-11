using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadingReadyIcon : MonoBehaviour {
    void Start() {
        GetComponent<Image>().sprite = Utils.GetCharacterData().readySprite;
    }
}
