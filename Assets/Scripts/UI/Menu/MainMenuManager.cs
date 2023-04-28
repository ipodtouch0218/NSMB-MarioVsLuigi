using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

using Fusion;
using Fusion.Sockets;
using NSMB.Extensions;
using NSMB.UI.Prompts;
using NSMB.Utils;
using NSMB.Translation;

public class MainMenuManager : Singleton<MainMenuManager> {

    //---Static Variables
    public static readonly int NicknameMin = 2, NicknameMax = 20;

    //---Properties
    private NetworkRunner Runner => NetworkHandler.Instance.runner;
    private PlayerData LocalData => Runner.GetLocalPlayerData();

    //---Public Variables
    public bool nonNetworkShutdown;
    public AudioSource sfx, music;
    public Toggle spectateToggle;
    public GameObject playersContent, playersPrefab, chatContent, chatPrefab;
    public GameObject mainMenuSelected, lobbySelected, currentLobbySelected, creditsSelected, updateBoxSelected, ColorName;
    public byte currentSkin;

    //---Serialized Fields
    [SerializeField] public PlayerListHandler playerList;
    [SerializeField] public RoomListManager roomManager;
    [SerializeField] private ColorChooser colorManager;
    [SerializeField] public ChatManager chat;
    [SerializeField] public RoomSettingsCallbacks roomSettingsCallbacks;
    [SerializeField] private CanvasGroup hostControlsGroup;
    [SerializeField] private NetworkErrorPrompt networkErrorPrompt;

    [SerializeField] private GameObject title, bg, mainMenu, lobbyMenu, createLobbyPrompt, privateRoomIdPrompt, inLobbyMenu, creditsMenu, updateBox, connecting;
    [SerializeField] private GameObject sliderText, currentMaxPlayers, settingsPanel;
    [SerializeField] private GameObject errorBox, errorButton;
    [SerializeField] private TMP_Dropdown levelDropdown, characterDropdown, regionDropdown;
    [SerializeField] private Button createRoomBtn, joinRoomBtn, joinPrivateRoomBtn, reconnectBtn, startGameBtn;
    [SerializeField] private TMP_InputField nicknameField, chatTextField;
    [SerializeField] private TMP_Text errorText, lobbyHeaderText, updateText, startGameButtonText;
    [SerializeField] private ScrollRect settingsScroll;
    [SerializeField] private Slider lobbyPlayersSlider;

    [SerializeField, FormerlySerializedAs("ColorBar")] private Image colorBar;
    [SerializeField] private Image overallsColorImage, shirtColorImage;
    [SerializeField] private GameObject playerColorPaletteIcon, playerColorDisabledIcon;

    [SerializeField] private List<MapData> maps;

    //---Private Variables
    private Coroutine playerPingUpdateCoroutine, quitCoroutine, fadeMusicCoroutine;
    private bool validName;

    public void Awake() => Set(this, false);
    public void OnDestroy() => Release();

    public void OnEnable() {
        // Register callbacks
        NetworkHandler.OnPlayerJoined +=       OnPlayerJoined;
        NetworkHandler.OnPlayerLeft +=         OnPlayerLeft;
        NetworkHandler.OnLobbyConnect +=       OnLobbyConnect;
        NetworkHandler.OnShutdown +=           OnShutdown;
        NetworkHandler.OnJoinSessionFailed +=  OnShutdown;
        NetworkHandler.OnConnectFailed +=      OnConnectFailed;

        GlobalController.Instance.translationManager.OnLanguageChanged += OnLanguageChanged;
        OnLanguageChanged(GlobalController.Instance.translationManager);
    }

    public void OnDisable() {
        // Unregister callbacks
        NetworkHandler.OnPlayerJoined -=       OnPlayerJoined;
        NetworkHandler.OnPlayerLeft -=         OnPlayerLeft;
        NetworkHandler.OnLobbyConnect -=       OnLobbyConnect;
        NetworkHandler.OnShutdown -=           OnShutdown;
        NetworkHandler.OnJoinSessionFailed -=  OnShutdown;
        NetworkHandler.OnConnectFailed -=      OnConnectFailed;
        GlobalController.Instance.translationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    public void Start() {
        // Clear game-specific settings so they don't carry over
        HorizontalCamera.SizeIncreaseTarget = 0;
        HorizontalCamera.SizeIncreaseCurrent = 0;

        if (GlobalController.Instance.disconnectCause != null) {
            if (GlobalController.Instance.disconnectCause != ShutdownReason.Ok)
                OpenErrorBox(GlobalController.Instance.disconnectCause.Value);

            GlobalController.Instance.disconnectCause = null;
        }

        PreviewLevel(UnityEngine.Random.Range(0, maps.Count));

        // Region Dropdown
        regionDropdown.ClearOptions();
        regionDropdown.AddOptions(NetworkHandler.Regions.ToList());

        // Photon stuff.
        if (!Runner.IsCloudReady) {
            // Initial connection to the game
            OpenTitleScreen();

        } else if (Runner.SessionInfo.IsValid) {
            // Call enterroom callback
            EnterRoom();
        }

        // Controls & Settings
        nicknameField.text = Settings.Instance.genericNickname;
        nicknameField.characterLimit = NicknameMax;
        UpdateNickname();

        // Discord RPC
        GlobalController.Instance.discordController.UpdateActivity();

        // Version Checking
#if PLATFORM_WEBGL
        fullscreenToggle.interactable = false;
#else
        if (!GlobalController.Instance.checkedForVersion) {
            UpdateChecker.IsUpToDate((upToDate, latestVersion) => {
                if (upToDate)
                    return;

                updateText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.update.prompt", "newversion", latestVersion, "currentversion", Application.version);
                //updateText.text = $"An update is available:\n\nNew Version: {latestVersion}\nCurrent Version: {Application.version}";
                updateBox.SetActive(true);
                EventSystem.current.SetSelectedGameObject(updateBoxSelected);
            });
            GlobalController.Instance.checkedForVersion = true;
        }
#endif

        EventSystem.current.SetSelectedGameObject(title);
    }

    public void Update() {

        bool connectedToNetwork = NetworkHandler.Connected;
        bool connectingToNetwork = NetworkHandler.Connecting;

        connecting.SetActive(connectingToNetwork && lobbyMenu.activeInHierarchy);


        joinRoomBtn.interactable = connectedToNetwork && roomManager.SelectedRoom != null;
        createRoomBtn.interactable = connectedToNetwork && validName;
        //regionDropdown.interactable = connectedToNetwork;

        reconnectBtn.gameObject.SetActive(NetworkHandler.Disconnected);
        joinPrivateRoomBtn.gameObject.SetActive(connectedToNetwork);
    }

    //TODO: refactor, wtf?
    private readonly List<PlayerRef> waitingForJoinMessage = new();
    public IEnumerator OnPlayerDataValidated(PlayerRef player) {
        yield return null; //wait a frame because reasons
        if (waitingForJoinMessage.Remove(player)) {
            chat.AddSystemMessage("ui.inroom.chat.player.joined", "playername", player.GetPlayerData(Runner).GetNickname());
            sfx.PlayOneShot(Enums.Sounds.UI_PlayerConnect);
        }

        playerList.AddPlayerEntry(player);
    }

    public void EnterRoom() {

        // If the game is already started, we want to immediately load in.
        if (SessionData.Instance.GameStarted) {
            OnGameStartChanged();
            return;
        }

        // Open the in-room menu
        OpenInRoomMenu();
        StartCoroutine(ResetRoomSettingScrollPosition());

        // Set the player settings
        PlayerData data = Runner.GetLocalPlayerData();
        characterDropdown.SetValueWithoutNotify(data ? data.CharacterIndex : Settings.Instance.genericCharacter);
        SwapPlayerSkin(data ? data.SkinIndex : (byte) Settings.Instance.genericSkin, false);
        spectateToggle.isOn = data ? data.IsManualSpectator : false;

        // Set the room settings
        hostControlsGroup.interactable = data.IsRoomOwner;
        roomSettingsCallbacks.UpdateAllSettings(SessionData.Instance, false);

        // Preview the current level
        PreviewLevel(SessionData.Instance.Level);

        // Reset chat input field
        chatTextField.SetTextWithoutNotify("");

        // Reset the "Game start" button counting down
        CountdownTick(-1);

        // Host chat notification
        if (Runner.IsServer)
            chat.AddSystemMessage("ui.inroom.chat.hostreminder");

        // Update the room header text
        SessionInfo session = Runner.SessionInfo;
        Utils.GetSessionProperty(session, Enums.NetRoomProperties.HostName, out string name);
        lobbyHeaderText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.rooms.listing.name", "playername", name.ToValidUsername());

        // Discord RPC
        GlobalController.Instance.discordController.UpdateActivity(session);

        // Host-based header color
        UnityEngine.Random.InitState(name.GetHashCode() + 2035767);
        colorBar.color = UnityEngine.Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
    }

    public void PreviewLevel(int levelIndex) {
        if (levelIndex < 0 || levelIndex >= maps.Count)
            levelIndex = 0;

        Camera.main.transform.position = maps[levelIndex].levelPreviewPosition.transform.position;
    }

    private IEnumerator ResetRoomSettingScrollPosition() {
        settingsScroll.verticalNormalizedPosition = 1;
        yield return null;
        settingsScroll.verticalNormalizedPosition = 1;
    }

    private void DisableAllMenus() {
        title.SetActive(false);
        bg.SetActive(false);
        mainMenu.SetActive(false);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyMenu.SetActive(false);
        creditsMenu.SetActive(false);
        privateRoomIdPrompt.SetActive(false);
        updateBox.SetActive(false);
    }

    public void OpenTitleScreen() {
        DisableAllMenus();
        title.SetActive(true);

        EventSystem.current.SetSelectedGameObject(mainMenuSelected);
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

        if (NetworkHandler.Disconnected)
            Reconnect();

        roomManager.RefreshRooms();

        EventSystem.current.SetSelectedGameObject(lobbySelected);
    }
    public void OpenCreateRoomPrompt() {
        DisableAllMenus();
        bg.SetActive(true);
        lobbyMenu.SetActive(true);
        createLobbyPrompt.SetActive(true);
    }
    public void OpenOptions() {
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
        if (!errorBox.activeSelf)
            sfx.PlayOneShot(Enums.Sounds.UI_Error);

        errorBox.SetActive(true);
        errorText.text = GlobalController.Instance.translationManager.GetTranslation(NetworkUtils.disconnectMessages.GetValueOrDefault(cause, cause.ToString()));
        EventSystem.current.SetSelectedGameObject(errorButton);
    }

    public void OpenErrorBox(string text) {
        if (!errorBox.activeSelf)
            sfx.PlayOneShot(Enums.Sounds.UI_Error);

        errorBox.SetActive(true);
        errorText.text = text;
        EventSystem.current.SetSelectedGameObject(errorButton);
        nonNetworkShutdown = false;
    }

    public void OpenNetworkErrorBox(string text) {
        networkErrorPrompt.OpenWithText(text);
    }

    public void OpenNetworkErrorBox(ShutdownReason reason) {
        if (nonNetworkShutdown) {
            OpenErrorBox(reason);
            return;
        }

        OpenNetworkErrorBox(NetworkUtils.disconnectMessages.GetValueOrDefault(reason, reason.ToString()));
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
        string targetRegion = NetworkHandler.Regions[regionDropdown.value];
        if (NetworkHandler.CurrentRegion == targetRegion)
            return;

        roomManager.ClearRooms();
        NetworkHandler.CurrentRegion = targetRegion;

        _ = NetworkHandler.ConnectToRegion(targetRegion);
    }

    public void Reconnect() {
        _ = NetworkHandler.ConnectToSameRegion();
    }

    public async void QuitRoom() {
        OpenRoomListMenu();
        chat.ClearChat();

        if (playerPingUpdateCoroutine != null) {
            StopCoroutine(playerPingUpdateCoroutine);
            playerPingUpdateCoroutine = null;
        }

        await NetworkHandler.ConnectToRegion();
        GlobalController.Instance.discordController.UpdateActivity();
    }

    public void OnGameStartChanged() {
        if (SessionData.Instance.GameStarted)
            GlobalController.Instance.loadingCanvas.Initialize();
    }

    public void StartCountdown() {

        // We can't start the game if we're not the server.
        if (!Runner.IsServer)
            return;

        if (SessionData.Instance.GameStartTimer.IsRunning) {
            // Cancel early.
            SessionData.Instance.GameStartTimer = TickTimer.None;
            sfx.PlayOneShot(Enums.Sounds.UI_Back);

        } else {
            // Make sure we can actually start the game
            if (!IsRoomConfigurationValid())
                return;

            // Actually start the game.
            SessionData.Instance.GameStartTimer = TickTimer.CreateFromSeconds(Runner, 3f);
        }
    }

    public void CountdownTick(int time) {
        TranslationManager tm = GlobalController.Instance.translationManager;
        if (time > 0) {
            startGameButtonText.text = tm.GetTranslationWithReplacements("ui.inroom.buttons.starting", "countdown", time.ToString());
            hostControlsGroup.interactable = false;
            if (time == 1 && fadeMusicCoroutine == null)
                fadeMusicCoroutine = StartCoroutine(FadeMusic());
        } else {
            startGameButtonText.text = tm.GetTranslation("ui.inroom.buttons.start");
            PlayerData data = Runner.GetLocalPlayerData();
            hostControlsGroup.interactable = data ? data.IsRoomOwner : true;
            if (fadeMusicCoroutine != null) {
                StopCoroutine(fadeMusicCoroutine);
                fadeMusicCoroutine = null;
            }
            music.volume = 1;
        }
    }

    private IEnumerator FadeMusic() {
        while (music.volume > 0) {
            music.volume -= Time.deltaTime;
            yield return null;
        }
    }

    public void StartGame() {

        // Set PlayerIDs and spectator values for players
        sbyte count = 0;
        foreach (PlayerRef player in Runner.ActivePlayers) {
            PlayerData data = player.GetPlayerData(Runner);
            if (!data)
                continue;

            data.IsCurrentlySpectating = data.IsManualSpectator;
            data.IsLoaded = false;

            if (data.IsCurrentlySpectating) {
                data.PlayerId = -1;
                data.Team = -1;
                continue;
            }

            data.PlayerId = count;
            if (!SessionData.Instance.Teams) {
                data.Team = count;
            } else if (data.Team == -1) {
                data.Team = 0;
            }

            count++;
        }

        SessionData.Instance.SetGameStarted(true);

        // Load the correct scene
        Runner.SetActiveScene(GetCurrentSceneRef());
    }

    public void UpdateStartGameButton() {
        PlayerData data = Runner.GetLocalPlayerData();
        if (!data || !data.IsRoomOwner) {
            startGameBtn.interactable = false;
            return;
        }

        startGameBtn.interactable = IsRoomConfigurationValid();
    }

    public bool IsRoomConfigurationValid() {
        List<PlayerData> nonSpectators = Runner.ActivePlayers.Select(p => p.GetPlayerData(Runner)).Where(pd => !pd.IsManualSpectator).ToList();
        bool validRoomConfig = true;

        int realPlayers = nonSpectators.Count();
        validRoomConfig &= realPlayers >= 1;

        //only do team checks if there's more than one player
        if (SessionData.Instance.Teams && realPlayers > 1) {
            int teams = nonSpectators.Select(pd => pd.Team).Distinct().Count();
            validRoomConfig &= teams > 1;
        }

        return validRoomConfig;
    }

    public void Kick(PlayerRef target) {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        if (target == runner.LocalPlayer)
            return;

        chat.AddSystemMessage("ui.inroom.chat.player.kicked", "playername", target.GetPlayerData(runner).GetNickname());
        runner.Disconnect(target);
    }

    public void Promote(PlayerRef target) {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        if (target == runner.LocalPlayer)
            return;

        //PhotonNetwork.SetMasterClient(target);
        //LocalChatMessage($"Promoted {target.GetUniqueNickname()} to be the host", Color.red);
        chat.AddChatMessage("Changing hosts is not implemented yet!", PlayerRef.None, Color.red);
    }

    public void Mute(PlayerRef target) {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        if (target == runner.LocalPlayer)
            return;


        PlayerData data = target.GetPlayerData(runner);
        bool newMuteState = !data.IsMuted;
        data.IsMuted = newMuteState;
        chat.AddSystemMessage(newMuteState ? "ui.inroom.chat.player.muted" : "ui.inroom.chat.player.unmuted", "playername", data.GetNickname());
    }

    public void BanOrUnban(string playername) {
        //Player onlineTarget = PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault(pl => pl.GetUniqueNickname().ToLower() == playername);
        //if (onlineTarget != null) {
        //    //player is in room, ban them
        //    Ban(onlineTarget);
        //    return;
        //}

        //Utils.GetSessionProperty(Enums.NetRoomProperties.Bans, out object[] bans);
        //List<NameIdPair> pairs = bans.Cast<NameIdPair>().ToList();

        //playername = playername.ToLower();

        //NameIdPair targetPair = pairs.FirstOrDefault(nip => nip.name.ToLower() == playername);
        //if (targetPair != null) {
        //    //player is banned, unban them
        //    Unban(targetPair);
        //    return;
        //}

        //LocalChatMessage($"Error: Unknown player {playername}", Color.red);
    }

    public void Ban(PlayerRef target) {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        if (target == runner.LocalPlayer)
            return;

        SessionData.Instance.AddBan(target);
        chat.AddSystemMessage("ui.inroom.chat.player.banned", "playername", "target.GetPlayerData(runner).GetNickname()");
        Runner.Disconnect(target);

        //Utils.GetSessionProperty(Enums.NetRoomProperties.Bans, out object[] bans);
        //List<NameIdPair> pairs = bans.Cast<NameIdPair>().ToList();

        //NameIdPair newPair = new() {
        //    name = target.NickName,
        //    userId = target.UserId
        //};

        //pairs.Add(newPair);

        //Hashtable table = new() {
        //    [Enums.NetRoomProperties.Bans] = pairs.ToArray(),
        //};
        //PhotonNetwork.CurrentRoom.SetCustomProperties(table, null, NetworkUtils.forward);

        //Runner.Disconnect(target);
        //LocalChatMessage($"Successfully banned {target.GetUniqueNickname()}", Color.red);
    }

    private void Unban() {
        //TODO:
        //Utils.GetCustomProperty(Enums.NetRoomProperties.Bans, out object[] bans);
        //List<NameIdPair> pairs = bans.Cast<NameIdPair>().ToList();

        //pairs.Remove(targetPair);

        //Hashtable table = new() {
            //[Enums.NetRoomProperties.Bans] = pairs.ToArray(),
        //};
        //PhotonNetwork.CurrentRoom.SetCustomProperties(table, null, NetworkUtils.forward);
        //LocalChatMessage($"Successfully unbanned {targetPair.name}", Color.red);
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

        Settings.Instance.genericCharacter = character;
        Settings.Instance.SaveSettings();

        CharacterData data = ScriptableManager.Instance.characters[character];
        colorManager.ChangeCharacter(data);
        SwapPlayerSkin(currentSkin, false);
    }

    public void SwapPlayerSkin(byte index, bool callback) {

        bool disabled = index == 0;

        if (!disabled) {
            playerColorDisabledIcon.SetActive(false);
            playerColorPaletteIcon.SetActive(true);

            CharacterData character = Runner.GetLocalPlayerData().GetCharacterData();
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
            Settings.Instance.genericSkin = index;
            Settings.Instance.SaveSettings();
        }

        currentSkin = index;
    }

    private void UpdateNickname() {
        validName = Settings.Instance.genericNickname.IsValidUsername();
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
        Settings.Instance.genericNickname = field.text;
        UpdateNickname();

        Settings.Instance.SaveSettings();
    }

    public void OpenLinks() {
        Application.OpenURL("https://github.com/ipodtouch0218/NSMB-MarioVsLuigi/blob/master/LINKS.md");
    }

    public void Quit() {
        if (quitCoroutine == null)
            quitCoroutine = StartCoroutine(FinishQuitting());
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

    private SceneRef GetCurrentSceneRef() {
        if (!SessionData.Instance)
            return SceneRef.None;

        byte index = SessionData.Instance.Level;
        return maps[index].buildIndex;
    }

    // Network callbacks
    // LOBBY CALLBACKS
    public void OnLobbyConnect(NetworkRunner runner, LobbyInfo info) {
        int index = Array.IndexOf(NetworkHandler.Regions, info.Region);

        if (index != -1)
            regionDropdown.SetValueWithoutNotify(index);
    }

    // ROOM CALLBACKS
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
        waitingForJoinMessage.Add(player);
        UpdateStartGameButton();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        chat.AddSystemMessage("ui.inroom.chat.player.quit", "playername", player.GetPlayerData(runner).GetNickname());
        sfx.PlayOneShot(Enums.Sounds.UI_PlayerDisconnect);
        UpdateStartGameButton();
    }

    // CONNECTION CALLBACKS
    public void OnShutdown(NetworkRunner runner, ShutdownReason cause) {
        if (cause != ShutdownReason.Ok)
            OpenNetworkErrorBox(cause);

        if (inLobbyMenu.activeSelf) {
            OpenRoomListMenu();
        }

        GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason cause) {
        OpenErrorBox(cause);

        if (!runner.IsCloudReady) {
            roomManager.ClearRooms();
        }
    }

    private void OnLanguageChanged(TranslationManager tm) {
        levelDropdown.ClearOptions();
        levelDropdown.AddOptions(maps.Select(map => tm.GetTranslation(map.translationKey)).ToList());
    }

    //---Debug
#if UNITY_EDITOR
    private static readonly Vector3 MaxCameraSize = new(16f/9f * 7f, 7f);
    public void OnDrawGizmos() {
        Gizmos.color = Color.red;
        foreach (MapData map in maps) {
            if (map.levelPreviewPosition)
                Gizmos.DrawWireCube(map.levelPreviewPosition.transform.position, MaxCameraSize);
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
}
