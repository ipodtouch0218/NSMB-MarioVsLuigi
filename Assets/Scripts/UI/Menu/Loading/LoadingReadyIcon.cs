using UnityEngine;
using UnityEngine.UI;

using NSMB.Extensions;

namespace NSMB.Loading {
    public class LoadingReadyIcon : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private Image image;

        public void OnEnable() {
            CharacterData character = NetworkHandler.Instance.runner.GetLocalPlayerData().GetCharacterData();
            image.sprite = character.readySprite;
        }
    }
}
