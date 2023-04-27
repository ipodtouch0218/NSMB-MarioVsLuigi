using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using NSMB.Extensions;
using NSMB.Utils;

public class ScoreboardEntry : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private TMP_Text nameText, valuesText;
    [SerializeField] private Image background;
    [SerializeField] private float normalWidth = 250, controllerWidth = 280;

    //---Public Variables
    public PlayerController target;

    //---Private Variables
    private PlayerData data;
    private RectTransform rectTransform;
    private int playerId, currentLives, currentStars, currentPing;
    private bool isCameraController, rainbowEnabled, disconnected;

    private int deathTick;

    public void Awake() {
        rectTransform = GetComponent<RectTransform>();
    }

    public void Start() {

        if (!target) {
            enabled = false;
            return;
        }

        data = target.Object.InputAuthority.GetPlayerData(target.Runner);

        playerId = target.PlayerId;
        nameText.text = (data.IsRoomOwner ? "<sprite=60>" : "<sprite=56>") + data.GetNickname();

        Color c = target.animationController.GlowColor;
        background.color = new(c.r, c.g, c.b, 0.5f);

        rainbowEnabled = target.Object.InputAuthority.HasRainbowName();
    }

    public void Update() {
        CheckForTextUpdate();
        CheckForCameraControl();

        if (rainbowEnabled)
            nameText.color = Utils.GetRainbowColor(target.Runner);
    }

    private void CheckForCameraControl() {
        if (!(isCameraController ^ target.cameraController.IsControllingCamera))
            return;

        isCameraController = target.cameraController.IsControllingCamera;
        rectTransform.sizeDelta = new(isCameraController ? controllerWidth : normalWidth, rectTransform.sizeDelta.y);
    }

    private void CheckForTextUpdate() {
        if (disconnected)
            return;

        if (!data || !data.Object || !data.Object.IsValid) {
            disconnected = true;
            nameText.text = Regex.Replace(nameText.text, "<sprite=\\d*>", "<sprite=61>");

        } else if (!data.IsRoomOwner && currentPing != data.Ping) {
            currentPing = data.Ping;
            nameText.text = Utils.GetPingSymbol(currentPing) + data.GetNickname();
        }

        if (!target || disconnected) {
            // our target lost all lives (or dc'd)
            background.color = new(0.4f, 0.4f, 0.4f, 0.5f);
            deathTick = NetworkHandler.Runner.Tick;
            ScoreboardUpdater.Instance.RepositionEntries();
            return;
        }

        if (target.Lives != currentLives || target.Stars != currentStars) {
            currentLives = target.Lives;
            currentStars = target.Stars;
            UpdateScoreText();
            ScoreboardUpdater.Instance.RepositionEntries();
        }
    }

    private void UpdateScoreText() {
        string txt = "";
        if (currentLives >= 0)
            txt += target.data.GetCharacterData().uistring + Utils.GetSymbolString(currentLives.ToString());
        txt += Utils.GetSymbolString("S" + currentStars);

        valuesText.text = txt;
    }

    public class EntryComparer : IComparer<ScoreboardEntry> {
        public int Compare(ScoreboardEntry x, ScoreboardEntry y) {
            if (!x.target && !y.target)
                return y.deathTick - x.deathTick;

            if (!x.target ^ !y.target)
                return !x.target ? 1 : -1;

            if (x.currentStars == y.currentStars || x.currentLives == 0 || y.currentLives == 0) {
                if (Mathf.Max(0, x.currentLives) == Mathf.Max(0, y.currentLives))
                    return x.playerId - y.playerId;

                return y.currentLives - x.currentLives;
            }

            return y.currentStars - x.currentStars;
        }
    }
}
