using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class UIUpdater : NetworkBehaviour {

    public static UIUpdater Instance { get; set; }

    public PlayerController player;
    [SerializeField] private TrackIcon playerTrackTemplate, starTrackTemplate;
    [SerializeField] private Sprite storedItemNull;
    [SerializeField] private TMP_Text uiTeamStars, uiStars, uiCoins, uiDebug, uiLives, uiCountdown;
    [SerializeField] private Image itemReserve, itemColor;

    private float pingSample = 0;
    private Material timerMaterial;
    private GameObject teamsParent, starsParent, coinsParent, livesParent, timerParent;
    private readonly List<Image> backgrounds = new();
    private bool uiHidden;

    private PlayerRef localPlayer;
    private CharacterData character;
    private int coins = -1, teamStars = -1, stars = -1, lives = -1, timer = -1;

    private TeamManager teamManager;
    private bool teams;

    public void Awake() {
        Instance = this;
    }

    public override void Spawned() {
        teams = SessionData.Instance.Teams;
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
        uiDebug.gameObject.SetActive(!Runner.IsServer);
    }

    public override void Render() {

        if (!Runner.IsServer) {
            pingSample = Mathf.Lerp(pingSample, GetCurrentPing(), Mathf.Clamp01(Time.unscaledDeltaTime));
            if (pingSample == float.NaN)
                pingSample = 0;

            uiDebug.text = "<mark=#000000b0 padding=\"20, 20, 20, 20\"><font=\"defaultFont\">Ping: " + (int) pingSample + "ms</font>";
        }

        //Player stuff update.
        player = GameManager.Instance.localPlayer;

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
        if (!player || GameManager.Instance.gameover)
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
                uiLives.text = character.uistring + Utils.GetSymbolString("x" + lives);
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
}
