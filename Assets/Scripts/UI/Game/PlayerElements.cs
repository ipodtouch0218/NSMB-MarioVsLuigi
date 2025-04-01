using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Translation;
using NSMB.UI.Game.Scoreboard;
using NSMB.Utils;
using Quantum;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NSMB.UI.Game {
    public class PlayerElements : QuantumSceneViewComponent {

        public static HashSet<PlayerElements> AllPlayerElements = new();

        //---Properties
        public PlayerRef Player => player;
        public EntityRef Entity => spectating ? spectatingEntity : entity;
        public Camera ScrollCamera => scrollCamera;
        public Camera Camera => ourCamera;
        public CameraAnimator CameraAnimator => cameraAnimator;
        public ReplayUI ReplayUi => replayUi;
        public bool IsSpectating => spectating;

        //---Serialized Variables
        [SerializeField] private UIUpdater uiUpdater;
        [SerializeField] private CameraAnimator cameraAnimator;
        [SerializeField] private Camera ourCamera, scrollCamera;
        [SerializeField] private InputCollector inputCollector;
        [SerializeField] private ScoreboardUpdater scoreboardUpdater;
        [SerializeField] private ReplayUI replayUi;

        [SerializeField] public GameObject spectationUI;
        [SerializeField] private TMP_Text spectatingText;
        [SerializeField] private PlayerNametag nametagPrefab;
        [SerializeField] public GameObject nametagCanvas;

        //---Private Variables
        private PlayerRef player;
        private EntityRef entity;

        private bool initialized;
        private bool spectating;
        private EntityRef spectatingEntity;
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
            Settings.Controls.UI.SpectatePlayerByIndex.performed += SpectatePlayerIndex;
            Settings.Controls.UI.Next.performed += SpectateNextPlayer;
            Settings.Controls.UI.Previous.performed += SpectatePreviousPlayer;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public override void OnDeactivate() {
            AllPlayerElements.Remove(this);
            Settings.Controls.UI.Navigate.performed -= OnNavigate;
            Settings.Controls.UI.SpectatePlayerByIndex.performed -= SpectatePlayerIndex;
            Settings.Controls.UI.Next.performed -= SpectateNextPlayer;
            Settings.Controls.UI.Previous.performed -= SpectatePreviousPlayer;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Start() {
            nametagCanvas.SetActive(Settings.Instance.GraphicsPlayerNametags);
        }

        public void Initialize(QuantumGame game, Frame f, EntityRef entity, PlayerRef player) {
            this.player = player;
            this.entity = entity;

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
            if (!spectating && !f.Exists(entity) && f.Global->GameState >= GameState.Starting) {
                // Spectating
                StartSpectating();
            }

            if (spectating && !f.Exists(spectatingEntity)) {
                // Find a new player to spectate
                SpectateNextPlayer();
            }
        }

        public unsafe void UpdateSpectateUI() {
            if (!spectating) {
                return;
            }

            Frame f = PredictedFrame;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(spectatingEntity);

            RuntimePlayer runtimePlayer = f.GetPlayerData(mario->PlayerRef);
            string username = runtimePlayer.PlayerNickname.ToValidUsername(f, mario->PlayerRef);

            TranslationManager tm = GlobalController.Instance.translationManager;
            spectatingText.text = tm.GetTranslationWithReplacements("ui.game.spectating", "playername", username);
        }

        public void StartSpectating() {
            spectating = true;
            spectationUI.SetActive(true);
            if (!NetworkHandler.IsReplay) {
                if (GlobalController.Instance.loadingCanvas.isActiveAndEnabled) {
                    GlobalController.Instance.loadingCanvas.EndLoading(NetworkHandler.Game);
                }
            }

            SpectateNextPlayer();
        }

        public void SpectateNextPlayer(InputAction.CallbackContext context) {
            if (!spectating) {
                return;
            }

            SpectateNextPlayer();
        }

        public unsafe void SpectateNextPlayer() {
            Frame f = PredictedFrame;

            int marioCount = f.ComponentCount<MarioPlayer>();
            if (marioCount <= 0) {
                return;
            }

            List<EntityRef> marios = new(marioCount);
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;
            while (marioFilter.NextUnsafe(out EntityRef entity, out _)) {
                marios.Add(entity);
            }
            marios.Sort((a, b) => {
                return a.Index - b.Index;
            });
            int currentIndex = marios.IndexOf(spectatingEntity);
            spectatingEntity = marios[(currentIndex + 1) % marioCount];
            UpdateSpectateUI();
        }

        public void SpectatePreviousPlayer(InputAction.CallbackContext context) {
            if (!spectating) {
                return;
            }

            SpectatePreviousPlayer();
        }

        public unsafe void SpectatePreviousPlayer() {
            Frame f = PredictedFrame;

            int marioCount = f.ComponentCount<MarioPlayer>();
            if (marioCount <= 0) {
                return;
            }

            List<EntityRef> marios = new(marioCount);
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;
            while (marioFilter.NextUnsafe(out EntityRef entity, out _)) {
                marios.Add(entity);
            }
            marios.Sort((a, b) => {
                return a.Index - b.Index;
            });
            int currentIndex = marios.IndexOf(spectatingEntity);
            spectatingEntity = marios[(currentIndex - 1 + marioCount) % marioCount];
            UpdateSpectateUI();
        }

        private void OnNavigate(InputAction.CallbackContext context) {
            if (!spectating || EventSystem.current != spectationUI) {
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
        }

        private void SpectatePlayerIndex(InputAction.CallbackContext context) {
            if (!spectating) {
                return;
            }

            if (int.TryParse(context.control.name, out int index)) {
                index += 9;
                index %= 10;

                EntityRef newTarget = scoreboardUpdater.EntityAtPosition(index);
                if (newTarget != EntityRef.None) {
                    spectatingEntity = newTarget;
                    UpdateSpectateUI();
                }
            }
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateSpectateUI();
        }
    }
}
