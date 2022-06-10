using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ColorChooser : MonoBehaviour, KeepChildInFocus.IFocusIgnore {

    [SerializeField] string property;
    [SerializeField] GameObject template, blockerTemplate, content;
    [SerializeField] Sprite clearSprite;
    [SerializeField] Canvas baseCanvas;

    int selected;
    GameObject blocker;
    List<Button> buttons;
    List<Navigation> navigations;

    public void Start() {
        content.SetActive(false);
        buttons = new();
        navigations = new();

        CustomColors.PlayerColor[] colors = CustomColors.Colors;

        for (int i = 0; i < colors.Length; i++) {
            CustomColors.PlayerColor color = colors[i];

            GameObject newButton = Instantiate(template, template.transform.parent);
            ColorButton cb = newButton.GetComponent<ColorButton>();
            cb.palette = color;
            cb.Instantiate();

            Button b = newButton.GetComponent<Button>();
            newButton.name = color.name;
            if (color.hat.a == 0)
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
    }

    public void SelectColor(Button button) {
        selected = buttons.IndexOf(button);
        MainMenuManager.Instance.SetPlayerColor(buttons.IndexOf(button));
        Close();
    }
    public void Open() {
        blocker = Instantiate(blockerTemplate, baseCanvas.transform);
        blocker.SetActive(true);
        content.SetActive(true);

        EventSystem.current.SetSelectedGameObject(buttons[selected].gameObject);
    }
    public void Close() {
        Destroy(blocker);
        EventSystem.current.SetSelectedGameObject(gameObject);
        content.SetActive(false);
    }
}