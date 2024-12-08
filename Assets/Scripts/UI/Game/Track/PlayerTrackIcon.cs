using NSMB.Utils;
using Quantum;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game.Track {
    public unsafe class PlayerTrackIcon : TrackIcon {

        //---Static Variables
        public static bool HideAllPlayerIcons = false;
        private static readonly Vector3 TwoThirds = Vector3.one * (2f / 3f);
        private static readonly Vector3 FlipY = new(1f, -1f, 1f);
        private static readonly WaitForSeconds FlashWait = new(0.1f);

        //---Serialized Variables
        [SerializeField] private GameObject allImageParent;
        [SerializeField] private Image teamIcon;

        //---Private Variables
        private Coroutine flashRoutine;

        public void OnEnable() {
            image.enabled = true;
        }

        public void OnDisable() {
            if (flashRoutine != null) {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }
        }

        public void Start() {
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;

            var mario = f.Unsafe.GetPointer<MarioPlayer>(targetEntity);
            image.color = Utils.Utils.GetPlayerColor(f, mario->PlayerRef);
            if (f.Global->Rules.TeamsEnabled) {
                teamIcon.sprite = f.SimulationConfig.Teams[mario->Team].spriteColorblind;
            }

            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
            QuantumEvent.Subscribe<EventMarioPlayerRespawned>(this, OnMarioPlayerRespawned);
        }

        public void OnUpdateView(CallbackUpdateView e) {
            QuantumGame game = e.Game;
            bool controllingCamera = playerElements.CameraAnimator.Target == targetEntity;
            transform.localScale = controllingCamera ? FlipY : TwoThirds;

            Frame f = game.Frames.Predicted;
            image.enabled &= controllingCamera || !stage.HidePlayersOnMinimap;
            teamIcon.gameObject.SetActive(Settings.Instance.GraphicsColorblind && f.Global->Rules.TeamsEnabled && !controllingCamera);
        }

        private void OnGameResynced(CallbackGameResynced e) {
            // TODO: do proper if statements to start the flashing if needed?
            // eh. probably not needed.
            if (flashRoutine != null) {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }
        }

        private IEnumerator Flash() {
            while (true) {
                image.enabled = !image.enabled;
                yield return FlashWait;
            }
        }

        public void OnMarioPlayerDied(EventMarioPlayerDied e) {
            if (e.Entity != targetEntity) {
                return;
            }

            flashRoutine = StartCoroutine(Flash());
        }

        public void OnMarioPlayerRespawned(EventMarioPlayerRespawned e) {
            if (e.Entity != targetEntity) {
                return;
            }

            image.enabled = true;
            if (flashRoutine != null) {
                StopCoroutine(flashRoutine);
            }
            flashRoutine = null;
        }
    }
}
