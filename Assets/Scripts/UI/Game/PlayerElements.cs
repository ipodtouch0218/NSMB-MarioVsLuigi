using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Translation;
using NSMB.UI.Game.Scoreboard;
using NSMB.Utils;
using Quantum;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.Game {
    public class PlayerElements : MonoBehaviour {

        public static HashSet<PlayerElements> AllPlayerElements = new();

        //---Properties
        public PlayerRef Player => player;
        public EntityRef Entity => spectating ? spectatingEntity : entity;
        public Camera ScrollCamera => scrollCamera;
        public Camera Camera => ourCamera;
        public CameraAnimator CameraAnimator => cameraAnimator;
        public ReplayUI ReplayUi => replayUi;

        //---Serialized Variables
        [SerializeField] private RawImage image;
        [SerializeField] private UIUpdater uiUpdater;
        [SerializeField] private CameraAnimator cameraAnimator;
        [SerializeField] private Camera ourCamera, scrollCamera;
        [SerializeField] private InputCollector inputCollector;
        [SerializeField] private ScoreboardUpdater scoreboardUpdater;
        [SerializeField] private ReplayUI replayUi;

        [SerializeField] private GameObject spectationUI;
        [SerializeField] private TMP_Text spectatingText;
        [SerializeField] private PlayerNametag nametagPrefab;
        [SerializeField] public GameObject nametagCanvas;

        //---Private Variables
        private PlayerRef player;
        private EntityRef entity;

        private bool spectating;
        private EntityRef spectatingEntity;
        private Vector2 previousNavigate;

        public void OnValidate() {
            this.SetIfNull(ref image);
            this.SetIfNull(ref uiUpdater);
            this.SetIfNull(ref cameraAnimator);
            this.SetIfNull(ref ourCamera, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref inputCollector);
            this.SetIfNull(ref scoreboardUpdater, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref replayUi, UnityExtensions.GetComponentType.Children);
        }

        public void OnEnable() {
            AllPlayerElements.Add(this);
            ControlSystem.controls.UI.Navigate.performed += OnNavigate;
            ControlSystem.controls.UI.SpectatePlayerByIndex.performed += SpectatePlayerIndex;
            ControlSystem.controls.UI.Next.performed += SpectateNextPlayer;
            ControlSystem.controls.UI.Previous.performed += SpectatePreviousPlayer;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public void OnDisable() {
            AllPlayerElements.Remove(this);
            ControlSystem.controls.UI.Navigate.performed -= OnNavigate;
            ControlSystem.controls.UI.SpectatePlayerByIndex.performed -= SpectatePlayerIndex;
            ControlSystem.controls.UI.Next.performed -= SpectateNextPlayer;
            ControlSystem.controls.UI.Previous.performed -= SpectatePreviousPlayer;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Start() {
            nametagCanvas.SetActive(Settings.Instance.GraphicsPlayerNametags);
            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
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
            MarioPlayerAnimator.MarioPlayerInitialized += MarioPlayerInitialized;
        }

        public void OnDestroy() {
            MarioPlayerAnimator.MarioPlayerInitialized -= MarioPlayerInitialized;
        }

        private void MarioPlayerInitialized(QuantumGame game, Frame f, MarioPlayerAnimator mario) {
            PlayerNametag newNametag = Instantiate(nametagPrefab, nametagPrefab.transform.parent);
            newNametag.Initialize(game, f, this, mario);
        }

        public void OnUpdateView(CallbackUpdateView e) {
            Frame f = e.Game.Frames.Predicted;

            if (!spectating && !f.Exists(entity)) {
                // Spectating
                StartSpectating();
            }

            if (spectating && !f.Exists(spectatingEntity)) {
                // Find a new player to spectate
                SpectateNextPlayer();
            }
        }

        public unsafe void UpdateSpectateUI() {
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(spectatingEntity);

            RuntimePlayer runtimePlayer = f.GetPlayerData(mario->PlayerRef);
            string username = runtimePlayer.PlayerNickname.ToValidUsername(f, mario->PlayerRef);

            TranslationManager tm = GlobalController.Instance.translationManager;
            spectatingText.text = tm.GetTranslationWithReplacements("ui.game.spectating", "playername", username);
        }

        public void StartSpectating() {
            spectating = true;

            if (!NetworkHandler.IsReplay) {
                spectationUI.SetActive(true);
            }

            if (GlobalController.Instance.loadingCanvas.isActiveAndEnabled) {
                GlobalController.Instance.loadingCanvas.EndLoading(QuantumRunner.DefaultGame);
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
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;

            int marioCount = f.ComponentCount<MarioPlayer>();
            if (marioCount <= 0) {
                return;
            }

            Span<EntityRef> marios = stackalloc EntityRef[marioCount];
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;

            int index = 0;
            while (marioFilter.NextUnsafe(out EntityRef entity, out _)) {
                marios[index++] = entity;
            }

            int currentIndex = -1;
            for (int i = 0; i < marioCount; i++) {
                if (spectatingEntity == marios[i]
                    || marios[i].Index > spectatingEntity.Index) {

                    currentIndex = i;
                    break;
                }
            }
            spectatingEntity = marios[(currentIndex + 1) % marioCount];
            UpdateSpectateUI();
        }

        public void SpectatePreviousPlayer(InputAction.CallbackContext context) {
            if (!spectating) {
                return;
            }

            SpectateNextPlayer();
        }

        public unsafe void SpectatePreviousPlayer() {
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;

            int marioCount = f.ComponentCount<MarioPlayer>();
            if (marioCount <= 0) {
                return;
            }

            Span<EntityRef> marios = stackalloc EntityRef[marioCount];
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;

            int index = 0;
            while (marioFilter.NextUnsafe(out EntityRef entity, out _)) {
                marios[index++] = entity;
            }

            int currentIndex = -1;
            for (int i = marioCount - 1; i >= 0; i--) {
                if (spectatingEntity == marios[i]
                    || marios[i].Index < spectatingEntity.Index) {

                    currentIndex = i;
                    break;
                }
            }
            spectatingEntity = marios[(currentIndex - 1 + marioCount) % marioCount];
            UpdateSpectateUI();
        }

        private void OnNavigate(InputAction.CallbackContext context) {
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
