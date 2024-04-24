using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

using Fusion;
using Fusion.Sockets;
using NSMB.Extensions;
using NSMB.Translation;
using NSMB.UI.Prompts;
using NSMB.Utils;

namespace NSMB.UI.MainMenu {
    public class MainMenuManager : Singleton<MainMenuManager> {

        //---Static Variables
        public static readonly int NicknameMin = 2, NicknameMax = 20;
        public static bool WasHostMigration;

        //---Properties
        private NetworkRunner Runner => NetworkHandler.Runner;
        private PlayerData LocalData => Runner.GetLocalPlayerData();

        //---Public Variables
        public bool nonNetworkShutdown;
        public AudioSource sfx, music;
        public Toggle spectateToggle;
        public GameObject playersContent, playersPrefab, chatContent, chatPrefab;
        public GameObject titleSelected, mainMenuSelected, lobbySelected, currentLobbySelected, creditsSelected, updateBoxSelected, ColorName;
        public byte currentSkin;

        //---Serialized Fields
        [Header("Managers")]
        [SerializeField] public PlayerListHandler playerList;
        [SerializeField] public RoomListManager roomManager;
        [SerializeField] private ColorChooser colorManager;
        [SerializeField] public MainMenuChat chat;
        [SerializeField] public RoomSettingsCallbacks roomSettingsCallbacks;

        [Header("UI Elements")]
        [SerializeField] private GameObject title;
        [SerializeField] private GameObject bg, mainMenu, lobbyMenu, createLobbyPrompt, webglCreateLobbyPrompt, privateRoomIdPrompt, inLobbyMenu, creditsMenu, updateBox;
        [SerializeField] private GameObject sliderText, currentMaxPlayers, settingsPanel;
        [SerializeField] private TMP_Dropdown levelDropdown, characterDropdown, regionDropdown;
        [SerializeField] private Button createRoomBtn, joinRoomBtn, joinPrivateRoomBtn, reconnectBtn, startGameBtn;
        [SerializeField] private TMP_InputField nicknameField, chatTextField;
        [SerializeField] private TMP_Text lobbyHeaderText, updateText, startGameButtonText;
        [SerializeField] private ScrollRect settingsScroll;
        [SerializeField] private Slider lobbyPlayersSlider;
        [SerializeField] private CanvasGroup hostControlsGroup, copyRoomIdCanvasGroup;
        [SerializeField] private ErrorPrompt errorPrompt, networkErrorPrompt;

        [SerializeField, FormerlySerializedAs("ColorBar")] private Image colorBar;
        [SerializeField] private Image overallsColorImage, shirtColorImage;
        [SerializeField] private GameObject playerColorPaletteIcon, playerColorDisabledIcon;

        [Header("Misc")]
        [SerializeField] public List<MapData> maps;

        //---Private Variables
        private Coroutine playerPingUpdateCoroutine, quitCoroutine, fadeMusicCoroutine;
        private bool validName;
        private bool wasSettingsOpen;

        public void Awake() {
            Set(this, false);
        }

        public void OnEnable() {
            // Register callbacks
            PlayerData.OnPlayerDataReady += OnPlayerDataReady;
            PlayerData.OnPlayerDataDespawned += OnPlayerDataDespawned;
            NetworkHandler.OnLobbyConnect += OnLobbyConnect;
            NetworkHandler.OnShutdown += OnShutdown;
            NetworkHandler.OnDisconnectedFromServer += OnDisconnect;
            NetworkHandler.OnConnectFailed += OnConnectFailed;
            NetworkHandler.OnRegionPingsUpdated += OnRegionPingsUpdated;
            NetworkHandler.OnHostMigration += OnHostMigration;
            MvLSceneManager.OnSceneLoadStart += OnSceneLoadStart;

            ControlSystem.controls.UI.Pause.performed += OnPause;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void OnDisable() {
            // Unregister callbacks
            PlayerData.OnPlayerDataReady -= OnPlayerDataReady;
            PlayerData.OnPlayerDataDespawned -= OnPlayerDataDespawned;
            NetworkHandler.OnLobbyConnect -= OnLobbyConnect;
            NetworkHandler.OnShutdown -= OnShutdown;
            NetworkHandler.OnDisconnectedFromServer -= OnDisconnect;
            NetworkHandler.OnConnectFailed -= OnConnectFailed;
            NetworkHandler.OnRegionPingsUpdated -= OnRegionPingsUpdated;
            NetworkHandler.OnHostMigration -= OnHostMigration;
            MvLSceneManager.OnSceneLoadStart -= OnSceneLoadStart;

            ControlSystem.controls.UI.Pause.performed -= OnPause;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Start() {
            // Clear game-specific settings so they don't carry over
            WasHostMigration = false;
            HorizontalCamera.SizeIncreaseTarget = 0;
            HorizontalCamera.SizeIncreaseCurrent = 0;

            PreviewLevel(UnityEngine.Random.Range(0, maps.Count));
            UpdateRegionDropdown();
            //StartCoroutine(NetworkHandler.PingRegions());

            // Photon stuff.
            if (GlobalController.Instance.firstConnection) {
                // Initial connection to the game
                OpenTitleScreen();

            } else if ((Runner.IsServer || Runner.IsConnectedToServer) && SessionData.Instance && SessionData.Instance.Object) {
                // Call enterroom callback
                EnterRoom(true);

            } else {
                // Quit out of a room unexpectedly
                OpenRoomListMenu();
            }

            // Controls & Settings
            nicknameField.text = Settings.Instance.generalNickname;
            nicknameField.characterLimit = NicknameMax;
            UpdateNickname();

            // Discord RPC
            GlobalController.Instance.discordController.UpdateActivity();

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
        }

        public void Update() {

            bool connectedToNetwork = NetworkHandler.Connected && !Runner.SessionInfo;
            bool connectingToNetwork = NetworkHandler.Connecting || Runner.SessionInfo;

            GlobalController.Instance.connecting.SetActive((connectingToNetwork && lobbyMenu.activeInHierarchy) || WasHostMigration);

            joinRoomBtn.interactable = connectedToNetwork && roomManager.SelectedRoom != null && validName;
            createRoomBtn.interactable = connectedToNetwork && validName;
            //regionDropdown.interactable = connectedToNetwork;

            reconnectBtn.gameObject.SetActive(NetworkHandler.Disconnected);
            joinPrivateRoomBtn.gameObject.SetActive(connectedToNetwork);

            wasSettingsOpen = GlobalController.Instance.optionsManager.gameObject.activeSelf;
        }

        public void OnDestroy() {
            if (GlobalController.Instance) {
                GlobalController.Instance.connecting.SetActive(false);
            }
        }

        public void UpdateRegionDropdown() {

            if (regionDropdown.options.Count == 0) {
                // Create brand-new options
                foreach (string region in NetworkHandler.Regions) {
                    NetworkHandler.RegionPings.TryGetValue(region, out int ping);
                    regionDropdown.options.Add(new RegionOption(region, ping));
                }
                regionDropdown.options.Sort();
            } else {
                // Update existing options
                RegionOption selected = (RegionOption) regionDropdown.options[regionDropdown.value];

                if (NetworkHandler.RegionPings != null) {
                    foreach (var option in regionDropdown.options) {
                        if (option is RegionOption ro) {
                            ro.Ping = NetworkHandler.RegionPings[ro.Region];
                        }
                    }
                }

                regionDropdown.options.Sort();
                regionDropdown.SetValueWithoutNotify(regionDropdown.options.IndexOf(selected));
            }
        }

        public void EnterRoom(bool inSameRoom) {

            // Chat
            if (WasHostMigration) {
                // Host chat notification
                if (Runner.IsServer) {
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.hostreminder", ChatManager.Red);
                }
            } else if (inSameRoom) {
                chat.ReplayChatMessages();
            } else {
                chatTextField.SetTextWithoutNotify("");
            }

            // Open the in-room menu
            OpenInRoomMenu();

            if (!WasHostMigration) {
                // Fix the damned setting scroll menu
                StartCoroutine(SetVerticalNormalizedPositionFix(settingsScroll, 1));
            }

            // Set the player settings
            PlayerData data = Runner.GetLocalPlayerData();
            characterDropdown.SetValueWithoutNotify(data.CharacterIndex);
            SwapPlayerSkin(data.SkinIndex, false);
            spectateToggle.isOn = data.IsManualSpectator;

            // Set the room settings
            hostControlsGroup.interactable = data.IsRoomOwner;
            roomSettingsCallbacks.UpdateAllSettings(SessionData.Instance, false);

            // Preview the current level
            PreviewLevel(SessionData.Instance.Level);

            // Reset the "Game start" button counting down
            OnCountdownTick(-1);

            // Update the room header text + color
            UpdateRoomHeader();

            // Discord RPC
            GlobalController.Instance.discordController.UpdateActivity();

            // Create player icons
            playerList.PopulatePlayerEntries();

            WasHostMigration = false;
        }

        public void UpdateRoomHeader() {
            const int rngSeed = 2035767;

            PlayerData host =
                SessionData.Instance.PlayerDatas
                    .Select(kvp => kvp.Value)
                    .Where(pd => pd.IsRoomOwner)
                    .FirstOrDefault();

            string hostname;
            if (host) {
                hostname = host.GetNickname();
            } else {
                // Fallback
                NetworkUtils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.HostName, out hostname);
            }

            lobbyHeaderText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.rooms.listing.name", "playername", hostname.ToValidUsername());
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
            webglCreateLobbyPrompt.SetActive(false);
            inLobbyMenu.SetActive(false);
            creditsMenu.SetActive(false);
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

            if (!errorPrompt.gameObject.activeSelf && !networkErrorPrompt.gameObject.activeSelf && NetworkHandler.Disconnected) {
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

        public void OpenCredits() {
            DisableAllMenus();
            bg.SetActive(true);
            creditsMenu.SetActive(true);

            EventSystem.current.SetSelectedGameObject(creditsSelected);
        }

        public void OpenInRoomMenu() {
            DisableAllMenus();
            bg.SetActive(true);
            inLobbyMenu.SetActive(true);

            EventSystem.current.SetSelectedGameObject(currentLobbySelected);
        }

        public void OpenErrorBox(Enum cause) {
            OpenErrorBox(NetworkUtils.DisconnectMessages.GetValueOrDefault(cause, cause.ToString()));
        }

        public void OpenErrorBox(string key) {
            errorPrompt.OpenWithText(key);
            nonNetworkShutdown = false;
            GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        }

        public void OpenNetworkErrorBox(string key) {
            networkErrorPrompt.OpenWithText(key);
            GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        }

        public void OpenNetworkErrorBox(Enum reason) {
            if (nonNetworkShutdown) {
                OpenErrorBox(reason);
                return;
            }

            OpenNetworkErrorBox(NetworkUtils.DisconnectMessages.GetValueOrDefault(reason, reason.ToString()));
        }

        public void BackSound() {
            sfx.PlayOneShot(Enums.Sounds.UI_Back);
        }

        public void ConfirmSound() {
            sfx.PlayOneShot(Enums.Sounds.UI_Decide);
        }

        public void CursorSound() {
            sfx.PlayOneShot(Enums.Sounds.UI_Cursor);
        }

        public void StartSound() {
            sfx.PlayOneShot(Enums.Sounds.UI_StartGame);
        }

        public void ConnectToDropdownRegion() {
            RegionOption selectedRegion = (RegionOption) regionDropdown.options[regionDropdown.value];
            string targetRegion = selectedRegion.Region;
            if (NetworkHandler.CurrentRegion == targetRegion) {
                return;
            }

            roomManager.ClearRooms();
            NetworkHandler.CurrentRegion = targetRegion;

            _ = NetworkHandler.ConnectToRegion(targetRegion);
        }

        public void Reconnect() {
            Debug.Log("[Network] (Re)connecting to the master server");
            _ = NetworkHandler.ConnectToSameRegion();
        }

        public void QuitRoom() {
            OpenRoomListMenu();

            if (playerPingUpdateCoroutine != null) {
                StopCoroutine(playerPingUpdateCoroutine);
                playerPingUpdateCoroutine = null;
            }

            _ = NetworkHandler.ConnectToRegion();
            GlobalController.Instance.discordController.UpdateActivity();
        }

        public void StartCountdown() {

            PlayerData data = Runner.GetLocalPlayerData();
            if (!data.IsRoomOwner) {
                // Toggle ready up state instead
                sfx.PlayOneShot(data.IsReady ? Enums.Sounds.UI_Back : Enums.Sounds.UI_Decide);
                data.Rpc_SetIsReady(!data.IsReady);
                return;
            }

            if (SessionData.Instance.GameStartTimer.IsRunning) {
                // Cancel early.
                SessionData.Instance.GameStartTimer = TickTimer.None;
                sfx.PlayOneShot(Enums.Sounds.UI_Back);
            } else {
                // Make sure we can actually start the game
                if (!IsRoomConfigurationValid()) {
                    return;
                }

                // Actually start the game.
                SessionData.Instance.GameStartTimer = TickTimer.CreateFromSeconds(Runner, 3f);
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

        public void UpdateStartGameButton() {
            if (!SessionData.Instance || SessionData.Instance.GameStartTimer.IsRunning) {
                return;
            }

            PlayerData data = Runner.GetLocalPlayerData();
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (data && data.IsRoomOwner) {
                startGameButtonText.text = tm.GetTranslation("ui.inroom.buttons.start");
                startGameBtn.interactable = IsRoomConfigurationValid();
            } else {
                UpdateReadyButton(data && data.IsReady);
                startGameBtn.interactable = true;
            }
        }

        public bool IsRoomConfigurationValid() {
            return
                SessionData.Instance.PlayerDatas
                    .Any(kvp => !kvp.Value.IsManualSpectator);
        }

        public void Kick(PlayerData target) {
            if (target.Owner == Runner.LocalPlayer) {
                return;
            }

            SessionData.Instance.Disconnect(target.Owner);
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.kicked", ChatManager.Blue, "playername", target.GetNickname());
        }

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

        public void Mute(PlayerData target) {
            if (target.Owner == Runner.LocalPlayer) {
                return;
            }

            bool newMuteState = !target.IsMuted;
            target.IsMuted = newMuteState;
            ChatManager.Instance.AddSystemMessage(newMuteState ? "ui.inroom.chat.player.muted" : "ui.inroom.chat.player.unmuted", ChatManager.Blue, "playername", target.GetNickname());
        }

        public void Ban(PlayerData target) {
            if (target.Owner == Runner.LocalPlayer) {
                return;
            }

            SessionData.Instance.Disconnect(target.Owner);
            SessionData.Instance.AddBan(target);
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.banned", ChatManager.Blue, "playername", target.GetNickname());
        }

        public void UI_CharacterDropdownChanged() {
            byte value = (byte) characterDropdown.value;
            SwapCharacter(value, true);

            CharacterData data = ScriptableManager.Instance.characters[value];
            sfx.PlayOneShot(Enums.Sounds.Player_Voice_Selected, data);
        }

        public void SwapCharacter(byte character, bool callback) {
            if (callback) {
                LocalData.Rpc_SetCharacterIndex(character);
            } else {
                characterDropdown.SetValueWithoutNotify(character);
            }

            Settings.Instance.generalCharacter = character;
            Settings.Instance.SaveSettings();

            CharacterData data = ScriptableManager.Instance.characters[character];
            colorManager.ChangeCharacter(data);
            SwapPlayerSkin(currentSkin, false, data);
        }

        public void SwapPlayerSkin(byte index, bool callback, CharacterData character = null) {

            if (!character) {
                character = Runner.GetLocalPlayerData().GetCharacterData();
            }

            bool disabled = index == 0;

            if (!disabled) {
                playerColorDisabledIcon.SetActive(false);
                playerColorPaletteIcon.SetActive(true);
                PlayerColorSet set = ScriptableManager.Instance.skins[index];
                PlayerColors colors = set.GetPlayerColors(character);
                overallsColorImage.color = colors.overallsColor;
                shirtColorImage.color = colors.shirtColor;
                ColorName.GetComponent<TMP_Text>().text = set.Name;
            }

            playerColorDisabledIcon.SetActive(disabled);
            playerColorPaletteIcon.SetActive(!disabled);

            if (callback) {
                LocalData.Rpc_SetSkinIndex(index);
                Settings.Instance.generalSkin = index;
                Settings.Instance.SaveSettings();
            }

            currentSkin = index;
        }

        private void UpdateNickname() {
            validName = Settings.Instance.generalNickname.IsValidUsername();
            if (!validName) {
                ColorBlock colors = nicknameField.colors;
                colors.normalColor = new(1, 0.7f, 0.7f, 1);
                colors.highlightedColor = new(1, 0.55f, 0.55f, 1);
                nicknameField.colors = colors;
            } else {
                ColorBlock colors = nicknameField.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new(0.7f, 0.7f, 0.7f, 1);
                nicknameField.colors = colors;
            }
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
            AudioClip clip = Enums.Sounds.UI_Quit.GetClip();
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

        public void EnableSpectator(Toggle toggle) {
            PlayerData data = Runner.GetLocalPlayerData();

            data.Rpc_SetPermanentSpectator(toggle.isOn);
        }

        public SceneRef GetCurrentSceneRef() {
            if (!SessionData.Instance) {
                return SceneRef.None;
            }

            byte index = SessionData.Instance.Level;
            return SceneRef.FromIndex(maps[index].buildIndex);
        }

        public void OnCountdownTick(int time) {
            PlayerData data = Runner.GetLocalPlayerData();
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (time > 0) {
                startGameBtn.interactable = data && data.IsRoomOwner;
                startGameButtonText.text = tm.GetTranslationWithReplacements("ui.inroom.buttons.starting", "countdown", time.ToString());
                hostControlsGroup.interactable = false;
                if (time == 1 && fadeMusicCoroutine == null) {
                    fadeMusicCoroutine = StartCoroutine(FadeMusic());
                }
            } else {
                UpdateStartGameButton();
                hostControlsGroup.interactable = Runner.IsServer || Runner.IsSharedModeMasterClient || (data && data.IsRoomOwner);
                if (fadeMusicCoroutine != null) {
                    StopCoroutine(fadeMusicCoroutine);
                    fadeMusicCoroutine = null;
                }
                music.volume = 1;
            }

            startGameButtonText.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
        }

        //---Callbacks
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

        private void OnLanguageChanged(TranslationManager tm) {
            int selectedLevel = levelDropdown.value;
            levelDropdown.ClearOptions();
            levelDropdown.AddOptions(maps.Select(map => tm.GetTranslation(map.translationKey)).ToList());
            levelDropdown.SetValueWithoutNotify(selectedLevel);

            int selectedCharacter = characterDropdown.value;
            characterDropdown.ClearOptions();
            foreach (CharacterData character in ScriptableManager.Instance.characters) {
                string characterName = tm.GetTranslation(character.translationString);
                characterDropdown.options.Add(new TMP_Dropdown.OptionData(characterName, character.readySprite));
            }
            characterDropdown.SetValueWithoutNotify(selectedCharacter);
            characterDropdown.RefreshShownValue();

            if (SessionData.Instance && SessionData.Instance.Object) {
                UpdateRoomHeader();
                OnCountdownTick((int) (SessionData.Instance.GameStartTimer.RemainingRenderTime(NetworkHandler.Runner) ?? -1));
            }
        }

        private void OnPlayerDataReady(PlayerData data) {
            if (data.Owner == Runner.LocalPlayer) {
                EnterRoom(false);
            }

            sfx.PlayOneShot(Enums.Sounds.UI_PlayerConnect);
            UpdateStartGameButton();
        }

        private void OnPlayerDataDespawned(PlayerData data) {
            if (!Runner.IsShutdown && data.Owner != Runner.LocalPlayer) {
                sfx.PlayOneShot(Enums.Sounds.UI_PlayerDisconnect);
                UpdateStartGameButton();
            }

            GlobalController.Instance.discordController.UpdateActivity();
        }

        private void OnPause(InputAction.CallbackContext context) {
            if (NetworkHandler.Runner.SessionInfo.IsValid && !wasSettingsOpen) {
                // Open the settings menu if we're inside a room (so we dont have to leave)
                ConfirmSound();
                OpenOptions();
            }
        }

        private void OnRegionPingsUpdated() {
            UpdateRegionDropdown();
        }

        private void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {
            playerList.RemoveAllPlayerEntries();
        }

        private void OnSceneLoadStart() {
            if (!Runner.TryGetSceneInfo(out var sceneInfo) || sceneInfo.Scenes[0].AsIndex != 0) {
                GlobalController.Instance.loadingCanvas.Initialize();
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
            public int buildIndex;
        }

        public class RegionOption : TMP_Dropdown.OptionData, IComparable {
            public string Region { get; private set; }
            private int _ping = -1;
            public int Ping {
                get => _ping;
                set {
                    if (value <= 0) {
                        value = -1;
                    }

                    _ping = value;
                    text = "<align=left>" + Region + "<line-height=0>\n<align=right>" + Utils.Utils.GetPingSymbol(_ping);
                }
            }

            public RegionOption(string region, int ping) {
                Region = region;
                Ping = ping;
            }

            public int CompareTo(object other) {
                if (other is not RegionOption ro) {
                    return -1;
                }

                if (Ping <= 0) {
                    return 1;
                }

                if (ro.Ping <= 0) {
                    return -1;
                }

                return Ping.CompareTo(ro.Ping);
            }
        }
    }
}
