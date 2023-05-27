using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "TextSubmitValidator", menuName = "ScriptableObjects/Input Validators/TextSubmitValidator")]
public class TextSubmitValidator : TMP_InputValidator {

    public override char Validate(ref string text, ref int pos, char ch) {
        if (ch == '\n' || ch == '\xB') {
            //submit
            MainMenuManager.Instance.chat.SendChat();
            return '\0';
        } else {
            if (text.Length >= 128)
                return '\0';

            text = text.Insert(pos, ch.ToString());
            pos++;
        }
        return ch;
    }
}
