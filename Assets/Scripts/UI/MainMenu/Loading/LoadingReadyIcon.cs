using UnityEngine;
using UnityEngine.UI;

namespace NSMB.Loading {
    public class LoadingReadyIcon : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private Image image;

        public void OnEnable() {
            /* TODO
            CharacterData character = NetworkHandler.Runner.GetLocalPlayerData().GetCharacterData();
            image.sprite = character.readySprite;
            */
        }
    }
}
