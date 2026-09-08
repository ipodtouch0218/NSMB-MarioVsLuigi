using NSMB.Utilities;
using Quantum;
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

        public override void OnActivate(Frame f) {
            image.enabled = true;

            var mario = f.Unsafe.GetPointer<MarioPlayer>(targetEntity);
            image.color = Utils.GetPlayerColor(f, mario->PlayerRef);
            if (f.Global->Rules.TeamsEnabled && mario->GetTeam(f) is byte teamIndex) {
                var teams = f.Context.GetAllAssets<TeamAsset>();
                teamIcon.sprite = teams[teamIndex % teams.Count].spriteColorblind;
            } else {
                var slot = Utils.GetPlayerSlotInfo(f, mario->PlayerRef);
                if (slot) {
                    teamIcon.sprite = slot.Sprite;
                } else {
                    teamIcon.sprite = null;
                }
            }
        }

        public override void OnUpdateView() {
            bool controllingCamera = playerElements.CameraAnimator.Target == targetEntity;
            transform.localScale = controllingCamera ? FlipY : TwoThirds;

            if (!PredictedFrame.Unsafe.TryGetPointer(targetEntity, out MarioPlayer* mario)) {
                return;
            }

            if (mario->IsDead) {
                image.enabled = Utils.Blink(Time.time, 5f);
            } else {
                image.enabled = controllingCamera || !stage.HidePlayersOnMinimap;
            }

            teamIcon.gameObject.SetActive(image.enabled && Settings.Instance.GraphicsColorblind && !controllingCamera);
        }
    }
}
