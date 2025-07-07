using NSMB.Utilities.Extensions;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.Game.Replay {
    public class PlaybackSpeedReplayUITab : ReplayUITab {

        //---Serialized Variables
        [SerializeField] private float[] speeds = { 0.25f, 0.5f, 1f, 2f, 4f };
        [SerializeField] private GameObject[] speedButtons;

        public override void OnEnable() {
            base.OnEnable();
            EventSystem.current.SetSelectedGameObject(speedButtons[Array.IndexOf(speeds, parent.ReplaySpeed)]);
            ApplyColor();
        }

        public void ChangePlaybackSpeedViaIndex(int index) {
            parent.ChangeReplaySpeed(index);
            EventSystem.current.SetSelectedGameObject(speedButtons[index]);
            GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
            ApplyColor();
        }

        public void ApplyColor() {
            int selectedIndex = Array.IndexOf(speeds, parent.ReplaySpeed);
            for (int i = 0; i < speeds.Length; i++) {
                speedButtons[i].GetComponentInChildren<TMP_Text>().color = (i == selectedIndex) ? enabledColor : disabledColor;
            }
        }
    }
}