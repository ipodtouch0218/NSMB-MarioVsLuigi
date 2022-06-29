using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "TextSubmitValidator", menuName = "ScriptableObjects/TextSubmitValidator")]
public class TextSubmitValidator : TMP_InputValidator {

    public override char Validate(ref string text, ref int pos, char ch) {
        if (ch == '>' || ch == '<')
            return '\0';

        if (ch == '\n' || ch == '\xB') {
            //submit
            MainMenuManager.Instance.SendChat();
            text = "";
            pos = 0;
        } else {
            text = text.Insert(pos, ch.ToString());
            pos++;
        }
        return ch;
    }
}