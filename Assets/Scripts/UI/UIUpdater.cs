using NSMB.Utils;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using NSMB.Extensions;
using Fusion;

public class UIUpdater : MonoBehaviour {

    public static UIUpdater Instance;
    public GameObject playerTrackTemplate, starTrackTemplate;
    public PlayerController player;
    public Sprite storedItemNull;
    public TMP_Text uiStars, uiCoins, uiDebug, uiLives, uiCountdown;
    public Image itemReserve, itemColor;
    public float pingSample = 0;

    private NetworkRunner Runner => NetworkHandler.Instance.runner;
    private int CurrentPing {
        get {
            try {
                return (int) (Runner.GetPlayerRtt(localPlayer) * 1000f);
            } catch {
                return 0;
            }
        }
    }

    private Material timerMaterial;
    private GameObject starsParent, coinsParent, livesParent, timerParent;
    private readonly List<Image> backgrounds = new();
    private bool uiHidden;

    private PlayerRef localPlayer;
    private CharacterData character;
    private int coins = -1, stars = -1, lives = -1, timer = -1;

    public void Start() {
        Instance = this;

        localPlayer = Runner.LocalPlayer;
        character = localPlayer.GetCharacterData(Runner);
        pingSample = CurrentPing;

        starsParent = uiStars.transform.parent.gameObject;
        coinsParent = uiCoins.transform.parent.gameObject;
        livesParent = uiLives.transform.parent.gameObject;
        timerParent = uiCountdown.transform.parent.gameObject;

        backgrounds.Add(starsParent.GetComponentInChildren<Image>());
        backgrounds.Add(coinsParent.GetComponentInChildren<Image>());
        backgrounds.Add(livesParent.GetComponentInChildren<Image>());
        backgrounds.Add(timerParent.GetComponentInChildren<Image>());

        foreach (Image bg in backgrounds)
            bg.color = GameManager.Instance.levelUIColor;
        itemColor.color = new(GameManager.Instance.levelUIColor.r - 0.2f, GameManager.Instance.levelUIColor.g - 0.2f, GameManager.Instance.levelUIColor.b - 0.2f, GameManager.Instance.levelUIColor.a);
    }

    public void Update() {

        pingSample = Mathf.Lerp(pingSample, CurrentPing, Mathf.Clamp01(Time.unscaledDeltaTime));
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

        starsParent.SetActive(!hidden);
        livesParent.SetActive(!hidden);
        coinsParent.SetActive(!hidden);
        timerParent.SetActive(!hidden);
    }

    private void UpdateStoredItemUI() {
        if (!player)
            return;

        itemReserve.sprite = player.StoredPowerup != Enums.PowerupState.None ? Enums.PowerupFromState[player.StoredPowerup].reserveSprite : storedItemNull;
    }

    private void UpdateTextUI() {
        if (!player || GameManager.Instance.gameover)
            return;

        if (player.Stars != stars) {
            stars = player.Stars;
            uiStars.text = Utils.GetSymbolString("Sx" + stars + "/" + LobbyData.Instance.StarRequirement);
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
            float timeRemaining = GameManager.Instance.GameEndTimer.RemainingTime(Runner) ?? 0f;

            int seconds = Mathf.CeilToInt(timeRemaining);
            seconds = Mathf.Clamp(seconds, 0, LobbyData.Instance.Timer);

            if (seconds != timer) {
                timer = seconds;
                uiCountdown.text = Utils.GetSymbolString("cx" + (timer / 60) + ":" + (seconds % 60).ToString("00"));
                timerParent.SetActive(true);
            }

            if (timeRemaining <= 0 && !timerMaterial) {
                CanvasRenderer cr = uiCountdown.transform.GetChild(0).GetComponent<CanvasRenderer>();
                cr.SetMaterial(timerMaterial = new(cr.GetMaterial()), 0);
                timerMaterial.SetColor("_Color", new Color32(255, 1, 1, 255));
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
}
