using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using NSMB.Extensions;

public class ColorChooser : MonoBehaviour, KeepChildInFocus.IFocusIgnore {

    //---Serialized Variables
    [SerializeField] private Canvas baseCanvas;
    [SerializeField] private GameObject template, blockerTemplate, content;
    [SerializeField] private Sprite clearSprite;
    [SerializeField] private string property;

    //---Private Variables
    private readonly List<ColorButton> colorButtons = new();
    private List<Button> buttons;
    private List<Navigation> navigations;
    private GameObject blocker;
    private int selected;

    public void Start() {
        content.SetActive(false);
        buttons = new();
        navigations = new();

        PlayerColorSet[] colors = ScriptableManager.Instance.skins;

        for (int i = 0; i < colors.Length; i++) {
            PlayerColorSet color = colors[i];

            GameObject newButton = Instantiate(template, template.transform.parent);
            ColorButton cb = newButton.GetComponent<ColorButton>();
            colorButtons.Add(cb);
            cb.palette = color;

            Button b = newButton.GetComponent<Button>();
            newButton.name = color?.name ?? "Reset";
            if (color == null)
                b.image.sprite = clearSprite;

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

        CharacterData character = NetworkHandler.Instance.runner.LocalPlayer.GetCharacterData(NetworkHandler.Instance.runner);
        ChangeCharacter(character);
    }

    public void ChangeCharacter(CharacterData data) {
        foreach (ColorButton b in colorButtons)
            b.Instantiate(data);
    }

    public void SelectColor(Button button) {
        selected = buttons.IndexOf(button);
        MainMenuManager.Instance.SwapPlayerSkin((byte) buttons.IndexOf(button), true);
        Close(false);

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Decide);
    }

    public void Open() {
        blocker = Instantiate(blockerTemplate, baseCanvas.transform);
        blocker.SetActive(true);
        content.SetActive(true);

        EventSystem.current.SetSelectedGameObject(buttons[selected].gameObject);

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Cursor);
    }

    public void Close(bool playSound) {
        Destroy(blocker);
        EventSystem.current.SetSelectedGameObject(gameObject);
        content.SetActive(false);

        if (playSound && MainMenuManager.Instance)
            MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Back);
    }
}
