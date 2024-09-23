using NSMB.Entities.Player;
using NSMB.Translation;
using NSMB.Utils;
using Quantum;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIUpdater : QuantumCallbacks {

    //---Static Variables
    private static readonly int ParamIn = Animator.StringToHash("in");
    private static readonly int ParamOut = Animator.StringToHash("out");
    private static readonly int ParamHasItem = Animator.StringToHash("has-item");

    //---Properties
    public EntityRef Target { get; set; }

    //---Serialized Variables
    [SerializeField] private PlayerElements playerElements;
    [SerializeField] private TrackIcon playerTrackTemplate, starTrackTemplate;
    [SerializeField] private Sprite storedItemNull;
    [SerializeField] private TMP_Text uiTeamStars, uiStars, uiCoins, uiDebug, uiLives, uiCountdown;
    [SerializeField] private Image itemReserve, itemColor;
    [SerializeField] private GameObject boos;
    [SerializeField] private Animator reserveAnimator;
    [SerializeField] private InputActionReference pauseAction;

    //---Private Variables
    private readonly Dictionary<MonoBehaviour, TrackIcon> entityTrackIcons = new();
    private readonly List<Image> backgrounds = new();
    private GameObject teamsParent, starsParent, coinsParent, livesParent, timerParent;
    private Material timerMaterial;
    private PlayerRef localPlayer;
    private bool uiHidden, paused;

    //private TeamManager teamManager;
    private int cachedCoins = -1, teamStars = -1, cachedStars = -1, cachedLives = -1, cachedTimer = -1;
    private PowerupAsset previousPowerup;
    private VersusStageData stage;

    protected override void OnEnable() {
        base.OnEnable();

        MarioAnimator.MarioPlayerInitialized += OnMarioInitialized;
        MarioAnimator.MarioPlayerDestroyed += OnMarioDestroyed;
        BigStarAnimator.BigStarInitialized += OnStarInitialized;
        BigStarAnimator.BigStarDestroyed += OnStarDestroyed;
        TranslationManager.OnLanguageChanged += OnLanguageChanged;
        OnLanguageChanged(GlobalController.Instance.translationManager);
    }

    protected override void OnDisable() {
        base.OnDisable();

        MarioAnimator.MarioPlayerInitialized -= OnMarioInitialized;
        MarioAnimator.MarioPlayerDestroyed -= OnMarioDestroyed;
        BigStarAnimator.BigStarInitialized -= OnStarInitialized;
        BigStarAnimator.BigStarDestroyed -= OnStarDestroyed;
        TranslationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    public void Awake() {
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

        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventTimerExpired>(this, OnTimerExpired);
        boos.SetActive(stage.HidePlayersOnMinimap);
        StartCoroutine(UpdatePingTextCoroutine());
    }

    public override void OnUpdateView(QuantumGame game) {
        // PlayerTrackIcon.HideAllPlayerIcons = !GameManager.Instance.spectationManager.Spectating && GameManager.Instance.hidePlayersOnMinimap;

        Frame f = game.Frames.Predicted;
        UpdateTrackIcons(f);

        if (!Target.IsValid
            || !f.TryGet(Target, out MarioPlayer mario)) {
            return;
        }

        /*
        if (!player) {
            if (!uiHidden) {
                ToggleUI(true);
            }

            return;
        }
        */

        if (uiHidden) {
            ToggleUI(f, false);
        }

        UpdateStoredItemUI(mario);
        UpdateTextUI(f, mario);
        ApplyUIColor(f, mario);
    }

    private void OnMarioInitialized(Frame f, MarioAnimator mario) {
        entityTrackIcons[mario] = CreateTrackIcon(f, mario.entity.EntityRef, mario.transform);
    }

    private void OnMarioDestroyed(Frame f, MarioAnimator mario) {
        if (entityTrackIcons.TryGetValue(mario, out TrackIcon icon)) {
            Destroy(icon.gameObject);
        }
    }

    private void OnStarInitialized(Frame f, BigStarAnimator star) {
        entityTrackIcons[star] = CreateTrackIcon(f, star.entity.EntityRef, star.transform);
    }

    private void OnStarDestroyed(Frame f, BigStarAnimator star) {
        if (entityTrackIcons.TryGetValue(star, out TrackIcon icon)) {
            Destroy(icon.gameObject);
        }
    }

    private void UpdateTrackIcons(Frame f) {
        HashSet<EntityRef> validEntities = new();

        var filter = f.Filter<BigStar>();
        while (filter.Next(out EntityRef entity, out _)) {
            validEntities.Add(entity);
        }

    }

    private unsafe void ToggleUI(Frame f, bool hidden) {
        uiHidden = hidden;

        teamsParent.SetActive(!hidden && f.Global->Rules.TeamsEnabled);
        starsParent.SetActive(!hidden);
        livesParent.SetActive(!hidden);
        coinsParent.SetActive(!hidden);
        timerParent.SetActive(!hidden);
    }

    private void UpdateStoredItemUI(MarioPlayer mario) {
        PowerupAsset powerup = QuantumUnityDB.GetGlobalAsset(mario.ReserveItem);
        reserveAnimator.SetBool(ParamHasItem, powerup && powerup.ReserveSprite);

        if (!powerup) {
            if (previousPowerup != powerup) {
                reserveAnimator.SetTrigger(ParamOut);
                previousPowerup = powerup;
            }
            return;
        }

        itemReserve.sprite = powerup.ReserveSprite ? powerup.ReserveSprite : storedItemNull;
        if (previousPowerup != powerup) {
            reserveAnimator.SetTrigger(ParamIn);
            previousPowerup = powerup;
        }
    }

    // The "reserve-static" animation is just for the "No Item" sprite to not do the bopping idling movement.
    // We gotta wait for the "reserve-summon" animation, which always auto-exits to the static one,
    // to finish before swapping to the "No Item" sprite.
    public void OnReserveItemStaticStarted() {
        itemReserve.sprite = storedItemNull;
    }

    public void OnTimerExpired(EventTimerExpired e) {
        CanvasRenderer cr = uiCountdown.transform.GetChild(0).GetComponent<CanvasRenderer>();
        cr.SetMaterial(timerMaterial = new(cr.GetMaterial()), 0);
        timerMaterial.SetColor("_Color", Color.red);
    }

    private unsafe void UpdateTextUI(Frame f, MarioPlayer mario) {

        var rules = f.Global->Rules;

        int starRequirement = rules.StarsToWin;
        int coinRequirement = rules.CoinsForPowerup;
        bool teamsEnabled = rules.TeamsEnabled;
        bool livesEnabled = rules.IsLivesEnabled;
        bool timerEnabled = rules.TimerSeconds > 0;

        if (rules.TeamsEnabled) {
            int teamIndex = mario.Team;
            //teamManager?.GetTeamStars(teamIndex, out teamStars);
            Team team = ScriptableManager.Instance.teams[teamIndex];
            uiTeamStars.text = (Settings.Instance.GraphicsColorblind ? team.textSpriteColorblind : team.textSpriteNormal) + Utils.GetSymbolString("x" + teamStars + "/" + starRequirement);
        } else {
            teamsParent.SetActive(false);
        }

        if (mario.Stars != cachedStars) {
            cachedStars = mario.Stars;
            string starString = "Sx" + cachedStars;
            if (!teamsEnabled) {
                starString += "/" + starRequirement;
            }

            uiStars.text = Utils.GetSymbolString(starString);
        }
        if (mario.Coins != cachedCoins) {
            cachedCoins = mario.Coins;
            uiCoins.text = Utils.GetSymbolString("Cx" + cachedCoins + "/" + coinRequirement);
        }

        if (livesEnabled) {
            if (mario.Lives != cachedLives) {
                cachedLives = mario.Lives;
                uiLives.text = QuantumUnityDB.GetGlobalAsset(mario.CharacterAsset).UiString + Utils.GetSymbolString("x" + cachedLives);
            }
        } else {
            livesParent.SetActive(false);
        }

        if (timerEnabled) {
            float timeRemaining = f.Global->Timer.AsFloat;
            int secondsRemaining = Mathf.Max(Mathf.CeilToInt(timeRemaining), 0);

            if (secondsRemaining != cachedTimer) {
                cachedTimer = secondsRemaining;
                uiCountdown.text = Utils.GetSymbolString("Tx" + (secondsRemaining / 60) + ":" + (secondsRemaining % 60).ToString("00"));
                timerParent.SetActive(true);
            }
        } else {
            timerParent.SetActive(false);
        }
    }

    public TrackIcon CreateTrackIcon(Frame f, EntityRef entity, Transform target) {
        TrackIcon icon;
        if (f.Has<BigStar>(entity)) {
            icon = Instantiate(starTrackTemplate, starTrackTemplate.transform.parent);
        } else {
            icon = Instantiate(playerTrackTemplate, playerTrackTemplate.transform.parent);
        }

        icon.Initialize(playerElements, entity, target, stage);
        icon.gameObject.SetActive(true);
        return icon;
    }

    private static readonly WaitForSeconds PingSampleRate = new(0.5f);
    private IEnumerator UpdatePingTextCoroutine() {
        while (true) {
            yield return PingSampleRate;
            UpdatePingText();
        }
    }

    private void UpdatePingText() {
        int ping = (int) NetworkHandler.Ping.Value;
        uiDebug.text = "<mark=#000000b0 padding=\"16,16,10,10\"><font=\"MarioFont\">" + Utils.GetPingSymbol(ping) + ping;
        //uiDebug.isRightToLeftText = GlobalController.Instance.translationManager.RightToLeft;
    }

    private unsafe void ApplyUIColor(Frame f, MarioPlayer mario) {
        Color color = f.Global->Rules.TeamsEnabled ? Utils.GetTeamColor(mario.Team, 0.8f, 1f) : stage.UIColor;

        foreach (Image bg in backgrounds) {
            bg.color = color;
        }

        itemColor.color = color;
    }

    //---Callbacks
    private void OnLanguageChanged(TranslationManager tm) {
        UpdatePingText();
    }

    private void OnAllPlayersLoaded() {
        /*
        teams = SessionData.Instance.Teams;
        teamManager = GameManager.Instance.teamManager;

        localPlayer = Runner.LocalPlayer;

        ApplyUIColor();
        teamsParent.SetActive(teams);
        GameManager.Instance.teamScoreboardElement.gameObject.SetActive(teams);

        if (!Runner.IsServer) {
            StartCoroutine(UpdatePingTextCoroutine());
        }

        UpdatePingText();

        stars = -1;
        lives = -1;
        teamStars = -1;
        coins = -1;
        UpdateTextUI();
        */
    }

    public void OnReserveItemIconClicked() {
        if (QuantumRunner.DefaultGame is QuantumGame game) {
            game.SendCommand(new CommandSpawnReserveItem());
        }
    }
}