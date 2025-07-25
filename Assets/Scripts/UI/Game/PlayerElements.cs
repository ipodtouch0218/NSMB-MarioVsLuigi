using NSMB.Cameras;
using NSMB.Entities.Player;
using NSMB.Quantum;
using NSMB.Sound;
using NSMB.UI.Game.Replay;
using NSMB.UI.Game.Scoreboard;
using NSMB.UI.Pause;
using NSMB.UI.Translation;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.UI.Game {
    public class PlayerElements : QuantumSceneViewComponent {

        //---Static Variables
        public static HashSet<PlayerElements> AllPlayerElements = new();
        public event Action OnCameraFocusChanged;

        //---Properties
        public PlayerRef Player { get; private set; }
        public EntityRef Entity { get; set; }
        public Canvas Canvas => canvas;
        public Camera Camera => ourCamera;
        public Camera ScrollCamera => scrollCamera;
        public Camera UICamera => uiCamera;
        public CameraAnimator CameraAnimator => cameraAnimator;
        public ReplayUI ReplayUi => replayUi;
        public PauseMenuManager PauseMenu => pauseMenu;
        public bool IsSpectating => spectating;

        //---Serialized Variables
        [SerializeField] private Canvas canvas;
        [SerializeField] private UIUpdater uiUpdater;
        [SerializeField] private CameraAnimator cameraAnimator;
        [SerializeField] private Camera ourCamera, scrollCamera, uiCamera;
        [SerializeField] private InputCollector inputCollector;
        [SerializeField] private ScoreboardUpdater scoreboardUpdater;
        [SerializeField] private ReplayUI replayUi;
        [SerializeField] private PauseMenuManager pauseMenu;

        [SerializeField] public GameObject spectationUI;
        [SerializeField] private TMP_Text spectatingText;
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
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public override void OnDeactivate() {
            AllPlayerElements.Remove(this);
            Settings.Controls.UI.Navigate.performed -= OnNavigate;
            Settings.Controls.UI.Navigate.canceled -= OnNavigate;
            Settings.Controls.UI.SpectatePlayerByIndex.performed -= SpectatePlayerIndex;
            Settings.Controls.UI.Next.performed -= SpectateNextPlayer;
            Settings.Controls.UI.Previous.performed -= SpectatePreviousPlayer;
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
                    SpectateNextPlayer(0, allowFreecam: false);
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
            } else {
                spectatingText.text = tm.GetTranslation("ui.replay.camera.freecam");
            }

            OnCameraFocusChanged?.Invoke();
            FindFirstObjectByType<MusicManager>().HandleMusic(Game, true);
        }

        public void StartSpectating() {
            spectating = true;
            spectationUI.SetActive(!IsReplay);
            if (!IsReplay) {
                if (GlobalController.Instance.loadingCanvas.isActiveAndEnabled) {
                    GlobalController.Instance.loadingCanvas.EndLoading(QuantumRunner.DefaultGame);
                }
            }

            SpectateNextPlayer(0, allowFreecam: false);
        }

        public void SpectateNextPlayer(InputAction.CallbackContext context) {
            if (!spectating) {
                return;
            }

            SpectateNextPlayer(1);
        }
        
        public void SpectateNextPlayer(int increment) {
            SpectateNextPlayer(increment, true);
        }

        public unsafe void SpectateNextPlayer(int increment, bool allowFreecam) {
            Frame f = PredictedFrame;

            int marioCount = f.ComponentCount<MarioPlayer>();
            if (marioCount <= 0) {
                return;
            }

            List<EntityRef> marios = new();
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;
            while (marioFilter.NextUnsafe(out EntityRef entity, out MarioPlayer* mario)) {
                marios.Add(entity);
            }
            marios.Sort((a, b) => {
                int indexA = int.MaxValue;
                int indexB = int.MaxValue;
                var marioA = f.Unsafe.GetPointer<MarioPlayer>(a);
                var marioB = f.Unsafe.GetPointer<MarioPlayer>(b);

                for (int i = 0; i < f.Global->RealPlayers; i++) {
                    PlayerRef player = f.Global->PlayerInfo[i].PlayerRef;
                    if (player == marioA->PlayerRef) {
                        indexA = i;
                    } else if (player == marioB->PlayerRef) {
                        indexB = i;
                    }
                }
                return indexA - indexB;
            });
            
            int currentIndex = marios.IndexOf(Entity);
            if (currentIndex < 0) currentIndex = 0;
            int nextIndex = (int) Mathf.Repeat(currentIndex + increment, marioCount + (allowFreecam ? 1 : 0)) + (allowFreecam ? 0 : 1);
            if (nextIndex == marioCount) {
                // Freecam
                CameraAnimator.Mode = CameraAnimator.CameraMode.Freecam;
                Entity = EntityRef.None;
            } else {
                // Follow Player
                CameraAnimator.Mode = CameraAnimator.CameraMode.FollowPlayer;
                Entity = marios[nextIndex];
            }

            UpdateSpectateUI();
        }

        public void SpectatePreviousPlayer(InputAction.CallbackContext context) {
            if (!spectating) {
                return;
            }

            SpectateNextPlayer(-1);
        }

        private void OnNavigate(InputAction.CallbackContext context) {
            /*
            if (!spectating) {
                return;
            }

            Vector2 newPosition = context.ReadValue<Vector2>();
            if (previousNavigate.x > -0.3f && newPosition.x <= -0.3f) {
                // Left
                SpectatePreviousPlayer();
            }
            if (previousNavigate.x < 0.3f && newPosition.x >= 0.3f) {
                // Right
                SpectateNextPlayer();
            }
            previousNavigate = newPosition;
            */
        }

        private unsafe void SpectatePlayerIndex(InputAction.CallbackContext context) {
            if (!spectating || Game.Frames.Predicted.Global->GameState >= GameState.Ended) {
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

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateSpectateUI();
        }
    }
}
