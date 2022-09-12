using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "TextSubmitValidator", menuName = "ScriptableObjects/TextSubmitValidator")]
public class TextSubmitValidator : TMP_InputValidator {

    public override char Validate(ref string text, ref int pos, char ch) {
        if (ch == '\n' || ch == '\xB') {
            //submit
            MainMenuManager.Instance.SendChat();
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
