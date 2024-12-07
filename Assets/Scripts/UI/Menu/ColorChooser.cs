using NSMB.Extensions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Button = UnityEngine.UI.Button;
using Navigation = UnityEngine.UI.Navigation;

namespace NSMB.UI.MainMenu {
    public class ColorChooser : MonoBehaviour, KeepChildInFocus.IFocusIgnore {

        //---Serialized Variables
        // [SerializeField] private SimulationConfig config;
        [SerializeField] private Canvas baseCanvas;
        [SerializeField] private GameObject template, blockerTemplate, content;
        [SerializeField] private Sprite clearSprite;
        [SerializeField] private CharacterAsset defaultCharacter;
        [SerializeField] private GameObject selectOnClose;
        [SerializeField] private AudioSource sfx;

        //---Private Variables
        private readonly List<ColorButton> colorButtons = new();
        private readonly List<Button> buttons = new();
        private readonly List<Navigation> navigations = new();
        private GameObject blocker;
        private int selected;
        private bool initialized;

        public void Initialize() {
            if (initialized) {
                return;
            }

            PlayerColorSet[] colors = ScriptableManager.Instance.skins;

            for (int i = 0; i < colors.Length; i++) {
                PlayerColorSet color = colors[i];

                GameObject newButton = Instantiate(template, template.transform.parent);
                ColorButton cb = newButton.GetComponent<ColorButton>();
                colorButtons.Add(cb);
                cb.palette = color;

                Button b = newButton.GetComponent<Button>();
                newButton.name = color ? color.name : "Reset";
                if (color == null) {
                    b.image.sprite = clearSprite;
                }

                newButton.SetActive(true);
                buttons.Add(b);

                Navigation navigation = new() { mode = Navigation.Mode.Explicit };

                if (i > 0 && i % 4 != 0) {
                    Navigation n = navigations[i - 1];
                    n.selectOnRight = b;
                    navigations[i - 1] = n;
                    navigation.selectOnLeft = buttons[i - 1];
                }
                if (i >= 4) {
                    Navigation n = navigations[i - 4];
                    n.selectOnDown = b;
                    navigations[i - 4] = n;
                    navigation.selectOnUp = buttons[i - 4];
                }

                navigations.Add(navigation);
            }

            for (int i = 0; i < buttons.Count; i++) {
                buttons[i].navigation = navigations[i];
            }
            initialized = true;

            foreach (ColorButton b in colorButtons) {
                b.Instantiate(defaultCharacter);
            }
        }

        public void Start() {
            Initialize();
        }

        public void ChangeCharacter(CharacterAsset data) {
            Initialize();
            foreach (ColorButton b in colorButtons) {
                b.Instantiate(data);
            }
        }

        public void SelectColor(Button button) {
            selected = buttons.IndexOf(button);
            MainMenuManager.Instance.SwapPlayerSkin(buttons.IndexOf(button), true);
            Close(false);

            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
            }
        }

        public void Open() {
            Initialize();

            blocker = Instantiate(blockerTemplate, baseCanvas.transform);
            gameObject.SetActive(true);
            blocker.SetActive(true);
            content.SetActive(true);

            EventSystem.current.SetSelectedGameObject(buttons[selected].gameObject);

            sfx.Play();
        }

        public void Close(bool playSound) {
            gameObject.SetActive(false);
            Destroy(blocker);
            EventSystem.current.SetSelectedGameObject(selectOnClose);
            content.SetActive(false);

            if (playSound) {
                sfx.PlayOneShot(SoundEffect.UI_Back);
            }
        }
    }
}
