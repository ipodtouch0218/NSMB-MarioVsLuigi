using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;
using NSMB.Translation;

public class UIUpdater : NetworkBehaviour {

    public static UIUpdater Instance { get; set; }

    //---Public Variables
    public PlayerController player;

    //---Serialized Variables
    [SerializeField] private TrackIcon playerTrackTemplate, starTrackTemplate;
    [SerializeField] private Sprite storedItemNull;
    [SerializeField] private TMP_Text uiTeamStars, uiStars, uiCoins, uiDebug, uiLives, uiCountdown;
    [SerializeField] private Image itemReserve, itemColor;

    //---Private Variables
    private readonly List<Image> backgrounds = new();
    private GameObject teamsParent, starsParent, coinsParent, livesParent, timerParent;
    private Material timerMaterial;
    private PlayerRef localPlayer;
    private bool uiHidden;

    private TeamManager teamManager;
    private bool teams;
    private int coins = -1, teamStars = -1, stars = -1, lives = -1, timer = -1;

    public void Awake() {
        Instance = this;
    }

    public void OnEnable() {
        GlobalController.Instance.translationManager.OnLanguageChanged += OnLanguageChanged;
        OnLanguageChanged(GlobalController.Instance.translationManager);
    }

    public void OnDisable() {
        GlobalController.Instance.translationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(TranslationManager obj) {
        UpdatePingText();
    }

    public void Start() {
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
    }

    public override void Spawned() {
        teams = SessionData.Instance.Teams;
        teamManager = GameManager.Instance.teamManager;

        localPlayer = Runner.LocalPlayer;

        foreach (Image bg in backgrounds)
            bg.color = GameManager.Instance.levelUIColor;

        Color uiColor = GameManager.Instance.levelUIColor;
        uiColor.r -= 0.2f;
        uiColor.g -= 0.2f;
        uiColor.b -= 0.2f;
        itemColor.color = uiColor;

        teamsParent.SetActive(teams);

        if (!Runner.IsServer)
            StartCoroutine(UpdatePingTextCoroutine());
    }

    public override void Render() {

        if (!player) {
            if (!uiHidden)
                ToggleUI(true);

            return;
        }

        if (uiHidden)
            ToggleUI(false);

        UpdateStoredItemUI();
        UpdateTextUI();
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
        if (!player || GameManager.Instance.GameEnded)
            return;

        if (teams) {
            int team = player.data.Team;
            teamStars = teamManager.GetTeamStars(team);
            uiTeamStars.text = ScriptableManager.Instance.teams[team].textSprite + Utils.GetSymbolString("x" + teamStars + "/" + SessionData.Instance.StarRequirement);
        }
        if (player.Stars != stars) {
            stars = player.Stars;
            string starString = "Sx" + stars;
            if (!teams)
                starString += "/" + SessionData.Instance.StarRequirement;

            uiStars.text = Utils.GetSymbolString(starString);
        }
        if (player.Coins != coins) {
            coins = player.Coins;
            uiCoins.text = Utils.GetSymbolString("Cx" + coins + "/" + SessionData.Instance.CoinRequirement);
        }

        if (player.Lives >= 0) {
            if (player.Lives != lives) {
                lives = player.Lives;
                uiLives.text = player.data.GetCharacterData().uistring + Utils.GetSymbolString("x" + lives);
            }
        } else {
            livesParent.SetActive(false);
        }

        if (SessionData.Instance.Timer > 0) {
            float? timeRemaining = GameManager.Instance.GameEndTimer.RemainingTime(Runner);

            if (timeRemaining != null) {
                int seconds = Mathf.CeilToInt(timeRemaining.Value - 1);
                seconds = Mathf.Clamp(seconds, 0, SessionData.Instance.Timer);

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

    public TrackIcon CreateTrackIcon(Component comp) {
        TrackIcon icon;
        if (comp is PlayerController)
            icon = Instantiate(playerTrackTemplate, playerTrackTemplate.transform.parent);
        else if (comp is StarBouncer)
            icon = Instantiate(starTrackTemplate, starTrackTemplate.transform.parent);
        else
            return null;

        icon.target = comp.gameObject;
        icon.gameObject.SetActive(true);
        return icon;
    }

    private int GetCurrentPing() {
        try {
            return (int) (Runner.GetPlayerRtt(localPlayer) * 1000f);
        } catch {
            return 0;
        }
    }

    private static readonly WaitForSeconds PingSampleRate = new(0.5f);
    private IEnumerator UpdatePingTextCoroutine() {
        while (true) {
            yield return PingSampleRate;
            UpdatePingText();
        }
    }

    private void UpdatePingText() {
        if (!Runner)
            return;

        if (Runner.IsServer) {
            uiDebug.text = "<mark=#000000b0 padding=\"16,16,10,10\"><font=\"MarioFont\"> <sprite=60>" + GlobalController.Instance.translationManager.GetTranslation("ui.game.ping.hosting");
        } else {
            int ping = GetCurrentPing();
            uiDebug.text = "<mark=#000000b0 padding=\"16,16,10,10\"><font=\"MarioFont\">" + Utils.GetPingSymbol(ping) + ping;
        }
    }

    public void OnReserveItemIconClicked() {
        if (!GameManager.Instance)
            return;

        if (!GameManager.Instance.localPlayer)
            return;

        GameManager.Instance.localPlayer.RPC_SpawnReserveItem();
    }
}
