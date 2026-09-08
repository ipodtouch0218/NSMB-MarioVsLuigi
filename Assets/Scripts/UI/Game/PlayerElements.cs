using NSMB.Cameras;
using NSMB.Entities.Player;
using NSMB.Quantum;
using NSMB.Sound;
using NSMB.UI.Game.Replay;
using NSMB.UI.Game.Scoreboard;
using NSMB.UI.Pause;
using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.UI.Game {
    public unsafe class PlayerElements : QuantumSceneViewComponent {

        //---Static Variables
        public static HashSet<PlayerElements> AllPlayerElements = new();
        public event Action OnCameraFocusChanged;

        //---Properties
        public PlayerRef Player { get; private set; }
        public EntityRef Entity { get; set; }
        public Canvas Canvas => canvas;
        public Camera Camera => ourCamera;
        public Camera ScrollCamera => scrollCamera;
        public CameraAnimator CameraAnimator => cameraAnimator;
        public ReplayUI ReplayUi => replayUi;
        public PauseMenuManager PauseMenu => pauseMenu;
        public bool IsSpectating => spectating;

        //---Serialized Variables
        [SerializeField] private Canvas canvas;
        [SerializeField] private UIUpdater uiUpdater;
        [SerializeField] private CameraAnimator cameraAnimator;
        [SerializeField] private Camera ourCamera, scrollCamera;
        [SerializeField] private InputCollector inputCollector;
        [SerializeField] private ScoreboardUpdater scoreboardUpdater;
        [SerializeField] private ReplayUI replayUi;
        [SerializeField] private PauseMenuManager pauseMenu;

        [SerializeField] public GameObject spectationUI, spectatingArrows;
        [SerializeField] private TMP_Text spectatingText, spectateModeSwitchPrompt;
        [SerializeField] private PlayerNametag nametagPrefab;
        [SerializeField] public GameObject nametagCanvas;

        //---Private Variables
        private bool initialized;
        private bool spectating;
        private Vector2 previousNavigate;

        public void OnValidate() {
            this.SetIfNull(ref uiUpdater);
            this.SetIfNull(ref cameraAnimator);
            this.SetIfNull(ref ourCamera, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref inputCollector);
            this.SetIfNull(ref scoreboardUpdater, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref replayUi, UnityExtensions.GetComponentType.Children);
        }

        public override void OnActivate(Frame f) {
            AllPlayerElements.Add(this);
            Settings.Controls.UI.Navigate.performed += OnNavigate;
            Settings.Controls.UI.Navigate.canceled += OnNavigate;
            Settings.Controls.UI.SpectatePlayerByIndex.performed += SpectatePlayerIndex;
            Settings.Controls.UI.Next.performed += SpectateNextPlayer;
            Settings.Controls.UI.Previous.performed += SpectatePreviousPlayer;
            Settings.Controls.UI.Submit.performed += OnSubmit;
            Settings.OnNametagVisibilityChanged += OnNametagVisibilityChanged;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;

            if (Game.Session.IsReplay) {
                StartSpectating();
                
                if (PlayerPrefs.HasKey("id")) {
                    string userId = PlayerPrefs.GetString("id");
                    for (int i = 0; i < f.MaxPlayerCount; i++) {
                        RuntimePlayer player = f.GetPlayerData(i);
                        if (player == null || player.UserId != userId) {
                            continue;
                        }

                        foreach ((var entity, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                            if (mario->PlayerRef != i) {
                                continue;
                            }

                            // This is our player. Default to them.
                            Entity = entity;
                            cameraAnimator.Mode = CameraAnimator.CameraMode.FollowPlayer;
                            UpdateSpectateUI();
                            break;
                        }
                        break;
                    }
                }
            }
        }

        public override void OnDeactivate() {
            AllPlayerElements.Remove(this);
            Settings.Controls.UI.Navigate.performed -= OnNavigate;
            Settings.Controls.UI.Navigate.canceled -= OnNavigate;
            Settings.Controls.UI.SpectatePlayerByIndex.performed -= SpectatePlayerIndex;
            Settings.Controls.UI.Next.performed -= SpectateNextPlayer;
            Settings.Controls.UI.Previous.performed -= SpectatePreviousPlayer;
            Settings.Controls.UI.Submit.performed -= OnSubmit;
            Settings.OnNametagVisibilityChanged -= OnNametagVisibilityChanged;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Start() {
            nametagCanvas.SetActive(Settings.Instance.GraphicsPlayerNametags);
        }

        public void Initialize(QuantumGame game, Frame f, EntityRef entity, PlayerRef player) {
            Player = player;
            Entity = entity;

            Camera.transform.SetParent(null);
            Camera.transform.localScale = Vector3.one;
            uiUpdater.Initialize(game, f);
            scoreboardUpdater.Initialize();

            foreach (var mario in MarioPlayerAnimator.AllMarioPlayers) {
                MarioPlayerInitialized(game, f, mario);
            }
            initialized = true;
            MarioPlayerAnimator.MarioPlayerInitialized += MarioPlayerInitialized;
        }

        public void OnDestroy() {
            MarioPlayerAnimator.MarioPlayerInitialized -= MarioPlayerInitialized;
        }

        private void MarioPlayerInitialized(QuantumGame game, Frame f, MarioPlayerAnimator mario) {
            PlayerNametag newNametag = Instantiate(nametagPrefab, nametagPrefab.transform.parent);
            newNametag.Initialize(game, f, this, mario);
        }

        public override unsafe void OnUpdateView() {
            if (!initialized) {
                return;
            }

            Frame f = PredictedFrame;
            if (!f.Exists(Entity) && f.Global->GameState >= GameState.Starting && CameraAnimator.Mode == CameraAnimator.CameraMode.FollowPlayer) {
                if (spectating) {
                    // Find a new player to spectate
                    SpectateNextPlayer(0);
                } else {
                    // Spectating
                    StartSpectating();
                }
            }
        }

        public unsafe void UpdateSpectateUI() {
            if (!spectating) {
                return;
            }

            TranslationManager tm = GlobalController.Instance.translationManager;
            Frame f = PredictedFrame;
            if (f.Unsafe.TryGetPointer(Entity, out MarioPlayer* mario)) {
                string nickname = "noname";
                for (int i = 0; i < f.Global->RealPlayers; i++) {
                    if (f.Global->PlayerInfo[i].PlayerRef == mario->PlayerRef) {
                        nickname = f.Global->PlayerInfo[i].Nickname.ToString().ToValidNickname(f, mario->PlayerRef);
                        break;
                    }
                }

                spectatingText.text = tm.GetTranslationWithReplacements("ui.game.spectating", "playername", nickname);
                spectateModeSwitchPrompt.text = tm.GetTranslation("ui.replay.camera.freecam");
                spectatingArrows.SetActive(true);
            } else {
                spectatingText.text = tm.GetTranslation("ui.replay.camera.freecam");
                spectateModeSwitchPrompt.text = tm.GetTranslation("ui.game.players");
                spectatingArrows.SetActive(false);
            }

            OnCameraFocusChanged?.Invoke();

            if (f.Global->GameState == GameState.Playing) {
                FindFirstObjectByType<MusicManager>().HandleMusic(true);
            }
        }

        public void StartSpectating() {
            spectating = true;
            spectationUI.SetActive(!IsReplay);
            if (!IsReplay && GlobalController.Instance.loadingCanvas.isActiveAndEnabled) {
                GlobalController.Instance.loadingCanvas.EndLoading(QuantumRunner.DefaultGame);
            }

            SpectateNextPlayer(0);
        }

        public unsafe void SpectateNextPlayer(InputAction.CallbackContext context) {
            if (!spectating || cameraAnimator.Mode != CameraAnimator.CameraMode.FollowPlayer
                || pauseMenu.IsPaused || Game.Frames.Predicted.Global->GameState >= GameState.Ended) {
                return;
            }

            SpectateNextPlayer(1);
        }
        
        public unsafe void SpectateNextPlayer(int increment) {
            Frame f = PredictedFrame;

            int marioCount = f.ComponentCount<MarioPlayer>();
            if (marioCount <= 0) {
                return;
            }

            List<(EntityRef, PlayerRef)> marios = new();
            foreach ((var entity, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                marios.Add((entity, mario->PlayerRef));
            }
            marios.Sort((a, b) => {
                int indexA = int.MaxValue;
                int indexB = int.MaxValue;

                for (int i = 0; i < f.Global->RealPlayers; i++) {
                    PlayerRef player = f.Global->PlayerInfo[i].PlayerRef;
                    if (player == a.Item2) {
                        indexA = i;
                    } else if (player == b.Item2) {
                        indexB = i;
                    }
                }
                return indexA - indexB;
            });
            
            int currentIndex = marios.IndexOf(x => x.Item1 == Entity);
            int nextIndex = (int) Mathf.Repeat(currentIndex + increment, marioCount);
            CameraAnimator.Mode = CameraAnimator.CameraMode.FollowPlayer;
            Entity = marios[nextIndex].Item1;

            UpdateSpectateUI();
        }

        public unsafe void SpectatePreviousPlayer(InputAction.CallbackContext context) {
            if (!spectating || cameraAnimator.Mode != CameraAnimator.CameraMode.FollowPlayer
                || pauseMenu.IsPaused || Game.Frames.Predicted.Global->GameState >= GameState.Ended) {
                return;
            }

            SpectateNextPlayer(-1);
        }

        public bool IsOurCamera(Camera camera) {
            return camera == Camera || camera == ScrollCamera;
        }

        private unsafe void OnNavigate(InputAction.CallbackContext context) {
            if (!spectating || cameraAnimator.Mode != CameraAnimator.CameraMode.FollowPlayer
                || pauseMenu.IsPaused || Game.Frames.Predicted.Global->GameState >= GameState.Ended) {
                previousNavigate = Vector2.zero;
                return;
            }

            Vector2 newPosition = context.ReadValue<Vector2>();
            if (previousNavigate.x > -0.3f && newPosition.x <= -0.3f) {
                // Left
                SpectateNextPlayer(-1);
            }
            if (previousNavigate.x < 0.3f && newPosition.x >= 0.3f) {
                // Right
                SpectateNextPlayer(1);
            }
            previousNavigate = newPosition;
        }

        private unsafe void OnSubmit(InputAction.CallbackContext context) {
            if (!spectating || pauseMenu.IsPaused || Game.Session.IsReplay
                || Game.Frames.Predicted.Global->GameState >= GameState.Ended) {
                return;
            }

            // Change mode
            if (cameraAnimator.Mode == CameraAnimator.CameraMode.FollowPlayer) {
                cameraAnimator.Mode = CameraAnimator.CameraMode.Freecam;
                Entity = EntityRef.None;

            } else if (cameraAnimator.Mode == CameraAnimator.CameraMode.Freecam) {
                CameraAnimator.Mode = CameraAnimator.CameraMode.FollowPlayer;
                Entity = scoreboardUpdater.EntityAtPosition(0);
            }
            UpdateSpectateUI();
        }

        private unsafe void SpectatePlayerIndex(InputAction.CallbackContext context) {
            if (!spectating || Game.Frames.Predicted.Global->GameState >= GameState.Ended
                || pauseMenu.IsPaused) {
                return;
            }

            if (int.TryParse(context.control.name, out int index)) {
                index += 9;
                index %= 10;

                EntityRef newTarget = scoreboardUpdater.EntityAtPosition(index);
                if (newTarget != EntityRef.None) {
                    CameraAnimator.Mode = CameraAnimator.CameraMode.FollowPlayer;
                    Entity = newTarget;
                    UpdateSpectateUI();
                }
            }
        }

        private void OnNametagVisibilityChanged() {
            nametagCanvas.SetActive(Settings.Instance.GraphicsPlayerNametags);
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateSpectateUI();
        }
    }
}
