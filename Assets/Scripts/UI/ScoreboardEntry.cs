using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NSMB.Utils;

public class ScoreboardEntry : MonoBehaviour {

    [SerializeField] TMP_Text nameText, valuesText;
    [SerializeField] Image background;

    public PlayerController target;

    private int playerId, currentLives, currentStars;

    public void Start() {
        if (!target) {
            enabled = false;
            return;
        }

        playerId = target.playerId;
        nameText.text = target.photonView.Owner.NickName.Filter();

        Color c = target.AnimationController.GlowColor;
        background.color = new(c.r, c.g, c.b, 0.5f);
    }

    public void Update() {
        CheckForTextUpdate();
    }

    public void CheckForTextUpdate() {
        if (!target) {
            // our target lost all lives (or dc'd), disable the updater script
            enabled = false;
            background.color = new(0.4f, 0.4f, 0.4f, 0.5f);
            return;
        }
        if (target.lives == currentLives && target.stars == currentStars)
            // No changes.
            return;

        currentLives = target.lives;
        currentStars = target.stars;
        UpdateText();
        ScoreboardUpdater.instance.Reposition();
    }

    public void UpdateText() {
        string txt = "";
        if (currentLives >= 0)
            txt += target.character.uistring + Utils.GetSymbolString(currentLives.ToString());
        txt += Utils.GetSymbolString($"S{currentStars}");

        valuesText.text = txt;
    }

    public class EntryComparer : IComparer<ScoreboardEntry> {
        public int Compare(ScoreboardEntry x, ScoreboardEntry y) {
            if (x.target == null ^ y.target == null)
                return x.target == null ? 1 : -1;

            if (x.currentStars == y.currentStars || x.currentLives == 0 || y.currentLives == 0) {
                if (Mathf.Max(0, x.currentLives) == Mathf.Max(0, y.currentLives))
                    return x.playerId - y.playerId;

                return y.currentLives - x.currentLives;
            }

            return y.currentStars - x.currentStars;
        }
    }
}