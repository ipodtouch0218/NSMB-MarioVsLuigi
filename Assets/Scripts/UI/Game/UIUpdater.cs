using NSMB.Chat;
using NSMB.Entities.Player;
using NSMB.Entities.World;
using NSMB.Networking;
using NSMB.Quantum;
using NSMB.UI.Game.Track;
using NSMB.UI.Translation;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.Game {
    public unsafe class UIUpdater : QuantumSceneViewComponent<StageContext> {

        //---Properties
        public EntityRef Target => playerElements.Entity;

        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private CanvasGroup toggler;
        [SerializeField] private TrackIcon playerTrackTemplate, starTrackTemplate, starCoinTrackTemplate, objectiveCoinTrackTemplate;
        [SerializeField] private Sprite storedItemNull;
        [SerializeField] private TMP_Text uiTeamObjective, uiMainObjective, uiCoins, uiDebug, uiLives, uiCountdown;
        [SerializeField] private Image itemReserve, itemColor, deathFade;
        [SerializeField] private GameObject boos, reserveItemBox;
        [SerializeField] private Animation reserveAnimation;

        [SerializeField] private TMP_Text winText;
        [SerializeField] private Animator winTextAnimator;
        //[SerializeField] private RectTransform[] player

        //---Private 
        private readonly Dictionary<MonoBehaviour, TrackIcon> entityTrackIcons = new();
        private readonly Dictionary<Type, List<TrackIcon>> availablePooledTrackIcons = new();
        private readonly List<Image> backgrounds = new();
        private GameObject teamsParent, starsParent, coinsParent, livesParent, timerParent;
        private Material timerMaterial;

        //private TeamManager teamManager;
        private int cachedCoins = -1, cachedTeamObjective = -1, cachedObjective = -1, cachedLives = -1, cachedTimer = -1;
        private PowerupAsset previousPowerup;
        private EntityRef previousTarget;
        private bool previousMarioExists;
        private bool justResynced;

        private Coroutine endGameSequenceCoroutine, reserveSummonCoroutine;

        public override void OnEnable() {
            base.OnEnable();
            MarioPlayerAnimator.MarioPlayerInitialized += OnMarioInitialized;
            MarioPlayerAnimator.MarioPlayerDestroyed += OnMarioDestroyed;
            BigStarAnimator.BigStarInitialized += OnBigStarInitialized;
            BigStarAnimator.BigStarDestroyed += OnBigStarDestroyed;
            StarCoinAnimator.StarCoinInitialized += OnStarCoinInitialized;
            StarCoinAnimator.StarCoinDestroyed += OnStarCoinDestroyed;
            CoinAnimator.ObjectiveCoinInitialized += OnObjectiveCoinInitialized;
            CoinAnimator.ObjectiveCoinDestroyed += OnObjectiveCoinDestroyed;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            Settings.Controls.Debug.ToggleHUD.performed += OnToggleHUD;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public override void OnDisable() {
            base.OnDisable();
            MarioPlayerAnimator.MarioPlayerInitialized -= OnMarioInitialized;
            MarioPlayerAnimator.MarioPlayerDestroyed -= OnMarioDestroyed;
            BigStarAnimator.BigStarInitialized -= OnBigStarInitialized;
            BigStarAnimator.BigStarDestroyed -= OnBigStarDestroyed;
            StarCoinAnimator.StarCoinInitialized -= OnStarCoinInitialized;
            StarCoinAnimator.StarCoinDestroyed -= OnStarCoinDestroyed;
            CoinAnimator.ObjectiveCoinInitialized -= OnObjectiveCoinInitialized;
            CoinAnimator.ObjectiveCoinDestroyed -= OnObjectiveCoinDestroyed;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
            Settings.Controls.Debug.ToggleHUD.performed -= OnToggleHUD;
        }

        public void Initialize(QuantumGame game, Frame f) {
            // Add existing MarioPlayer icons
            MarioPlayerAnimator.AllMarioPlayers.RemoveWhere(ma => ma == null);

            foreach (MarioPlayerAnimator mario in MarioPlayerAnimator.AllMarioPlayers) {
                OnMarioInitialized(game, f, mario);
            }
        }

        public void Awake() {
            teamsParent = uiTeamObjective.transform.parent.gameObject;
            starsParent = uiMainObjective.transform.parent.gameObject;
            coinsParent = uiCoins.transform.parent.gameObject;
            livesParent = uiLives.transform.parent.gameObject;
            timerParent = uiCountdown.transform.parent.gameObject;

            backgrounds.Add(teamsParent.GetComponentInChildren<Image>());
            backgrounds.Add(starsParent.GetComponentInChildren<Image>());
            backgrounds.Add(coinsParent.GetComponentInChildren<Image>());
            backgrounds.Add(livesParent.GetComponentInChildren<Image>());
            backgrounds.Add(timerParent.GetComponentInChildren<Image>());
        }

        public void Start() {
            VersusStageData stage = ViewContext.Stage;

            PlayerTrackIcon.HideAllPlayerIcons = stage.HidePlayersOnMinimap;
            boos.SetActive(stage.HidePlayersOnMinimap);
            StartCoroutine(UpdatePingTextCoroutine());

            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
            QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
            QuantumEvent.Subscribe<EventTimerExpired>(this, OnTimerExpired);
            QuantumEvent.Subscribe<EventStartCameraFadeOut>(this, OnStartCameraFadeOut);
        }

        public void OnDestroy() {
            Destroy(timerMaterial);
        }

        public void OnUpdateView(CallbackUpdateView e) {
            if (!timerMaterial) {
                // what a hack.
                try {
                    CanvasRenderer cr = uiCountdown.transform.GetChild(0).GetComponent<CanvasRenderer>();
                    cr.SetMaterial(timerMaterial = new(cr.GetMaterial()), 0);
                } catch { }
            }

            QuantumGame game = e.Game;
            Frame f = game.Frames.Predicted;
            //UpdateTrackIcons(f);

            bool marioExists = f.Unsafe.TryGetPointer(Target, out MarioPlayer* mario);
            if (Target != previousTarget || marioExists != previousMarioExists) {
                UpdateElementVisibility(f, marioExists);
            }

            UpdateStoredItemUI(mario, previousTarget == Target && !justResynced);
            UpdateTextUI(f, mario);
            ApplyUIColor(f, mario);

            previousTarget = Target;
            previousMarioExists = marioExists;
            justResynced = false;
        }

        private void OnMarioInitialized(QuantumGame game, Frame f, MarioPlayerAnimator mario) {
            entityTrackIcons[mario] = CreateTrackIcon(Updater, f, mario.EntityRef, mario.transform);
        }

        private void OnMarioDestroyed(QuantumGame game, Frame f, MarioPlayerAnimator mario) {
            DestroyTrackIcon(mario);
        }

        private void OnBigStarInitialized(Frame f, BigStarAnimator star) {
            entityTrackIcons[star] = CreateTrackIcon(Updater, f, star.EntityRef, star.transform);
        }

        private void OnBigStarDestroyed(Frame f, BigStarAnimator star) {
            DestroyTrackIcon(star);
        }

        private void OnStarCoinInitialized(Frame f, StarCoinAnimator starCoin) {
            entityTrackIcons[starCoin] = CreateTrackIcon(Updater, f, starCoin.EntityRef, starCoin.transform);
        }

        private void OnStarCoinDestroyed(Frame f, StarCoinAnimator starCoin) {
            DestroyTrackIcon(starCoin);
        }

        private void OnObjectiveCoinInitialized(Frame f, CoinAnimator objectiveCoin) {
            entityTrackIcons[objectiveCoin] = CreateTrackIcon(Updater, f, objectiveCoin.EntityRef, objectiveCoin.transform);
        }

        private void OnObjectiveCoinDestroyed(CoinAnimator objectiveCoin) {
            DestroyTrackIcon(objectiveCoin);
        }

        private void UpdateStoredItemUI(MarioPlayer* mario, bool playAnimation) {
            if (mario == null) {
                return;
            }

            PowerupAsset powerup = QuantumUnityDB.GetGlobalAsset(mario->ReserveItem);
            if (previousPowerup == powerup) {
                return;
            }

            // New powerup
            if (reserveSummonCoroutine != null) {
                StopCoroutine(reserveSummonCoroutine);
                reserveSummonCoroutine = null;
            }
            if (playAnimation) {
                if (powerup) {
                    reserveAnimation.Play("reserve-in");
                    itemReserve.sprite = powerup.ReserveSprite;
                } else {
                    reserveAnimation.Play("reserve-out");
                    reserveSummonCoroutine = StartCoroutine(ReserveSummonCoroutine());
                }
            } else {
                itemReserve.sprite = (powerup && powerup.ReserveSprite) ? powerup.ReserveSprite : storedItemNull;
                reserveAnimation.Play();
            }
            previousPowerup = powerup;
        }

        private void UpdateElementVisibility(Frame f, bool marioExists) {
            teamsParent.SetActive(marioExists && f.Global->Rules.TeamsEnabled);
            starsParent.SetActive(marioExists);
            livesParent.SetActive(marioExists && f.Global->Rules.IsLivesEnabled);
            coinsParent.SetActive(marioExists);
            timerParent.SetActive(f.Global->Rules.IsTimerEnabled);
            reserveItemBox.SetActive(marioExists);
        }

        private IEnumerator ReserveSummonCoroutine() {
            yield return new WaitForSeconds(reserveAnimation.GetClip("reserve-out").length);
            itemReserve.sprite = storedItemNull;
            reserveAnimation.Play();
            reserveSummonCoroutine = null;
        }

        private void OnStartCameraFadeOut(EventStartCameraFadeOut e) {
            if (e.Entity != Target) {
                return;
            }
            StartCoroutine(FadeOutThenInCoroutine());
        }

        private IEnumerator FadeOutThenInCoroutine() {
            yield return FadeCoroutine(1, 0.25f);
            yield return new WaitForSeconds(0.1f);
            yield return FadeCoroutine(0, 0.25f);
        }

        private IEnumerator FadeCoroutine(float target, float duration) {
            float totalDuration = duration;
            Color color = deathFade.color;
            float startAlpha = color.a;

            while (duration > 0) {
                duration -= Time.deltaTime;
                color.a = Mathf.Lerp(target, startAlpha, duration / totalDuration);
                deathFade.color = color;
                yield return null;
            }
        }

        private void OnTimerExpired(EventTimerExpired e) {
            timerMaterial.SetColor("_Color", Color.red);
        }

        private unsafe void UpdateTextUI(Frame f, MarioPlayer* mario) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            var rules = f.Global->Rules;

            //int starRequirement = rules.StarsToWin;
            int coinRequirement = rules.CoinsForPowerup;
            bool teamsEnabled = rules.TeamsEnabled;
            bool livesEnabled = rules.IsLivesEnabled;
            bool timerEnabled = rules.TimerMinutes > 0;

            // TIMER
            if (timerEnabled) {
                float timeRemaining = f.Global->Timer.AsFloat;
                int secondsRemaining = Mathf.Max(Mathf.CeilToInt(timeRemaining), 0);

                if (secondsRemaining != cachedTimer) {
                    cachedTimer = secondsRemaining;
                    uiCountdown.text = Utils.GetSymbolString("Tx" + Utils.SecondsToMinuteSeconds(secondsRemaining));
                    timerParent.SetActive(true);
                }
            }

            if (mario == null) {
                return;
            }

            // TEAMS
            if (teamsEnabled) {
                if (mario->GetTeam(f) is byte teamIndex) {
                    int teamObjective = gamemode.GetTeamObjectiveCount(f, teamIndex);
                    if (cachedTeamObjective != teamObjective) {
                        cachedTeamObjective = teamObjective;
                        TeamAsset team = f.FindAsset(f.SimulationConfig.Teams[teamIndex]);
                        string objectiveString = "x" + cachedTeamObjective;
                        if (gamemode is StarChasersGamemode) {
                            objectiveString += "/" + rules.StarsToWin;
                        }
                        uiTeamObjective.text = (Settings.Instance.GraphicsColorblind ? team.textSpriteColorblind : team.textSpriteNormal) + Utils.GetSymbolString(objectiveString);
                    }
                }
            }

            // STARS
            int objective = gamemode.GetObjectiveCount(f, mario);
            if (objective != cachedObjective) {
                cachedObjective = objective;
                string objectiveString = gamemode.ObjectiveSymbolPrefix + "x" + cachedObjective;
                if (gamemode is StarChasersGamemode && !teamsEnabled) {
                    objectiveString += "/" + rules.StarsToWin;
                }

                uiMainObjective.text = Utils.GetSymbolString(objectiveString);
            }

            // COINS
            if (mario->Coins != cachedCoins) {
                cachedCoins = mario->Coins;
                uiCoins.text = Utils.GetSymbolString("Cx" + cachedCoins + "/" + coinRequirement);
            }

            // LIVES
            if (livesEnabled) {
                if (mario->Lives != cachedLives) {
                    cachedLives = mario->Lives;
                    uiLives.text = QuantumUnityDB.GetGlobalAsset(mario->CharacterAsset).UiString + Utils.GetSymbolString("x" + cachedLives);
                }
            }
        }

        public TrackIcon CreateTrackIcon(QuantumEntityViewUpdater evu, Frame f, EntityRef entity, Transform target) {
            TrackIcon icon;
            if (f.Has<BigStar>(entity)) {
                icon = Instantiate(starTrackTemplate, starTrackTemplate.transform.parent);
            } else if (f.Has<StarCoin>(entity)) {
                icon = Instantiate(starCoinTrackTemplate, starCoinTrackTemplate.transform.parent);
            } else if (f.Has<ObjectiveCoin>(entity)) {
                if (availablePooledTrackIcons.TryGetValue(typeof(CoinAnimator), out var pool) && pool.Count > 0) {
                    icon = pool[0];
                    pool.RemoveAt(0);
                } else {
                    icon = Instantiate(objectiveCoinTrackTemplate, objectiveCoinTrackTemplate.transform.parent);
                }
            } else if (f.Has<MarioPlayer>(entity)) {
                icon = Instantiate(playerTrackTemplate, playerTrackTemplate.transform.parent);
            } else {
                return null;
            }

            icon.Updater = evu;
            icon.Initialize(playerElements, entity, target);
            icon.gameObject.SetActive(true);
            return icon;
        }

        public void DestroyTrackIcon(MonoBehaviour animator) {
            if (entityTrackIcons.TryGetValue(animator, out TrackIcon icon)) {
                if (animator is CoinAnimator) {
                    // Pool.
                    icon.gameObject.SetActive(false);
                    if (!availablePooledTrackIcons.TryGetValue(animator.GetType(), out List<TrackIcon> pool)) {
                        availablePooledTrackIcons[animator.GetType()] = (pool = new());
                    }
                    pool.Add(icon);
                } else {
                    // Don't pool
                    Destroy(icon.gameObject);
                    entityTrackIcons.Remove(animator);
                }
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
            if (NetworkHandler.Client.InRoom) {
                int ping = (int) NetworkHandler.Ping.Value;
                uiDebug.text = "<mark=#000000b0 padding=\"16,16,10,10\"><font=\"MarioFont\">" + Utils.GetPingSymbol(ping) + ping;
                //uiDebug.isRightToLeftText = GlobalController.Instance.translationManager.RightToLeft;
            } else {
                uiDebug.enabled = false;
            }
        }

        private unsafe void ApplyUIColor(Frame f, MarioPlayer* mario) {
            Color color = (f.Global->Rules.TeamsEnabled && mario != null && mario->GetTeam(f) is byte team) ? Utils.GetTeamColor(f, team, 0.8f, 1f) : ViewContext.Stage.UIColor.AsColor;

            foreach (Image bg in backgrounds) {
                bg.color = color;
            }

            itemColor.color = color;
        }

        private IEnumerator EndGameSequence(SoundEffect resultMusic, string resultAnimationTrigger, float delay) {
            // Wait before playing the music 
            yield return new WaitForSecondsRealtime(delay);

            GlobalController.Instance.sfx.PlayOneShot(resultMusic);
            winTextAnimator.SetTrigger(resultAnimationTrigger);
            winText.enabled = true;
        }

        //---Callbacks
        private unsafe void OnGameResynced(CallbackGameResynced e) {
            if (endGameSequenceCoroutine != null) {
                StopCoroutine(endGameSequenceCoroutine);
                endGameSequenceCoroutine = null;

                // (Potentially) stop the win/loss
                GlobalController.Instance.sfx.Stop();

                winText.enabled = false;
                winTextAnimator.Play("Empty");
            }

            Frame f = e.Game.Frames.Predicted;
            Color timerColor = Color.white;
            if (f.Global->Timer <= 0 && f.Global->Rules.IsTimerEnabled) {
                timerColor = Color.red;
            }
            timerMaterial.SetColor("_Color", timerColor);

            justResynced = true;
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.Starting) {
                foreach (var mario in MarioPlayerAnimator.AllMarioPlayers) {
                    entityTrackIcons[mario] = CreateTrackIcon(Updater, PredictedFrame, mario.EntityRef, mario.transform);
                }
            }
        }

        private void OnGameEnded(EventGameEnded e) {
            Frame f = e.Game.Frames.Verified;
            bool teamMode = f.Global->Rules.TeamsEnabled;
            bool hasWinner = e.HasWinner;

            TranslationManager tm = GlobalController.Instance.translationManager;
            string resultText;
            string winner = null;
            bool local = false;

            if (e.EndedByHost) {
                resultText = tm.GetTranslation("ui.result.nocontest");
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.nocontest", color: ChatManager.Red);
            } else if (hasWinner) {
                if (teamMode) {
                    // Winning team
                    var teams = f.SimulationConfig.Teams;
                    winner = tm.GetTranslation(f.FindAsset(teams[e.WinningTeam % teams.Length]).nameTranslationKey);
                    resultText = tm.GetTranslationWithReplacements("ui.result.teamwin", "team", winner);
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.team", color: ChatManager.Red, "team", winner);
                } else {
                    // Winning player
                    var allPlayers = f.Filter<PlayerData>();
                    allPlayers.UseCulling = false;
                    while (allPlayers.NextUnsafe(out _, out PlayerData* data)) {
                        if (data->RealTeam == e.WinningTeam) {
                            RuntimePlayer runtimePlayer = f.GetPlayerData(data->PlayerRef);
                            winner = runtimePlayer?.PlayerNickname.ToValidNickname(f, data->PlayerRef);
                        }
                    }
                    resultText = tm.GetTranslationWithReplacements("ui.result.playerwin", "playername", winner);
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.player", color: ChatManager.Red, "playername", winner);
                }
                local = PlayerElements.AllPlayerElements.Any(pe => f.Unsafe.TryGetPointer(pe.Entity, out MarioPlayer* marioPlayer) && marioPlayer->GetTeam(f) == e.WinningTeam);
            } else {
                resultText = tm.GetTranslation("ui.result.draw");
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.draw", color: ChatManager.Red);
            }
            winText.text = resultText;

            SoundEffect resultMusic;
            string resultAnimationTrigger;
            if (e.EndedByHost) {
                resultMusic = SoundEffect.UI_Match_Cancel;
                resultAnimationTrigger = "startNoContest";
            } else if (!hasWinner) {
                resultMusic = SoundEffect.UI_Match_Draw;
                resultAnimationTrigger = "startNegative";
            } else if (hasWinner && local) {
                resultMusic = SoundEffect.UI_Match_Win;
                resultAnimationTrigger = "start";
            } else {
                resultMusic = SoundEffect.UI_Match_Lose;
                resultAnimationTrigger = "startNegative";
            }

            endGameSequenceCoroutine = StartCoroutine(EndGameSequence(resultMusic, resultAnimationTrigger, e.EndedByHost ? 0.5f : 1f));
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdatePingText();
        }

        public void OnReserveItemIconClicked() {
            if (QuantumRunner.DefaultGame == null) {
                return;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            int slotIndex = game.GetLocalPlayers().IndexOf(playerElements.Player);
            if (slotIndex != -1) {
                game.SendCommand(game.GetLocalPlayerSlots()[slotIndex], new CommandSpawnReserveItem());
            }
        }

        private void OnToggleHUD(InputAction.CallbackContext context) {
            if (NetworkHandler.Game.Frames.Predicted.Global->GameState < GameState.Ended) {
                toggler.alpha = (toggler.alpha > 0) ? 0 : 1;
            }
        }
    }
}
