using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        nameText.text = target.photonView.Owner.NickName;

        Color c = target.animationController.glowColor;
        background.color = new(c.r, c.g, c.b, 0.6f);
    }

    public void Update() {
        CheckForTextUpdate();
    }

    public void CheckForTextUpdate() {
        if (!target) {
            // our target lost all lives (or dc'd), disable the updater script
            enabled = false;
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
        string values = "";
        if (currentLives >= 0)
            values += target.character.uistring + currentLives;
        values += "S" + currentStars;

        valuesText.text = Utils.GetSymbolString(values);
    }

    public class EntryComparer : IComparer<ScoreboardEntry> {
        public int Compare(ScoreboardEntry x, ScoreboardEntry y) {
            if (x.target == null ^ y.target == null)
                return x.target == null ? -1 : 1;

            if (x.currentLives == 0 || y.currentLives == 0)
                return x.currentLives - y.currentLives;

            if (x.currentStars == y.currentStars)
                return x.playerId - y.playerId;

            return y.currentStars - x.currentStars;
        }
    }
}