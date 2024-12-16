using NSMB.UI.MainMenu;
using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "TextSubmitValidator", menuName = "ScriptableObjects/Input Validators/TextSubmitValidator")]
public class TextSubmitValidator : TMP_InputValidator {

    private MainMenuChat chat;

    public override char Validate(ref string text, ref int pos, char ch) {
        if (ch == '\n' || ch == '\xB') {
            // Submit
            if (!chat) {
                chat = FindObjectOfType<MainMenuChat>();
                if (!chat) {
                    return '\0';
                }
            }
            chat.SendChat();
            return '\0';
        }

        if (text.Length >= 128) {
            return '\0';
        }

        text = text.Insert(pos, ch.ToString());
        pos++;
        return ch;
    }
}
