using NSMB.Utilities;
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

        public override void OnActivate(Frame f) {
            image.enabled = true;

            var mario = f.Unsafe.GetPointer<MarioPlayer>(targetEntity);
            image.color = Utils.GetPlayerColor(f, mario->PlayerRef);
            if (f.Global->Rules.TeamsEnabled && mario->GetTeam(f) is byte teamIndex) {
                var teams = f.SimulationConfig.Teams;
                teamIcon.sprite = f.FindAsset(teams[teamIndex % teams.Length]).spriteColorblind;
            }
        }

        public override void OnDeactivate() {
            if (flashRoutine != null) {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }
        }

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
            QuantumEvent.Subscribe<EventMarioPlayerRespawned>(this, OnMarioPlayerRespawned);
        }

        public override void OnUpdateView() {
            bool controllingCamera = playerElements.CameraAnimator.Target == targetEntity;
            transform.localScale = controllingCamera ? FlipY : TwoThirds;

            Frame f = PredictedFrame;
            if (flashRoutine == null) {
                image.enabled = controllingCamera || !stage.HidePlayersOnMinimap;
            }
            teamIcon.gameObject.SetActive(image.enabled && Settings.Instance.GraphicsColorblind && f.Global->Rules.TeamsEnabled && !controllingCamera);
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

            if (flashRoutine == null) {
                flashRoutine = StartCoroutine(Flash());
            }
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
