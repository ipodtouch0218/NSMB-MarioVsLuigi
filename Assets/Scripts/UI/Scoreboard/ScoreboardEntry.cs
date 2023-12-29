using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Utils;

public class ScoreboardEntry : MonoBehaviour, IComparable {

    //---Serialized Variables
    [SerializeField] private TMP_Text nameText, valuesText;
    [SerializeField] private Image background;

    //---Public Variables
    public PlayerController target;

    //---Private Variables
    private PlayerData data;
    private NicknameColor nicknameColor;
    private int playerId, currentLives, currentStars, currentPing;
    private bool disconnected;
    private int deathTick;

    public void Start() {

        if (!target) {
            enabled = false;
            return;
        }

        data = target.Object.InputAuthority.GetPlayerData(target.Runner);

        playerId = target.PlayerId;
        nameText.text = (data.IsRoomOwner ? "<sprite name=connection_host>" : "<sprite name=connection_great>") + data.GetNickname();

        Color c = target.animationController.GlowColor;
        background.color = new(c.r, c.g, c.b, 0.5f);

        nicknameColor = data.NicknameColor;
        nameText.color = nicknameColor.color;

        NetworkHandler.OnPlayerLeft += OnPlayerLeft;
    }

    public void OnDestroy() {
        NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
    }

    public void Update() {
        CheckForTextUpdate();

        if (nicknameColor.isRainbow)
            nameText.color = Utils.GetRainbowColor(NetworkHandler.Runner);
    }

    private void CheckForTextUpdate() {
        if (disconnected)
            return;

        if (!data || !data.Object || !data.Object.IsValid) {
            disconnected = true;
            nameText.text = Regex.Replace(nameText.text, "<sprite name=.*>", "<sprite name=connection_disconnected>");

        } else if (!data.IsRoomOwner && currentPing != data.Ping) {
            currentPing = data.Ping;
            nameText.text = Utils.GetPingSymbol(currentPing) + data.GetNickname();
        }

        if (!target || !target.Object || disconnected) {
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
            txt += target.Data.GetCharacterData().uistring + Utils.GetSymbolString(currentLives.ToString());
        txt += Utils.GetSymbolString("S" + currentStars);

        valuesText.text = txt;
    }

    public int CompareTo(object obj) {
        if (obj is not ScoreboardEntry other)
            return -1;

        if (!target && !other.target)
            return other.deathTick - deathTick;

        if (!target ^ !other.target)
            return !target ? 1 : -1;

        if (currentStars == other.currentStars || currentLives == 0 || other.currentLives == 0) {
            if (Mathf.Max(0, currentLives) == Mathf.Max(0, other.currentLives))
                return playerId - other.playerId;

            return other.currentLives - currentLives;
        }

        return other.currentStars - currentStars;
    }

    //---Callbacks
    private void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        if (target.Object.InputAuthority == player)
            ScoreboardUpdater.Instance.DestroyEntry(this);
    }
}
