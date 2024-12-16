using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Translation;
using NSMB.UI.Game.Track;
using NSMB.Utils;
using Quantum;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game {
    public unsafe class UIUpdater : MonoBehaviour {

        //---Static Variables
        private static readonly int ParamIn = Animator.StringToHash("in");
        private static readonly int ParamOut = Animator.StringToHash("out");
        private static readonly int ParamHasItem = Animator.StringToHash("has-item");

        //---Properties
        public EntityRef Target => playerElements.Entity;

        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private TrackIcon playerTrackTemplate, starTrackTemplate;
        [SerializeField] private Sprite storedItemNull;
        [SerializeField] private TMP_Text uiTeamStars, uiStars, uiCoins, uiDebug, uiLives, uiCountdown;
        [SerializeField] private Image itemReserve, itemColor, deathFade;
        [SerializeField] private GameObject boos;
        [SerializeField] private Animator reserveAnimator;

        [SerializeField] private TMP_Text winText;
        [SerializeField] private Animator winTextAnimator;

        //---Private Variables
        private readonly Dictionary<MonoBehaviour, TrackIcon> entityTrackIcons = new();
        private readonly List<Image> backgrounds = new();
        private GameObject teamsParent, starsParent, coinsParent, livesParent, timerParent;
        private Material timerMaterial;
        private bool uiHidden;

        //private TeamManager teamManager;
        private int cachedCoins = -1, teamStars = -1, cachedStars = -1, cachedLives = -1, cachedTimer = -1;
        private PowerupAsset previousPowerup;
        private VersusStageData stage;
        private EntityRef previousTarget;

        private Coroutine endGameSequenceCoroutine;

        public void OnEnable() {
            MarioPlayerAnimator.MarioPlayerInitialized += OnMarioInitialized;
            MarioPlayerAnimator.MarioPlayerDestroyed += OnMarioDestroyed;
            BigStarAnimator.BigStarInitialized += OnBigStarInitialized;
            BigStarAnimator.BigStarDestroyed += OnBigStarDestroyed;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void OnDisable() {
            MarioPlayerAnimator.MarioPlayerInitialized -= OnMarioInitialized;
            MarioPlayerAnimator.MarioPlayerDestroyed -= OnMarioDestroyed;
            BigStarAnimator.BigStarInitialized -= OnBigStarInitialized;
            BigStarAnimator.BigStarDestroyed -= OnBigStarDestroyed;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Initialize(QuantumGame game, Frame f) {
            // Add existing MarioPlayer icons
            MarioPlayerAnimator.AllMarioPlayers.RemoveWhere(ma => ma == null);

            foreach (MarioPlayerAnimator mario in MarioPlayerAnimator.AllMarioPlayers) {
                OnMarioInitialized(game, f, mario);
            }
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
            PlayerTrackIcon.HideAllPlayerIcons = stage.HidePlayersOnMinimap;
            boos.SetActive(stage.HidePlayersOnMinimap);
            StartCoroutine(UpdatePingTextCoroutine());

            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
            QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
            QuantumEvent.Subscribe<EventTimerExpired>(this, OnTimerExpired);
            QuantumEvent.Subscribe<EventStartCameraFadeOut>(this, OnStartCameraFadeOut, NetworkHandler.FilterOutReplayFastForward);
        }

        public void OnUpdateView(CallbackUpdateView e) {
            QuantumGame game = e.Game;
            Frame f = game.Frames.Predicted;
            //UpdateTrackIcons(f);

            if (!Target.IsValid
                || !f.Unsafe.TryGetPointer(Target, out MarioPlayer* mario)) {
                return;
            }

            if (uiHidden) {
                ToggleUI(f, false);
            }

            UpdateStoredItemUI(mario, previousTarget == Target);
            UpdateTextUI(f, mario);
            ApplyUIColor(f, mario);

            previousTarget = Target;
        }

        private void OnMarioInitialized(QuantumGame game, Frame f, MarioPlayerAnimator mario) {
            entityTrackIcons[mario] = CreateTrackIcon(f, mario.EntityRef, mario.transform);
        }

        private void OnMarioDestroyed(QuantumGame game, Frame f, MarioPlayerAnimator mario) {
            if (entityTrackIcons.TryGetValue(mario, out TrackIcon icon)) {
                Destroy(icon.gameObject);
            }
        }

        private void OnBigStarInitialized(Frame f, BigStarAnimator star) {
            entityTrackIcons[star] = CreateTrackIcon(f, star.EntityRef, star.transform);
        }

        private void OnBigStarDestroyed(Frame f, BigStarAnimator star) {
            if (entityTrackIcons.TryGetValue(star, out TrackIcon icon)) {
                if (icon) {
                    Destroy(icon.gameObject);
                }
                entityTrackIcons.Remove(star);
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

        private void UpdateStoredItemUI(MarioPlayer* mario, bool playAnimation) {
            PowerupAsset powerup = QuantumUnityDB.GetGlobalAsset(mario->ReserveItem);
            reserveAnimator.SetBool(ParamHasItem, powerup && powerup.ReserveSprite);

            if (!powerup) {
                if (playAnimation && previousPowerup != powerup) {
                    reserveAnimator.SetTrigger(ParamOut);
                    previousPowerup = powerup;
                }
                return;
            }

            itemReserve.sprite = powerup.ReserveSprite ? powerup.ReserveSprite : storedItemNull;
            if (playAnimation && previousPowerup != powerup) {
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
            CanvasRenderer cr = uiCountdown.transform.GetChild(0).GetComponent<CanvasRenderer>();
            cr.SetMaterial(timerMaterial = new(cr.GetMaterial()), 0);
            timerMaterial.SetColor("_Color", Color.red);
        }

        private unsafe void UpdateTextUI(Frame f, MarioPlayer* mario) {

            var rules = f.Global->Rules;

            int starRequirement = rules.StarsToWin;
            int coinRequirement = rules.CoinsForPowerup;
            bool teamsEnabled = rules.TeamsEnabled;
            bool livesEnabled = rules.IsLivesEnabled;
            bool timerEnabled = rules.TimerSeconds > 0;

            if (rules.TeamsEnabled) {
                int teamIndex = mario->Team;
                teamStars = QuantumUtils.GetTeamStars(f, teamIndex);
                TeamAsset team = f.SimulationConfig.Teams[teamIndex];

                uiTeamStars.text = (Settings.Instance.GraphicsColorblind ? team.textSpriteColorblind : team.textSpriteNormal) + Utils.Utils.GetSymbolString("x" + teamStars + "/" + starRequirement);
            } else {
                teamsParent.SetActive(false);
            }

            if (mario->Stars != cachedStars) {
                cachedStars = mario->Stars;
                string starString = "Sx" + cachedStars;
                if (!teamsEnabled) {
                    starString += "/" + starRequirement;
                }

                uiStars.text = Utils.Utils.GetSymbolString(starString);
            }
            if (mario->Coins != cachedCoins) {
                cachedCoins = mario->Coins;
                uiCoins.text = Utils.Utils.GetSymbolString("Cx" + cachedCoins + "/" + coinRequirement);
            }

            if (livesEnabled) {
                if (mario->Lives != cachedLives) {
                    cachedLives = mario->Lives;
                    uiLives.text = QuantumUnityDB.GetGlobalAsset(mario->CharacterAsset).UiString + Utils.Utils.GetSymbolString("x" + cachedLives);
                }
            } else {
                livesParent.SetActive(false);
            }

            if (timerEnabled) {
                float timeRemaining = f.Global->Timer.AsFloat;
                int secondsRemaining = Mathf.Max(Mathf.CeilToInt(timeRemaining), 0);

                if (secondsRemaining != cachedTimer) {
                    cachedTimer = secondsRemaining;
                    uiCountdown.text = Utils.Utils.GetSymbolString("Tx" + Utils.Utils.SecondsToMinuteSeconds(secondsRemaining));
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
            if (NetworkHandler.Client.InRoom) {
                int ping = (int) NetworkHandler.Ping.Value;
                uiDebug.text = "<mark=#000000b0 padding=\"16,16,10,10\"><font=\"MarioFont\">" + Utils.Utils.GetPingSymbol(ping) + ping;
                //uiDebug.isRightToLeftText = GlobalController.Instance.translationManager.RightToLeft;
            } else {
                uiDebug.enabled = false;
            }
        }

        private unsafe void ApplyUIColor(Frame f, MarioPlayer* mario) {
            Color color = f.Global->Rules.TeamsEnabled ? Utils.Utils.GetTeamColor(f, mario->Team, 0.8f, 1f) : stage.UIColor;

            foreach (Image bg in backgrounds) {
                bg.color = color;
            }

            itemColor.color = color;
        }

        private IEnumerator EndGameSequence(SoundEffect resultMusic, string resultAnimationTrigger) {
            // Wait one second before playing the music 
            yield return new WaitForSecondsRealtime(1);

            GlobalController.Instance.sfx.PlayOneShot(resultMusic);
            winTextAnimator.SetTrigger(resultAnimationTrigger);
            winText.enabled = true;
        }

        //---Callbacks
        private void OnGameResynced(CallbackGameResynced e) {
            if (endGameSequenceCoroutine != null) {
                StopCoroutine(endGameSequenceCoroutine);
                endGameSequenceCoroutine = null;

                // (Potentially) stop the win/loss
                GlobalController.Instance.sfx.Stop();

                winText.enabled = false;
                winTextAnimator.Play("Empty");
            }
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.Starting) {
                foreach (var mario in MarioPlayerAnimator.AllMarioPlayers) {
                    entityTrackIcons[mario] = CreateTrackIcon(e.Frame, mario.EntityRef, mario.transform);
                }
            }
        }

        private void OnGameEnded(EventGameEnded e) {
            Frame f = e.Frame;
            bool teamMode = f.Global->Rules.TeamsEnabled;
            bool hasWinner = e.HasWinner;

            TranslationManager tm = GlobalController.Instance.translationManager;
            TeamAsset[] allTeams = f.SimulationConfig.Teams;
            string resultText;
            string winner = null;
            bool local;

            if (hasWinner) {
                if (teamMode) {
                    // Winning team
                    winner = tm.GetTranslation(allTeams[e.WinningTeam].nameTranslationKey);
                    resultText = tm.GetTranslationWithReplacements("ui.result.teamwin", "team", winner);
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.team", color: ChatManager.Red, "team", winner);
                } else {
                    // Winning player
                    var allPlayers = f.Filter<PlayerData>();
                    allPlayers.UseCulling = false;
                    while (allPlayers.NextUnsafe(out _, out PlayerData* data)) {
                        if (data->Team == e.WinningTeam) {
                            RuntimePlayer runtimePlayer = f.GetPlayerData(data->PlayerRef);
                            winner = runtimePlayer?.PlayerNickname.ToValidUsername(f, data->PlayerRef);
                        }
                    }
                    resultText = tm.GetTranslationWithReplacements("ui.result.playerwin", "playername", winner);
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.player", color: ChatManager.Red, "playername", winner);
                }
                local = PlayerElements.AllPlayerElements.Any(pe => f.Unsafe.TryGetPointer(pe.Entity, out MarioPlayer* marioPlayer) && marioPlayer->Team == e.WinningTeam);
            } else {
                resultText = tm.GetTranslation("ui.result.draw");
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.draw", color: ChatManager.Red);
                local = false;
            }
            winText.text = resultText;

            SoundEffect resultMusic;
            string resultAnimationTrigger;
            if (!hasWinner) {
                resultMusic = SoundEffect.UI_Match_Draw;
                resultAnimationTrigger = "startNegative";
            } else if (hasWinner && local) {
                resultMusic = SoundEffect.UI_Match_Win;
                resultAnimationTrigger = "start";
            } else {
                resultMusic = SoundEffect.UI_Match_Lose;
                resultAnimationTrigger = "startNegative";
            }

            endGameSequenceCoroutine = StartCoroutine(EndGameSequence(resultMusic, resultAnimationTrigger));
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
    }
}
