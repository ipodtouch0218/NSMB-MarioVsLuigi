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
            if (palette)
            {
                colorNameString.text = GlobalController.Instance.translationManager.GetTranslation(palette.TranslationKey);
            }
            else
            {
                if (gameObject.name == "Reset")
                {
                    colorNameString.text = GlobalController.Instance.translationManager.GetTranslation("skin.default");
                }
                else
                {
                    colorNameString.text = GlobalController.Instance.translationManager.GetTranslation("ui.inroom.settings.game.mapchoosemode.random");
                    //The translation key currently used contains the text "Random" in the English language. In theory, there would need to be a "skin.random" entry added to all of the translation files, but I'm not a translator so I'm using this key for now.
                }
            }
        }
    }
}
