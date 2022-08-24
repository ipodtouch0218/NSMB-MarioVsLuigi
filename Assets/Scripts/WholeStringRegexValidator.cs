using System.Text.RegularExpressions;
using UnityEngine;

using TMPro;

[CreateAssetMenu(fileName = "WholeStringRegexValidator", menuName = "ScriptableObjects/WholeStringRegexValidator")]
public class WholeStringRegexValidator : TMP_InputValidator {

    public string pattern;

    public override char Validate(ref string text, ref int pos, char ch) {
        if (Regex.IsMatch(text + ch, pattern)) {
            text = text.Insert(pos, ch.ToString());
            pos++;
            return ch;
        }

        return (char) 0;
    }
}