using System;
using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "TimeTextValidator", menuName = "ScriptableObjects/Input Validators/TimeTextValidator")]
public class TimeTextValidator : TMP_InputValidator {

    public override char Validate(ref string text, ref int pos, char ch) {

        if (!(ch >= '0' && ch <= '9')) {
            return (char) 0;
        }

        if (text.Contains(':')) {
            text = Convert.ToString(ch);
            pos++;
        } else {
            if (text.Length >= 2) {
                return (char) 0;
            }

            text = text.Insert(pos, ch.ToString());
            pos++;
        }

        return (char) 0;
    }
}