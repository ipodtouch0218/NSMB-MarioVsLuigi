using UnityEngine;
using UnityEngine.UI;

using NSMB.Extensions;

public class LoadingReadyIcon : MonoBehaviour {
    public void Start() {
        CharacterData character = NetworkHandler.Instance.runner.GetLocalPlayerData().GetCharacterData();
        GetComponent<Image>().sprite = character.readySprite;
    }
}
