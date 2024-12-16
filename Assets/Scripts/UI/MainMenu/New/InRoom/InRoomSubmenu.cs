using NSMB.Extensions;
using NSMB.Translation;
using NSMB.Utils;
using Quantum;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.UI.MainMenu.Submenus {
    public class InRoomSubmenu : MainMenuSubmenu {

        //---Properties
        public override float BackHoldTime => teamChooser.content.activeSelf || paletteChooser.content.activeSelf || playerDropdownOpen ? 0f : 1f;
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
        [SerializeField] private InRoomSubmenuPanel defaultSelectedPanel;
        [SerializeField] private AudioSource sfx, musicSource;
        [SerializeField] private List<InRoomSubmenuPanel> allPanels;
        [SerializeField] private PlayerListHandler playerListHandler;
        [SerializeField] private PaletteChooser paletteChooser;
        [SerializeField] private TeamChooser teamChooser;
        [SerializeField] private TMP_Text startGameButtonText;
        [SerializeField] private Clickable startGameButton;
        [SerializeField] private MainMenuChat mainMenuChat;

        //---Private Variables
        private InRoomSubmenuPanel selectedPanel;
        private PlayerListEntry playerDropdownOpen;
        private int lastCountdownStartFrame;
        private bool invalidStart;
        private Coroutine fadeMusicCoroutine;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
        }

        public override void Initialize(MainMenuCanvas canvas) {
            base.Initialize(canvas);
            foreach (var panel in allPanels) {
                panel.Initialize();
            }
            playerListHandler.Initialize();
            paletteChooser.Initialize();
            teamChooser.Initialize();
            mainMenuChat.Initialize();

            QuantumCallback.Subscribe<CallbackLocalPlayerAddConfirmed>(this, OnLocalPlayerAddConfirmed);
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumEvent.Subscribe<EventStartingCountdownChanged>(this, OnStartingCountdownChanged);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
            QuantumEvent.Subscribe<EventHostChanged>(this, OnHostChanged);
            QuantumEvent.Subscribe<EventCountdownTick>(this, OnCountdownTick);
            QuantumEvent.Subscribe<EventPlayerDataChanged>(this, OnPlayerDataChanged);
        }

        public void OnEnable() {
            ControlSystem.controls.UI.Next.performed += OnNextPerformed;
            ControlSystem.controls.UI.Previous.performed += OnPreviousPerformed;
            PlayerListEntry.PlayerEntryDropdownChanged += OnPlayerEntryDropdownChanged;

            foreach (var panel in allPanels) {
                panel.Deselect();
            }
            selectedPanel = defaultSelectedPanel;
            selectedPanel.Select(true);
            playerDropdownOpen = null;
        }

        public void OnDisable() {
            ControlSystem.controls.UI.Next.performed -= OnNextPerformed;
            ControlSystem.controls.UI.Previous.performed -= OnPreviousPerformed;
            PlayerListEntry.PlayerEntryDropdownChanged -= OnPlayerEntryDropdownChanged;
        }

        public override bool TryGoBack(out bool playSound) {
            if (playerDropdownOpen) {
                playerDropdownOpen.HideDropdown(false);
                playSound = false;
                return false;
            }

            if (teamChooser.content.activeSelf) {
                teamChooser.Close(true);
                playSound = false;
                return false;
            }

            if (paletteChooser.content.activeSelf) {
                paletteChooser.Close(true);
                playSound = false;
                return false;
            }

            _ = NetworkHandler.ConnectToRegion(null);
            return base.TryGoBack(out playSound);
        }

        public void SelectPanel(InRoomSubmenuPanel panel) {
            SelectPanel(panel, true);
        }

        public void SelectPanel(InRoomSubmenuPanel panel, bool setDefault) {
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

        //---Helpers
        private unsafe void UpdateStartButton(QuantumGame game, Frame f, int? seconds = null) {
            TranslationManager tm = GlobalController.Instance.translationManager;
            bool isHost = game.PlayerIsLocal(QuantumUtils.GetHostPlayer(f, out _));
            seconds ??= f.Global->GameStartFrames / 60;

            if (seconds <= 0) {
                // Cancelled
                startGameButton.Interactable = !isHost || QuantumUtils.IsGameStartable(f);
                if (isHost) {
                    startGameButtonText.text = tm.GetTranslation("ui.inroom.buttons.start");
                } else {
                    // Check if we're readyed-up
                    bool ready;
                    if (game.GetLocalPlayers().Count > 0) {
                        PlayerRef localPlayer = game.GetLocalPlayers()[0];
                        var localPlayerData = QuantumUtils.GetPlayerData(f, localPlayer);
                        ready = localPlayerData != null ? localPlayerData->IsReady : false;
                    } else {
                        ready = false;
                    }
                    startGameButtonText.text = tm.GetTranslation(ready ? "ui.inroom.buttons.unready" : "ui.inroom.buttons.readyup");
                }

                if (fadeMusicCoroutine != null) {
                    StopCoroutine(fadeMusicCoroutine);
                    fadeMusicCoroutine = null;
                    musicSource.volume = 1;
                }
            } else {
                // Starting
                startGameButton.Interactable = isHost;
                startGameButtonText.text = tm.GetTranslationWithReplacements("ui.inroom.buttons.starting", "countdown", seconds.ToString());

                if (seconds == 1) {
                    // Start fade
                    fadeMusicCoroutine = StartCoroutine(FadeMusic());
                }
            }
        }

        private IEnumerator FadeMusic() {
            while (musicSource.volume > 0) {
                musicSource.volume -= Time.deltaTime;
                yield return null;
            }
            fadeMusicCoroutine = null;
        }

        //---Buttons
        public unsafe void OnStartGameButtonClicked() {
            QuantumGame game = NetworkHandler.Runner.Game;
            Frame f = game.Frames.Predicted;
            PlayerRef host = QuantumUtils.GetHostPlayer(f, out _);

            if (game.PlayerIsLocal(host)) {
                // Start (or cancel) the game countdown
                int slot = game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)];
                game.SendCommand(slot, new CommandToggleCountdown());
            } else {
                // Ready (or unready) up
                foreach (int slot in game.GetLocalPlayerSlots()) {
                    game.SendCommand(slot, new CommandToggleReady());
                }
            }
        }

        //---Callbacks
        private void OnPreviousPerformed(InputAction.CallbackContext context) {
            if (!context.performed || playerDropdownOpen) {
                return;
            }

            SelectPreviousPanel();
        }

        private void OnNextPerformed(InputAction.CallbackContext context) {
            if (!context.performed || playerDropdownOpen) {
                return;
            }

            SelectNextPanel();
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            if (fadeMusicCoroutine != null) {
                StopCoroutine(fadeMusicCoroutine);
                fadeMusicCoroutine = null;
            }
            musicSource.volume = 1;
        }

        private void OnLocalPlayerAddConfirmed(CallbackLocalPlayerAddConfirmed e) {
            UpdateStartButton(e.Game, e.Game.Frames.Predicted);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            UpdateStartButton(e.Game, e.Frame);

            if (fadeMusicCoroutine != null) {
                StopCoroutine(fadeMusicCoroutine);
                fadeMusicCoroutine = null;
            }
            musicSource.volume = 1;
        }

        private void OnHostChanged(EventHostChanged e) {
            Canvas.UpdateHeader();
            UpdateStartButton(e.Game, e.Frame);
        }

        private unsafe void OnStartingCountdownChanged(EventStartingCountdownChanged e) {
            Frame f = e.Frame;
            UpdateStartButton(e.Game, f, e.IsGameStarting ? 3 : -1);

            bool isHost = e.Game.PlayerIsLocal(QuantumUtils.GetHostPlayer(f, out _));
            
            if (isHost
                || (f.Number - lastCountdownStartFrame) > (f.UpdateRate * 3)
                || !invalidStart) {

                Canvas.PlaySound(e.IsGameStarting ? SoundEffect.UI_FileSelect : SoundEffect.UI_Back);
                if (e.IsGameStarting) {
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.starting", ChatManager.Red, "countdown", "3");
                    invalidStart = false;
                } else {
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.startcancelled", ChatManager.Red);
                    invalidStart = true;
                }
                lastCountdownStartFrame = f.Number;
            } else if (e.IsGameStarting) {
                invalidStart = true;
            }
        }

        private unsafe void OnPlayerDataChanged(EventPlayerDataChanged e) {
            bool isHost = e.Game.PlayerIsLocal(QuantumUtils.GetHostPlayer(e.Frame, out _));

            if (isHost) {
                startGameButton.Interactable = QuantumUtils.IsGameStartable(e.Frame);
            } else {
                startGameButton.Interactable = e.Frame.Global->GameStartFrames == 0;
            }
        }

        private void OnCountdownTick(EventCountdownTick e) {
            UpdateStartButton(e.Game, e.Frame);
        }

        private void OnPlayerEntryDropdownChanged(PlayerListEntry entry, bool state) {
            playerDropdownOpen = state ? entry : null;
        }
    }
}
