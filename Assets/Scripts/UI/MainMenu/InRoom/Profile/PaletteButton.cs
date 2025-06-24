using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class PaletteButton : MonoBehaviour, ISelectHandler {

        //---Public Variables
        public PaletteSet palette;

        //---Serialized Variables
        [SerializeField] private TMP_Text colorNameString;
        [SerializeField] private Image shirt, overalls;

        public void Instantiate(CharacterAsset player) {
            if (palette == null) {
                if (shirt && overalls) {
                    Destroy(shirt.gameObject);
                    Destroy(overalls.gameObject);
                }
                return;
            }

            CharacterSpecificPalette col = palette.GetPaletteForCharacter(player);
            shirt.color = col.ShirtColor.AsColor;
            overalls.color = col.OverallsColor.AsColor;
        }

        public void OnSelect(BaseEventData eventData) {
            UpdateLabel();
        }

        public void OnPress() {
            UpdateLabel();
        }

        private void UpdateLabel() {
            colorNameString.text = GlobalController.Instance.translationManager.GetTranslation(palette ? palette.translationKey : "skin.default");
        }
    }
}
