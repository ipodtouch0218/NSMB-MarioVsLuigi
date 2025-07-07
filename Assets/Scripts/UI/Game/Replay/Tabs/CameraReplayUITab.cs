using NSMB.Cameras;
using NSMB.UI.MainMenu.Submenus.Prompts;
using NSMB.Utilities.Extensions;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Button = UnityEngine.UI.Button;
using Navigation = UnityEngine.UI.Navigation;

namespace NSMB.UI.Game.Replay {
    public class CameraReplayUITab : ReplayUITab {

        //---Serialized Variables
        [SerializeField] private GameObject template;

        //---Private Variables
        private bool initialized;
        
        public override void OnEnable() {
            if (!initialized) {
                Initialize();
                initialized = true;
                defaultSelection = selectables[1];
            }

            ApplyColor(true);
            base.OnEnable();
            parent.playerElements.OnCameraFocusChanged += OnCameraFocusChanged;
        }

        public override void OnDisable() {
            base.OnDisable();

            parent.playerElements.OnCameraFocusChanged -= OnCameraFocusChanged;
        }

        public unsafe void Initialize() {
            template.SetActive(false);
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;

            int selectableStartIndex = selectables.Count;
            for (int i = 0; i < f.Global->RealPlayers; i++) {
                ref PlayerInformation playerInfo = ref f.Global->PlayerInfo[i];

                GameObject newPlayer = Instantiate(template, transform);

                // Label
                SelectablePromptLabel label = newPlayer.GetComponent<SelectablePromptLabel>();
                label.translationKey = playerInfo.Nickname.ToString();
                label.UpdateLabel();

                // Navigation
                Button newBtn = label.GetComponent<Button>();
                Navigation newNav = newBtn.navigation;

                Button otherBtn = selectables[i + (selectableStartIndex - 1)].GetComponent<Button>();

                newNav.selectOnUp = otherBtn;
                newBtn.navigation = newNav;

                Navigation otherNav = otherBtn.navigation;
                otherNav.selectOnDown = newBtn;
                otherBtn.navigation = otherNav;

                // Functionality
                int finalI = i;
                newBtn.onClick.AddListener(() => ChangeFocus(finalI));

                // Done.
                newPlayer.SetActive(true);
                selectables.Add(newPlayer);
            }

            ApplyColor(true);
        }

        public unsafe void ChangeFocus(int index) {
            if (index == -1) {
                // Freecam
                parent.playerElements.CameraAnimator.Mode = CameraAnimator.CameraMode.Freecam;
                parent.playerElements.Entity = EntityRef.None;
                parent.playerElements.UpdateSpectateUI();
                GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
            } else {
                // Player index
                Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
                ref PlayerInformation playerInfo = ref f.Global->PlayerInfo[index];
                EntityRef marioEntity = FindMario(f, playerInfo.PlayerRef);

                if (f.Exists(marioEntity)) {
                    parent.playerElements.CameraAnimator.Mode = CameraAnimator.CameraMode.FollowPlayer;
                    parent.playerElements.Entity = marioEntity;
                    parent.playerElements.UpdateSpectateUI();
                    GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
                } else {
                    GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Error);
                }
            }

            defaultSelection = selectables[index + 1];
        }

        private unsafe EntityRef FindMario(Frame f, PlayerRef player) {
            var filter = f.Filter<MarioPlayer>();
            filter.UseCulling = false;
            while (filter.NextUnsafe(out EntityRef entity, out MarioPlayer* mario)) {
                if (mario->PlayerRef == player) {
                    return entity;
                }
            }
            return EntityRef.None;
        }

        private unsafe void ApplyColor(bool changeSelection) {
            int selectedIndex = -1;
            switch (parent.playerElements.CameraAnimator.Mode) {
            case CameraAnimator.CameraMode.Freecam:
                selectedIndex = 0;
                break;
            case CameraAnimator.CameraMode.FollowPlayer:
                Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
                for (int i = 0; i < f.Global->RealPlayers; i++) {
                    EntityRef mario = FindMario(f, f.Global->PlayerInfo[i].PlayerRef);
                    if (mario == parent.playerElements.Entity) {
                        selectedIndex = i + 1;
                        break;
                    }
                }
                break;
            }

            for (int i = 0; i < selectables.Count; i++) {
                selectables[i].GetComponentInChildren<TMP_Text>().color = (i == selectedIndex) ? enabledColor : disabledColor;
            }
            if (changeSelection && selectedIndex >= 0 && selectedIndex < selectables.Count) {
                EventSystem.current.SetSelectedGameObject(selectables[selectedIndex]);
            }
            defaultSelection = selectables[selectedIndex];
        }

        private void OnCameraFocusChanged() {
            ApplyColor(true);
        }
    }
}