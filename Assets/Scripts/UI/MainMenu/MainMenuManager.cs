using NSMB.Extensions;
using NSMB.Translation;
using NSMB.UI.Prompts;
using NSMB.Utils;
using Photon.Realtime;
using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace NSMB.UI.MainMenu {
    public class MainMenuManager : Singleton<MainMenuManager> {

        //---Static Variables
        public const int NicknameMin = 2, NicknameMax = 20;

        //---Public Variables
        public AudioSource sfx, music;
        public Toggle spectateToggle;
        public GameObject playersContent, playersPrefab, chatContent, chatPrefab;
        public GameObject titleSelected, mainMenuSelected, lobbySelected, currentLobbySelected, replaysSelected, updateBoxSelected, ColorName;
        public int currentSkin;

        //---Serialized Fields
        [Header("Managers")]
        [SerializeField] public PlayerListHandler playerList;
        [SerializeField] public RoomListManager roomManager;
        [SerializeField] private PaletteChooser colorManager;
        [SerializeField] public MainMenuChat chat;
        [SerializeField] public RoomSettingsCallbacks roomSettingsCallbacks;
        [SerializeField] private LoopingMusicPlayer musicPlayer;

        [Header("UI Elements")]
        [SerializeField] private GameObject title;
        [SerializeField] private GameObject bg, mainMenu, lobbyMenu, replayMenu, createLobbyPrompt, privateRoomIdPrompt, inLobbyMenu, creditsMenu, updateBox;
        [SerializeField] private GameObject sliderText, currentMaxPlayers, settingsPanel;
        [SerializeField] private TMP_Dropdown levelDropdown, characterDropdown, regionDropdown;
        [SerializeField] private Button createRoomBtn, joinRoomBtn, joinPrivateRoomBtn, reconnectBtn, startGameBtn;
        [SerializeField] private TMP_InputField nicknameField, chatTextField;
        [SerializeField] private TMP_Text lobbyHeaderText, updateText, startGameButtonText;
        [SerializeField] private ScrollRect settingsScroll;
        [SerializeField] private Slider lobbyPlayersSlider;
        [SerializeField] private CanvasGroup hostControlsGroup, copyRoomIdCanvasGroup, roomListCanvasGroup, joinStartButtonCanvasGroup;
        [SerializeField] private ErrorPrompt errorPrompt, networkErrorPrompt;

        [SerializeField, FormerlySerializedAs("ColorBar")] private Image colorBar;
        [SerializeField] private Image overallsColorImage, shirtColorImage;
        [SerializeField] private GameObject playerColorPaletteIcon, playerColorDisabledIcon;

        [Header("Misc")]
        [SerializeField] public List<MapData> maps;

        //---Private Variables
        private CharacterAsset currentCharacter;
        private Coroutine quitCoroutine, fadeMusicCoroutine;
        private int lastCountdownStartFrame;
        private bool wasSettingsOpen, startingGame, alreadyInRoom;

        public void Awake() {
            Set(this, false);
        }

        public void OnEnable() {
            ControlSystem.controls.UI.Pause.performed += OnPause;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);

            musicPlayer.Restart();
        }

        public void OnDisable() {
            ControlSystem.controls.UI.Pause.performed -= OnPause;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Start() {
            // Clear game-specific settings so they don't carry over
            /* TODO
            HorizontalCamera.SizeIncreaseTarget = 0;
            HorizontalCamera.SizeIncreaseCurrent = 0;
            */

            PreviewLevel(UnityEngine.Random.Range(0, maps.Count));
            //UpdateRegionDropdown();
            //StartCoroutine(NetworkHandler.PingRegions());

            // Multiplayer stuff
            if (GlobalController.Instance.firstConnection) {
                OpenTitleScreen();
            }

            // Controls & Settings
            nicknameField.text = Settings.Instance.generalNickname;
            nicknameField.characterLimit = NicknameMax;
            UpdateNickname();

            // Discord RPC
            GlobalController.Instance.discordController.UpdateActivity();

            // Set up room list
            //roomManager.Initialize();

#if PLATFORM_WEBGL
            copyRoomIdCanvasGroup.interactable = false;
#else
            // Version Checking
            if (!GlobalController.Instance.checkedForVersion) {
                UpdateChecker.IsUpToDate((upToDate, latestVersion) => {
                    if (upToDate) {
                        return;
                    }

                    updateText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.update.prompt", "newversion", latestVersion, "currentversion", Application.version);
                    updateBox.SetActive(true);
                    EventSystem.current.SetSelectedGameObject(updateBoxSelected);
                });
                GlobalController.Instance.checkedForVersion = true;
            }
#endif

            GlobalController.Instance.firstConnection = false;

            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded, onlyIfActiveAndEnabled: true);
            QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved, onlyIfActiveAndEnabled: true);
            QuantumEvent.Subscribe<EventStartingCountdownChanged>(this, OnCountdownChanged);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
            QuantumEvent.Subscribe<EventHostChanged>(this, OnHostChanged);
            QuantumEvent.Subscribe<EventCountdownTick>(this, OnCountdownTick);
            QuantumEvent.Subscribe<EventPlayerDataChanged>(this, OnPlayerDataChanged);
            QuantumEvent.Subscribe<EventPlayerKickedFromRoom>(this, OnPlayerKickedFromRoom);
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
            QuantumCallback.Subscribe<CallbackLocalPlayerAddConfirmed>(this, OnLocalPlayerConfirmed);
        }

        public void Update() {
            wasSettingsOpen = GlobalController.Instance.optionsManager.gameObject.activeSelf;
        }

        public unsafe void InitializeRoom(Frame f) {
            // Chat
            chatTextField.SetTextWithoutNotify("");

            // Host reminder
            SendHostReminder();

            // Set the player settings
            SwapCharacter(Settings.Instance.generalCharacter, false);
            SwapPlayerSkin(Settings.Instance.generalPalette, false);
            spectateToggle.isOn = false;

            // Create player icons
            playerList.PopulatePlayerEntries(f);
        }

        public unsafe void EnterRoom() {
            // Open the in-room menu
            OpenInRoomMenu();

            // Fix the damned setting scroll menu
            StartCoroutine(SetVerticalNormalizedPositionFix(settingsScroll, 1));

            // Set the room settings
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            PlayerRef hostPlayer = QuantumUtils.GetHostPlayer(f, out _);
            bool isHost = game.PlayerIsLocal(hostPlayer);
            hostControlsGroup.interactable = isHost;
            roomSettingsCallbacks.RefreshSettingsUI(game, f, false);

            // Reset the "Game start" button counting down
            OnCountdownTick(game, -1);

            // Update the room header text + color
            UpdateRoomHeader(f, hostPlayer);

            // Discord RPC
            GlobalController.Instance.discordController.UpdateActivity();
        }

        public unsafe void UpdateRoomHeader(Frame f, PlayerRef host) {
            const int rngSeed = 2035767;
            var playerData = f.GetPlayerData(host);
            string hostname;

            if (playerData == null) {
                hostname = Settings.Instance.generalNickname.ToValidUsername(f, host);
            } else {
                hostname = playerData.PlayerNickname.ToValidUsername(f, host);
            }

            lobbyHeaderText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.rooms.listing.name", "playername", hostname);
            UnityEngine.Random.InitState(hostname.GetHashCode() + rngSeed);
            colorBar.color = UnityEngine.Random.ColorHSV(0f, 1f, 0.5f, 1f, 0f, 1f);
        }

        private static IEnumerator SetVerticalNormalizedPositionFix(ScrollRect scroll, float value) {
            for (int i = 0; i < 3; i++) {
                scroll.verticalNormalizedPosition = value;
                Canvas.ForceUpdateCanvases();
                yield return null;
            }
        }

        public void PreviewLevel(int levelIndex) {
            if (levelIndex < 0 || levelIndex >= maps.Count) {
                levelIndex = 0;
            }

            Camera.main.transform.position = maps[levelIndex].levelPreviewPosition.transform.position;
        }

        private void DisableAllMenus() {
            title.SetActive(false);
            bg.SetActive(false);
            mainMenu.SetActive(false);
            lobbyMenu.SetActive(false);
            createLobbyPrompt.SetActive(false);
            inLobbyMenu.SetActive(false);
            creditsMenu.SetActive(false);
            replayMenu.SetActive(false);
            privateRoomIdPrompt.SetActive(false);
            updateBox.SetActive(false);
        }

        public void OpenTitleScreen() {
            DisableAllMenus();
            title.SetActive(true);

            EventSystem.current.SetSelectedGameObject(titleSelected);
        }

        public void OpenMainMenu() {
            DisableAllMenus();
            bg.SetActive(true);
            mainMenu.SetActive(true);

            EventSystem.current.SetSelectedGameObject(mainMenuSelected);
        }

        public void OpenRoomListMenu() {
            DisableAllMenus();
            bg.SetActive(true);
            lobbyMenu.SetActive(true);
            chat.ClearChat();

            // First connection on play game button.
            if (NetworkHandler.Client.State == ClientState.PeerCreated) {
                Reconnect();
            }

            roomManager.RefreshRooms();
            EventSystem.current.SetSelectedGameObject(lobbySelected);
        }

        public void TryOpenCreateRoomPrompt() {
#if PLATFORM_WEBGL
            DisableAllMenus();
            bg.SetActive(true);
            lobbyMenu.SetActive(true);
            webglCreateLobbyPrompt.SetActive(true);
#else
            OpenCreateRoomPrompt();
#endif
        }

        public void OpenCreateRoomPrompt() {
            DisableAllMenus();
            bg.SetActive(true);
            lobbyMenu.SetActive(true);
            createLobbyPrompt.SetActive(true);
        }

        public void OpenOptions() {
            if (wasSettingsOpen) {
                return;
            }

            GlobalController.Instance.optionsManager.OpenMenu();
        }

        public void OpenReplays() {
            DisableAllMenus();
            bg.SetActive(true);
            replayMenu.SetActive(true);

            EventSystem.current.SetSelectedGameObject(replaysSelected);
        }

        public void OpenInRoomMenu() {
            DisableAllMenus();
            bg.SetActive(true);
            inLobbyMenu.SetActive(true);

            EventSystem.current.SetSelectedGameObject(currentLobbySelected);
        }

        public void OpenErrorBox(short cause) {
            OpenErrorBox(NetworkUtils.RealtimeErrorCodes.GetValueOrDefault(cause, $"Unknown error (Code: {cause})"));
        }

        public void OpenErrorBox(string key, params string[] replacements) {
            errorPrompt.OpenWithText(key, replacements);
            GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        }

        public void OpenNetworkErrorBox(DisconnectCause cause, params string[] replacements) {
            if (replacements.Length == 0) {
                replacements = new[] { "reason", GlobalController.Instance.translationManager.GetTranslation("ui.error.noreason") };
            }
            OpenNetworkErrorBox(NetworkUtils.RealtimeDisconnectCauses.GetValueOrDefault(cause, $"Unknown error (Code: {cause})"), replacements);
        }

        public void OpenNetworkErrorBox(string key, params string[] replacements) {
            networkErrorPrompt.OpenWithText(key, replacements);
            GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        }

        public void BackSound() {
            sfx.PlayOneShot(SoundEffect.UI_Back);
        }

        public void ConfirmSound() {
            sfx.PlayOneShot(SoundEffect.UI_Decide);
        }

        public void CursorSound() {
            sfx.PlayOneShot(SoundEffect.UI_Cursor);
        }

        public void StartSound() {
            sfx.PlayOneShot(SoundEffect.UI_StartGame);
        }

        public async void Reconnect() {
            GlobalController.Instance.connecting.SetActive(true);
            Debug.Log(GlobalController.Instance.connecting.activeSelf);
            roomListCanvasGroup.interactable = false;
            _ = await NetworkHandler.ConnectToRegion(null);
        }

        public void QuitRoom() {
            QuantumRunner.Default.Shutdown();
        }

        public unsafe void StartCountdown() {
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            PlayerRef host = QuantumUtils.GetHostPlayer(f, out _);

            if (game.PlayerIsLocal(host)) {
                int hostSlot = game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)];
                game.SendCommand(hostSlot, new CommandToggleCountdown());
            } else {
                foreach (int slot in game.GetLocalPlayerSlots()) {
                    // All players are ready at the same time
                    game.SendCommand(slot, new CommandToggleReady());
                }

                List<PlayerRef> localPlayers = game.GetLocalPlayers();
                bool isReady = false;
                if (localPlayers.Count > 0) {
                    var playerData = QuantumUtils.GetPlayerData(f, localPlayers[0]);
                    isReady = playerData->IsReady;
                }

                sfx.PlayOneShot(isReady ? SoundEffect.UI_Back : SoundEffect.UI_Decide);
                UpdateReadyButton(!isReady);
            }
        }

        private IEnumerator FadeMusic() {
            while (music.volume > 0) {
                music.volume -= Time.deltaTime;
                yield return null;
            }
        }

        public void UpdateReadyButton(bool ready) {
            TranslationManager tm = GlobalController.Instance.translationManager;
            startGameButtonText.text = tm.GetTranslation(ready ? "ui.inroom.buttons.unready" : "ui.inroom.buttons.readyup");
        }

        public unsafe void UpdateStartGameButton(QuantumGame game) {

            Frame f = game.Frames.Predicted;
            PlayerRef host = QuantumUtils.GetHostPlayer(f, out _);
            bool weAreHost = game.PlayerIsLocal(host);

            TranslationManager tm = GlobalController.Instance.translationManager;
            
            if (f.Global->GameStartFrames > 0) {
                return;
            }

            if (weAreHost) {
                startGameButtonText.text = tm.GetTranslation("ui.inroom.buttons.start");
                startGameBtn.interactable = QuantumUtils.IsGameStartable(f);
            } else {
                List<PlayerRef> localPlayers = game.GetLocalPlayers();
                if (localPlayers.Count > 0) {
                    var playerData = QuantumUtils.GetPlayerData(f, localPlayers[0]);
                    if (playerData != null) {
                        UpdateReadyButton(playerData->IsReady);
                    }
                    startGameBtn.interactable = true;
                }
            }
        }

        /* TODO
        public void Kick(PlayerData target) {
            if (target.Owner == Runner.LocalPlayer) {
                return;
            }

            SessionData.Instance.Disconnect(target.Owner);
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.kicked", ChatManager.Blue, "playername", target.GetNickname());
        }
        */

        /* TODO
        public void Promote(PlayerData target) {
            if (target.Owner == Runner.LocalPlayer) {
                return;
            }

            if (Runner.Topology == Topologies.ClientServer) {
                ChatManager.Instance.AddSystemMessage("Cannot promote yet!", ChatManager.Red);
            } else {
                Runner.SetMasterClient(target.Owner);
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.promoted", ChatManager.Blue, "playername", target.GetNickname());
            }
        }
        */

        /* TODO
        public void Mute(PlayerData target) {
            if (target.Owner == Runner.LocalPlayer) {
                return;
            }

            bool newMuteState = !target.IsMuted;
            target.IsMuted = newMuteState;
            ChatManager.Instance.AddSystemMessage(newMuteState ? "ui.inroom.chat.player.muted" : "ui.inroom.chat.player.unmuted", ChatManager.Blue, "playername", target.GetNickname());
        }
        */

        /* TODO
        public void Ban(PlayerData target) {
            if (target.Owner == Runner.LocalPlayer) {
                return;
            }

            SessionData.Instance.Disconnect(target.Owner);
            SessionData.Instance.AddBan(target);
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.banned", ChatManager.Blue, "playername", target.GetNickname());
        }
        */

        public void UI_CharacterDropdownChanged() {
            int value = characterDropdown.value;
            SwapCharacter(value, true);

            CharacterAsset data = GlobalController.Instance.config.CharacterDatas[value];
            sfx.PlayOneShot(SoundEffect.Player_Voice_Selected, data);
        }

        public void SwapCharacter(int character, bool broadcast) {
            if (broadcast) {
                foreach (int slot in QuantumRunner.DefaultGame.GetLocalPlayerSlots()) {
                    QuantumRunner.DefaultGame.SendCommand(slot, new CommandChangePlayerData {
                        EnabledChanges = CommandChangePlayerData.Changes.Character,
                        Character = (byte) character
                    });
                }
            } else {
                characterDropdown.SetValueWithoutNotify(character);
            }

            Settings.Instance.generalCharacter = character;
            Settings.Instance.SaveSettings();

            var characters = GlobalController.Instance.config.CharacterDatas;
            currentCharacter = characters[character % characters.Length];
            colorManager.ChangeCharacter(currentCharacter);
            SwapPlayerSkin(currentSkin, false);
        }

        public void SwapPlayerSkin(int index, bool save) {
            bool disabled = index == 0;

            if (!disabled) {
                playerColorDisabledIcon.SetActive(false);
                playerColorPaletteIcon.SetActive(true);
                PaletteSet set = ScriptableManager.Instance.skins[index];
                CharacterSpecificPalette colors = set.GetPaletteForCharacter(currentCharacter);
                overallsColorImage.color = colors.overallsColor;
                shirtColorImage.color = colors.shirtColor;
                ColorName.GetComponent<TMP_Text>().text = set.Name;
            }

            playerColorDisabledIcon.SetActive(disabled);
            playerColorPaletteIcon.SetActive(!disabled);

            if (save) {
                Settings.Instance.generalPalette = index;
                Settings.Instance.SaveSettings();
            }

            foreach (int slot in QuantumRunner.DefaultGame.GetLocalPlayerSlots()) {
                QuantumRunner.DefaultGame.SendCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.Palette,
                    Palette = (byte) index
                });
            }

            currentSkin = index;
        }

        public void EnableSpectator(Toggle toggle) {
            foreach (int slot in QuantumRunner.DefaultGame.GetLocalPlayerSlots()) {
                QuantumRunner.DefaultGame.SendCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.Spectating,
                    Spectating = toggle.isOn,
                });
            }
        }


        private void UpdateNickname() {
            bool validUsername = Settings.Instance.generalNickname.IsValidUsername();
            ColorBlock colors = nicknameField.colors;
            if (validUsername) {
                colors.normalColor = Color.white;
                colors.highlightedColor = new(0.7f, 0.7f, 0.7f, 1);
            } else {
                colors.normalColor = new(1, 0.7f, 0.7f, 1);
                colors.highlightedColor = new(1, 0.55f, 0.55f, 1);
            }

            nicknameField.colors = colors;
            joinStartButtonCanvasGroup.interactable = validUsername && NetworkHandler.Client.IsConnectedAndReady;
            NetworkHandler.Client.NickName = Settings.Instance.generalNickname;
        }

        public void SetUsername(TMP_InputField field) {
            Settings.Instance.generalNickname = field.text;
            UpdateNickname();
            Settings.Instance.SaveSettings();
        }

        public void OpenLinks() {
            Application.OpenURL("https://github.com/ipodtouch0218/NSMB-MarioVsLuigi/blob/master/LINKS.md");
        }

        public void Quit() {
            if (quitCoroutine == null) {
                quitCoroutine = StartCoroutine(FinishQuitting());
            }
        }

        private IEnumerator FinishQuitting() {
            AudioClip clip = SoundEffect.UI_Quit.GetClip();
            sfx.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OpenDownloadsPage() {
            Application.OpenURL("https://github.com/ipodtouch0218/NSMB-MarioVsLuigi/releases/latest");
            OpenMainMenu();
        }

        private unsafe void SendHostReminder() {
            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef hostPlayer = QuantumUtils.GetHostPlayer(game.Frames.Predicted, out _);

            if (game.PlayerIsLocal(hostPlayer)) {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.hostreminder", ChatManager.Red);
            }
        }

        public unsafe void OnCountdownTick(QuantumGame game, int time) {
            Frame f = game.Frames.Predicted;
            PlayerRef hostPlayer = QuantumUtils.GetHostPlayer(f, out _);
            bool weAreHost = game.PlayerIsLocal(hostPlayer);

            TranslationManager tm = GlobalController.Instance.translationManager;
            if (time > 0) {
                startGameBtn.interactable = weAreHost;
                startGameButtonText.text = tm.GetTranslationWithReplacements("ui.inroom.buttons.starting", "countdown", time.ToString());
                hostControlsGroup.interactable = false;
                if (time == 1 && fadeMusicCoroutine == null) {
                    fadeMusicCoroutine = StartCoroutine(FadeMusic());
                }
            } else {
                UpdateStartGameButton(game);
                hostControlsGroup.interactable = weAreHost;
                if (fadeMusicCoroutine != null) {
                    StopCoroutine(fadeMusicCoroutine);
                    fadeMusicCoroutine = null;
                }
                music.volume = 1;
            }

            startGameButtonText.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
        }

        private void GameStart(QuantumGame game) {
            GlobalController.Instance.loadingCanvas.Initialize(game);
            transform.parent.gameObject.SetActive(false);
        }

        //---Callbacks
        /* TODO
        public void OnLobbyConnect(NetworkRunner runner, LobbyInfo info) {
            for (int i = 0; i < regionDropdown.options.Count; i++) {
                RegionOption option = (RegionOption) regionDropdown.options[i];
                if (option.Region != info.Region) {
                    continue;
                }

                regionDropdown.SetValueWithoutNotify(i);
                return;
            }
        }

        private void OnShutdown(NetworkRunner runner, ShutdownReason cause) {
            if (cause != ShutdownReason.Ok) {
                OpenNetworkErrorBox(cause);
            }

            if (inLobbyMenu.activeSelf) {
                OpenRoomListMenu();
            }

            music.volume = 1;
            GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        }

        private void OnDisconnect(NetworkRunner runner, NetDisconnectReason disconnectReason) {
            OpenNetworkErrorBox(disconnectReason);
            OpenRoomListMenu();
            GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        }

        private void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason cause) {
            OpenErrorBox(cause);

            if (!runner.IsCloudReady) {
                roomManager.ClearRooms();
            }
        }
        */

        private unsafe void OnLocalPlayerConfirmed(CallbackLocalPlayerAddConfirmed e) {
            QuantumGame game = e.Game;
            int slot = game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(e.Player)];
            Frame f = game.Frames.Predicted;

            QuantumRunner.DefaultGame.SendCommand(slot, new CommandChangePlayerData {
                EnabledChanges = CommandChangePlayerData.Changes.All,
                Character = (byte) Settings.Instance.generalCharacter,
                Palette = (byte) Settings.Instance.generalPalette,
                Spectating = false,
                Team = (byte) (e.Player % f.SimulationConfig.Teams.Length),
            });

            if (!alreadyInRoom && e.Game.PlayerIsLocal(e.Player)) {
                InitializeRoom(f);
                if (f.Global->GameState == GameState.PreGameRoom) {
                    EnterRoom();
                } else {
                    GameStart(game);
                }
                alreadyInRoom = true;
            }
        }

        private unsafe void OnLanguageChanged(TranslationManager tm) {
            int selectedLevel = levelDropdown.value;
            levelDropdown.ClearOptions();
            levelDropdown.AddOptions(maps.Select(map => (map.isCustom ? "<sprite name=room_customlevel> " : "") + tm.GetTranslation(map.translationKey)).ToList());
            levelDropdown.SetValueWithoutNotify(selectedLevel);

            int selectedCharacter = characterDropdown.value;
            characterDropdown.ClearOptions();
            foreach (CharacterAsset character in GlobalController.Instance.config.CharacterDatas) {
                string characterName = tm.GetTranslation(character.TranslationString);
                characterDropdown.options.Add(new TMP_Dropdown.OptionData(characterName, character.ReadySprite));
            }
            characterDropdown.SetValueWithoutNotify(selectedCharacter);
            characterDropdown.RefreshShownValue();

            QuantumGame game = QuantumRunner.DefaultGame;
            if (game != null) {
                Frame f = game.Frames.Predicted;
                UpdateRoomHeader(f, QuantumUtils.GetHostPlayer(f, out _));
                OnCountdownTick(game, f.Global->GameStartFrames == 0 ? -1 : f.Global->GameStartFrames / 60);
            }
        }

        private void OnPause(InputAction.CallbackContext context) {
            if (isActiveAndEnabled && (NetworkHandler.Client?.InRoom ?? false) && !wasSettingsOpen) {
                // Open the settings menu if we're inside a room (so we dont have to leave)
                ConfirmSound();
                OpenOptions();
            }
        }

        public void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.WaitingForPlayers) {
                GameStart(e.Game);
            } else if (e.NewState == GameState.PreGameRoom) {
                transform.parent.gameObject.SetActive(true);
                EnterRoom();
            }
        }

        private unsafe void OnGameStarted(CallbackGameStarted e) {
            alreadyInRoom = false;

            if (NetworkHandler.IsReplay) {
                GameStart(e.Game);
            }
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            transform.parent.gameObject.SetActive(true);
            OpenRoomListMenu();
            GlobalController.Instance.discordController.UpdateActivity();
            alreadyInRoom = false;

            if (!NetworkHandler.WasDisconnectedViaError) {
                Reconnect();
            }
            NetworkHandler.WasDisconnectedViaError = false;
        }

        private void OnPlayerAdded(EventPlayerAdded e) {
            sfx.PlayOneShot(SoundEffect.UI_PlayerConnect);
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            sfx.PlayOneShot(SoundEffect.UI_PlayerDisconnect);
        }

        private unsafe void OnCountdownChanged(EventStartingCountdownChanged e) {
            Frame f = e.Frame;
            PlayerRef host = QuantumUtils.GetHostPlayer(f, out _);
            OnCountdownTick(e.Game, e.IsGameStarting ? 3 : -1);

            if (e.Game.PlayerIsLocal(host)
                || (startingGame && !e.IsGameStarting)
                || f.Number - lastCountdownStartFrame > 60 * 3) {
                // Play the sound (and send the chat message)

                sfx.PlayOneShot(e.IsGameStarting ? SoundEffect.UI_FileSelect : SoundEffect.UI_Back);

                if (e.IsGameStarting) {
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.starting", ChatManager.Red, "countdown", "3");
                } else {
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.startcancelled", ChatManager.Red);
                }
                startingGame = e.IsGameStarting;
            }
            lastCountdownStartFrame = f.Number;
        }

        private void OnPlayerKickedFromRoom(EventPlayerKickedFromRoom e) {
            UpdateStartGameButton(e.Game);

            if (e.Game.PlayerIsLocal(e.Player)) {
                QuantumRunner.Default.Shutdown(ShutdownCause.SessionError);
            }
        }

        private void OnPlayerDataChanged(EventPlayerDataChanged e) {
            UpdateStartGameButton(e.Game);
        }

        private void OnCountdownTick(EventCountdownTick e) {
            OnCountdownTick(e.Game, e.SecondsRemaining);
        }

        private unsafe void OnHostChanged(EventHostChanged e) {
            SendHostReminder();
            hostControlsGroup.interactable = e.Game.PlayerIsLocal(e.NewHost);
            UpdateRoomHeader(e.Frame, e.NewHost);

            if (e.Game.PlayerIsLocal(e.NewHost) && NetworkHandler.Client != null && NetworkHandler.Client.InRoom) {
                RuntimePlayer runtimePlayer = e.Frame.GetPlayerData(e.NewHost);
                if (runtimePlayer != null) {
                    NetworkHandler.Client.CurrentRoom.SetCustomProperties(new Photon.Client.PhotonHashtable {
                        [Enums.NetRoomProperties.HostName] = runtimePlayer.PlayerNickname
                    });
                }
            }
        }

        //---Debug
#if UNITY_EDITOR
        private static readonly Vector3 MaxCameraSize = new(16f/9f * 7f, 7f);

        public void OnDrawGizmos() {
            Gizmos.color = Color.red;
            foreach (MapData map in maps) {
                if (map.levelPreviewPosition) {
                    Gizmos.DrawWireCube(map.levelPreviewPosition.transform.position, MaxCameraSize);
                }
            }
        }
#endif

        //---Helpers
        [Serializable]
        public class MapData {
            public string translationKey;
            public GameObject levelPreviewPosition;
            public AssetRef<Map> mapAsset;
            public bool isCustom;
        }
    }
}
