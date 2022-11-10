using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class UIUpdater : MonoBehaviour {

    public static UIUpdater Instance { get; set; }

    public GameObject playerTrackTemplate, starTrackTemplate;
    public PlayerController player;
    public Sprite storedItemNull;
    public TMP_Text uiTeamStars, uiStars, uiCoins, uiDebug, uiLives, uiCountdown;
    public Image itemReserve, itemColor;
    public float pingSample = 0;

    private NetworkRunner Runner => NetworkHandler.Instance.runner;

    private Material timerMaterial;
    private GameObject teamsParent, starsParent, coinsParent, livesParent, timerParent;
    private readonly List<Image> backgrounds = new();
    private bool uiHidden;

    private PlayerRef localPlayer;
    private CharacterData character;
    private int coins = -1, teamStars = -1, stars = -1, lives = -1, timer = -1;

    private TeamManager teamManager;
    private bool teams;

    public void Start() {
        Instance = this;
        teams = LobbyData.Instance.Teams;
        teamManager = GameManager.Instance.teamManager;

        localPlayer = Runner.LocalPlayer;
        character = localPlayer.GetCharacterData(Runner);
        pingSample = GetCurrentPing();

        teamsParent = uiTeamStars.transform.parent.gameObject;
        starsParent = uiStars.transform.parent.gameObject;
        coinsParent = uiCoins.transform.parent.gameObject;
        livesParent = uiLives.transform.parent.gameObject;
        timerParent = uiCountdown.transform.parent.gameObject;

        backgrounds.Add(teamsParent.GetComponentInChildren<Image>());
        backgrounds.Add(starsParent.GetComponentInChildren<Image>());
        backgrounds.Add(coinsParent.GetComponentInChildren<Image>());
        backgrounds.Add(livesParent.GetComponentInChildren<Image>());
        backgrounds.Add(timerParent.GetComponentInChildren<Image>());

        foreach (Image bg in backgrounds)
            bg.color = GameManager.Instance.levelUIColor;

        itemColor.color = new(GameManager.Instance.levelUIColor.r - 0.2f, GameManager.Instance.levelUIColor.g - 0.2f, GameManager.Instance.levelUIColor.b - 0.2f, GameManager.Instance.levelUIColor.a);

        teamsParent.SetActive(teams);
    }

    public void Update() {

        pingSample = Mathf.Lerp(pingSample, GetCurrentPing(), Mathf.Clamp01(Time.unscaledDeltaTime));
        if (pingSample == float.NaN)
            pingSample = 0;

        uiDebug.text = "<mark=#000000b0 padding=\"20, 20, 20, 20\"><font=\"defaultFont\">Ping: " + (int) pingSample + "ms</font>";

        //Player stuff update.
        player = GameManager.Instance.localPlayer;

        if (!player) {
            if (!uiHidden)
                ToggleUI(true);

            return;
        }

        if (uiHidden)
            ToggleUI(false);

        if (Runner.IsForward) {
            UpdateStoredItemUI();
            UpdateTextUI();
        }
    }

    private void ToggleUI(bool hidden) {
        uiHidden = hidden;

        teamsParent.SetActive(!hidden && teams);
        starsParent.SetActive(!hidden);
        livesParent.SetActive(!hidden);
        coinsParent.SetActive(!hidden);
        timerParent.SetActive(!hidden);
    }

    private void UpdateStoredItemUI() {
        if (!player)
            return;

        Powerup powerup = player.StoredPowerup.GetPowerupScriptable();
        if (!powerup) {
            itemReserve.sprite = storedItemNull;
            return;
        }

        itemReserve.sprite = powerup.reserveSprite ? powerup.reserveSprite : storedItemNull;
    }

    private void UpdateTextUI() {
        if (!player || GameManager.Instance.gameover)
            return;

        if (teams) {
            int team = player.data.Team;
            teamStars = teamManager.GetTeamStars(team);
            uiTeamStars.text = Utils.teamSprites[team] + Utils.GetSymbolString("x" + stars + "/" + LobbyData.Instance.StarRequirement);
        }
        if (player.Stars != stars) {
            stars = player.Stars;
            string starString = "Sx" + stars;
            if (!teams)
                starString += "/" + LobbyData.Instance.StarRequirement;

            uiStars.text = Utils.GetSymbolString(starString);
        }
        if (player.Coins != coins) {
            coins = player.Coins;
            uiCoins.text = Utils.GetSymbolString("Cx" + coins + "/" + LobbyData.Instance.CoinRequirement);
        }

        if (player.Lives >= 0) {
            if (player.Lives != lives) {
                lives = player.Lives;
                uiLives.text = character.uistring + Utils.GetSymbolString("x" + lives);
            }
        } else {
            livesParent.SetActive(false);
        }

        if (LobbyData.Instance.Timer > 0) {
            float? timeRemaining = GameManager.Instance.GameEndTimer.RemainingTime(Runner);

            if (timeRemaining != null) {
                int seconds = Mathf.CeilToInt(timeRemaining.Value - 1);
                seconds = Mathf.Clamp(seconds, 0, LobbyData.Instance.Timer);

                if (seconds != timer) {
                    timer = seconds;
                    uiCountdown.text = Utils.GetSymbolString("cx" + (timer / 60) + ":" + (seconds % 60).ToString("00"));
                    timerParent.SetActive(true);
                }

                if (timeRemaining <= 0 && !timerMaterial) {
                    CanvasRenderer cr = uiCountdown.transform.GetChild(0).GetComponent<CanvasRenderer>();
                    cr.SetMaterial(timerMaterial = new(cr.GetMaterial()), 0);
                    timerMaterial.SetColor("_Color", new Color32(255, 0, 0, 255));
                }
            }
        } else {
            timerParent.SetActive(false);
        }
    }

    public GameObject CreatePlayerIcon(PlayerController player) {
        GameObject trackObject = Instantiate(playerTrackTemplate, playerTrackTemplate.transform.parent);
        TrackIcon icon = trackObject.GetComponent<TrackIcon>();
        icon.target = player.gameObject;

        trackObject.SetActive(true);

        return trackObject;
    }

    private int GetCurrentPing() {
        try {
            return (int) (Runner.GetPlayerRtt(localPlayer) * 1000f);
        } catch {
            return 0;
        }
    }
}
