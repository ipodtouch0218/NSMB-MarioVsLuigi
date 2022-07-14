using NSMB.Utils;
using UnityEngine;
using UnityEngine.UI;

public class LoadingReadyIcon : MonoBehaviour {
    public void Start() {
        GetComponent<Image>().sprite = Utils.GetCharacterData().readySprite;
    }
}
