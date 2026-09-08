using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class CharacterButton : MonoBehaviour, ISelectHandler {

        //---Public Variables
        [HideInInspector] public CharacterAsset character;
        public Button button;

        //---Serialized Variables
        [SerializeField] private TMP_Text colorNameString;

        public void Initialize(CharacterAsset character) {
            this.character = character;
            ((Image) button.targetGraphic).sprite = character.SelectionSprite;
        }

        public void OnSelect(BaseEventData eventData) {
            UpdateLabel();
        }

        public void OnPress() {
            UpdateLabel();
        }

        private void UpdateLabel() {
            colorNameString.text = GlobalController.Instance.translationManager.GetTranslation(character.TranslationString);
        }
    }
}
