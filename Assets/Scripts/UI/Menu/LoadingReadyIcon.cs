using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class LoadingReadyIcon : MonoBehaviour {
    void Start() {
        GetComponent<Image>().sprite = Utils.GetCharacterData().readySprite;
    }
}
