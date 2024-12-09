using NSMB.Extensions;
using NSMB.Utils;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.UI.MainMenu.Submenus {
    public class InRoomSubmenu : MainMenuSubmenu {

        //---Properties
        public override float BackHoldTime => colorPalettePicker.activeSelf ? 0f : 1f;
        public unsafe override Color? HeaderColor {
            get {
                const int rngSeed = 2035767;
                Frame f = NetworkHandler.Runner.Game.Frames.Predicted;
                PlayerRef host = QuantumUtils.GetHostPlayer(f, out _);
                RuntimePlayer playerData = f.GetPlayerData(host);
                string hostname;

                if (playerData == null) {
                    // Assume we're the host...
                    hostname = Settings.Instance.generalNickname.ToValidUsername(f, host);
                } else {
                    hostname = playerData.PlayerNickname.ToValidUsername(f, host);
                }

                Random.InitState(hostname.GetHashCode() + rngSeed);
                return Random.ColorHSV(0f, 1f, 0.5f, 1f, 0f, 1f);
            }
        }
        public unsafe override string Header {
            get {
                Frame f = NetworkHandler.Runner.Game.Frames.Predicted;
                PlayerRef host = QuantumUtils.GetHostPlayer(f, out _);
                RuntimePlayer playerData = f.GetPlayerData(host);
                string hostname;

                if (playerData == null) {
                    // Assume we're the host...
                    hostname = Settings.Instance.generalNickname.ToValidUsername(f, host);
                } else {
                    hostname = playerData.PlayerNickname.ToValidUsername(f, host);
                }

                return GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.rooms.listing.name", "playername", hostname);
            }
        }

        //---Serialized Variables
        [SerializeField] private InRoomPanel defaultSelectedPanel;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private List<InRoomPanel> allPanels;
        [SerializeField] private GameObject colorPalettePicker;

        //---Private Variables
        private InRoomPanel selectedPanel;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
        }

        public override void Initialize(MainMenuCanvas canvas) {
            base.Initialize(canvas);
            QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
        }

        public void OnEnable() {
            ControlSystem.controls.UI.Next.performed += OnNextPerformed;
            ControlSystem.controls.UI.Previous.performed += OnPreviousPerformed;

            foreach (var panel in allPanels) {
                panel.Deselect();
            }
            selectedPanel = defaultSelectedPanel;
            selectedPanel.Select(true);
        }

        public void OnDisable() {
            ControlSystem.controls.UI.Next.performed -= OnNextPerformed;
            ControlSystem.controls.UI.Previous.performed -= OnPreviousPerformed;
        }

        public void SelectPanel(InRoomPanel panel) {
            SelectPanel(panel, true);
        }

        public void SelectPanel(InRoomPanel panel, bool setDefault) {
            if (panel == selectedPanel) {
                return;
            }

            if (selectedPanel) {
                selectedPanel.Deselect();
            }
            selectedPanel = panel;
            selectedPanel.Select(setDefault);

            sfx.Play();
        }

        public void SelectPreviousPanel() {
            if (selectedPanel && selectedPanel.leftPanel) {
                SelectPanel(selectedPanel.leftPanel, true);
            }
        }

        public void SelectNextPanel() {
            if (selectedPanel && selectedPanel.rightPanel) {
                SelectPanel(selectedPanel.rightPanel, true);
            }
        }

        //---Buttons
        public void OnCharacterButtonClicked() {

        }

        //---Callbacks
        private void OnPreviousPerformed(InputAction.CallbackContext context) {
            if (!context.performed) {
                return;
            }

            SelectPreviousPanel();
        }

        private void OnNextPerformed(InputAction.CallbackContext context) {
            if (!context.performed) {
                return;
            }

            SelectNextPanel();
        }

        private void OnGameStarted(CallbackGameStarted e) {

        }
    }
}
