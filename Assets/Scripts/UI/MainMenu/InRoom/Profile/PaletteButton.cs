using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class PaletteButton : MonoBehaviour, ISelectHandler {

        //---Public Variables
        [HideInInspector] public PaletteSet palette;
        public Button button;

        //---Serialized Variables
        [SerializeField] private TMP_Text colorNameString;
        [SerializeField] private Image shirt, overalls;

        public void Instantiate(AssetRef<CharacterAsset> player) {
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
            colorNameString.text = GlobalController.Instance.translationManager.GetTranslation(palette ? palette.TranslationKey : "skin.default");
        }
    }
}
