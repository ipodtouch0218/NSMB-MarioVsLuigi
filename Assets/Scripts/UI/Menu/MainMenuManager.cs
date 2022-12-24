using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

using Fusion;
using Fusion.Sockets;
using NSMB.Extensions;
using NSMB.Utils;

public class MainMenuManager : MonoBehaviour {

    //---Static Variables
    public static readonly int NicknameMin = 2, NicknameMax = 20;
    private static readonly WaitForSeconds WaitTwoSeconds = new(2);
    public static MainMenuManager Instance;
    public static string CurrentRegion;

    public AudioSource sfx, music;
    public GameObject lobbiesContent, lobbyPrefab;

    public RoomIcon selectedRoom, privateJoinRoom;
    public Toggle ndsResolutionToggle, fullscreenToggle, fireballToggle, autoSprintToggle, vsyncToggle, privateToggle, aspectToggle, spectateToggle, scoreboardToggle, filterToggle;
    public GameObject playersContent, playersPrefab, chatContent, chatPrefab;
    public Slider musicSlider, sfxSlider, masterSlider, lobbyPlayersSlider;
    public GameObject mainMenuSelected, optionsSelected, lobbySelected, currentLobbySelected, createLobbySelected, creditsSelected, controlsSelected, privateSelected, updateBoxSelected;
    public GameObject errorBox, errorButton, rebindPrompt;
    public TMP_Text errorText, rebindCountdown, rebindText, lobbyHeaderText, updateText;
    public RebindManager rebindManager;
    public string connectThroughSecret = "";
    public bool askedToJoin;

    //---Serialized Fields
    [SerializeField] public PlayerListHandler playerList;
    [SerializeField] private ColorChooser colorManager;
    [SerializeField] public ChatManager chat;
    [SerializeField] public RoomSettingsCallbacks roomSettingsCallbacks;
    [SerializeField] private CanvasGroup hostControlsGroup;

    [SerializeField] private GameObject title, bg, mainMenu, optionsMenu, lobbyMenu, createLobbyPrompt, inLobbyMenu, creditsMenu, controlsMenu, privatePrompt, updateBox, connecting;
    [SerializeField] private GameObject sliderText, currentMaxPlayers, settingsPanel;
    [SerializeField] private TMP_Dropdown levelDropdown, characterDropdown, regionDropdown;
    [SerializeField] private Button joinRoomBtn, createRoomBtn, startGameBtn;
    [SerializeField] private TMP_InputField nicknameField, lobbyJoinField, chatTextField;
    [SerializeField] private ScrollRect settingsScroll;

    [SerializeField] private Image overallsColorImage, shirtColorImage;
    [SerializeField] private GameObject playerColorPaletteIcon, playerColorDisabledIcon;

    [SerializeField] private List<MapData> maps;

    //---PrivateVariables
    private readonly Dictionary<string, RoomIcon> currentRooms = new();
    private Coroutine playerPingUpdateCoroutine, quitCoroutine;
    private bool validName;


    //---Properties
    private NetworkRunner Runner => NetworkHandler.Instance.runner;
    private PlayerData LocalData => Runner.GetLocalPlayerData();

    // LOBBY CALLBACKS
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {

        List<string> invalidRooms = currentRooms.Keys.ToList();

        foreach (SessionInfo session in sessionList) {

            Utils.GetSessionProperty(session, Enums.NetRoomProperties.Lives, out int lives);
            Utils.GetSessionProperty(session, Enums.NetRoomProperties.StarRequirement, out int stars);
            Utils.GetSessionProperty(session, Enums.NetRoomProperties.CoinRequirement, out int coins);
            Utils.GetSessionProperty(session, Enums.NetRoomProperties.HostName, out string host);
            Utils.GetSessionProperty(session, Enums.NetRoomProperties.MaxPlayers, out int players);

            bool valid = true;
            valid &= session.IsVisible && session.IsOpen;
            valid &= session.MaxPlayers > 0 && session.MaxPlayers <= 10;
            valid &= players > 0 && players <= 10;
            valid &= lives <= 99;
            valid &= stars >= 1 && stars <= 99;
            valid &= coins >= 1 && coins <= 99;
            valid &= host.IsValidUsername();

            if (valid) {
                invalidRooms.Remove(session.Name);
            } else {
                continue;
            }

            RoomIcon roomIcon;
            if (currentRooms.ContainsKey(session.Name)) {
                roomIcon = currentRooms[session.Name];
            } else {
                GameObject newLobby = Instantiate(lobbyPrefab, Vector3.zero, Quaternion.identity);
                newLobby.name = session.Name;
                newLobby.SetActive(true);
                newLobby.transform.SetParent(lobbiesContent.transform, false);

                currentRooms[session.Name] = roomIcon = newLobby.GetComponent<RoomIcon>();
                roomIcon.session = session;
            }

            roomIcon.UpdateUI(session);
        }

        foreach (string key in invalidRooms) {
            if (!currentRooms.ContainsKey(key))
                continue;

            Destroy(currentRooms[key].gameObject);
            currentRooms.Remove(key);
        }

        if (askedToJoin && selectedRoom != null) {
            JoinSelectedRoom();
            askedToJoin = false;
            selectedRoom = null;
        }

        privateJoinRoom.transform.SetAsFirstSibling();
    }

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
        chat.AddChatMessage(player.GetPlayerData(runner).GetNickname() + " left the room", Color.red);
        sfx.PlayOneShot(Enums.Sounds.UI_PlayerDisconnect);
        UpdateStartGameButton();
    }

    // CONNECTION CALLBACKS
    public void OnShutdown(NetworkRunner runner, ShutdownReason cause) {
        if (cause != ShutdownReason.Ok)
            OpenErrorBox(cause);

        selectedRoom = null;
        GlobalController.Instance.loadingCanvas.gameObject.SetActive(false);
        if (!runner.IsCloudReady) {
            foreach ((string key, RoomIcon value) in currentRooms.ToArray()) {
                Destroy(value.gameObject);
                currentRooms.Remove(key);
            }
        }
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason cause) {
        OpenErrorBox(cause);

        selectedRoom = null;
        if (!runner.IsCloudReady) {
            foreach ((string key, RoomIcon value) in currentRooms.ToArray()) {
                Destroy(value.gameObject);
                currentRooms.Remove(key);
            }
        }
    }

    // Unity Stuff
    public void Awake() {
        Instance = this;
    }

    public void Start() {
        //Clear game-specific settings so they don't carry over
        HorizontalCamera.SizeIncreaseTarget = 0;
        HorizontalCamera.SizeIncreaseCurrent = 0;
        Time.timeScale = 1;

        if (GlobalController.Instance.disconnectCause != null) {
            OpenErrorBox(GlobalController.Instance.disconnectCause.Value);
            GlobalController.Instance.disconnectCause = null;
        }

        //Level Dropdown
        PreviewLevel(UnityEngine.Random.Range(0, maps.Count));
        levelDropdown.AddOptions(maps.Select(md => md.mapName).ToList());

        //Region Dropdown
        regionDropdown.ClearOptions();
        regionDropdown.AddOptions(NetworkHandler.Regions.ToList());

        //Photon stuff.
        ApplySettings();

        if (!Runner.IsCloudReady) {
            //initial connection to the game
            OpenTitleScreen();
            _ = NetworkHandler.ConnectToRegion();

        } else if (Runner.SessionInfo.IsValid) {
            //call enterroom callback
            EnterRoom();
        }


        lobbyPrefab = lobbiesContent.transform.Find("Template").gameObject;
        nicknameField.characterLimit = NicknameMax;
        UpdateNickname();

        rebindManager.Init();

        GlobalController.Instance.DiscordController.UpdateActivity();
        EventSystem.current.SetSelectedGameObject(title);

#if PLATFORM_WEBGL
        fullscreenToggle.interactable = false;
#else
        if (!GlobalController.Instance.checkedForVersion) {
            UpdateChecker.IsUpToDate((upToDate, latestVersion) => {
                if (upToDate)
                    return;

                updateText.text = $"An update is available:\n\nNew Version: {latestVersion}\nCurrent Version: {Application.version}";
                updateBox.SetActive(true);
                EventSystem.current.SetSelectedGameObject(updateBoxSelected);
            });
            GlobalController.Instance.checkedForVersion = true;
        }
#endif
    }

    public void OnEnable() {
        NetworkHandler.OnPlayerJoined +=       OnPlayerJoined;
        NetworkHandler.OnPlayerLeft +=         OnPlayerLeft;
        NetworkHandler.OnLobbyConnect +=       OnLobbyConnect;
        NetworkHandler.OnSessionListUpdated += OnSessionListUpdated;
        NetworkHandler.OnShutdown +=           OnShutdown;
        NetworkHandler.OnJoinSessionFailed +=  OnShutdown;
        NetworkHandler.OnConnectFailed +=      OnConnectFailed;
    }

    public void OnDisable() {
        NetworkHandler.OnPlayerJoined -=       OnPlayerJoined;
        NetworkHandler.OnPlayerLeft -=         OnPlayerLeft;
        NetworkHandler.OnLobbyConnect -=       OnLobbyConnect;
        NetworkHandler.OnSessionListUpdated -= OnSessionListUpdated;
        NetworkHandler.OnShutdown -=           OnShutdown;
        NetworkHandler.OnJoinSessionFailed -=  OnShutdown;
        NetworkHandler.OnConnectFailed -=      OnConnectFailed;
    }

    public void Update() {
        bool connected = Runner && Runner.State == NetworkRunner.States.Starting && Runner.IsCloudReady;
        connecting.SetActive(!connected && lobbyMenu.activeInHierarchy);
        privateJoinRoom.gameObject.SetActive(connected);

        joinRoomBtn.interactable = connected && selectedRoom != null && validName;
        createRoomBtn.interactable = connected && validName;
        regionDropdown.interactable = connected;
    }

    //TODO: refactor, wtf?
    private readonly List<PlayerRef> waitingForJoinMessage = new();
    public IEnumerator OnPlayerDataValidated(PlayerRef player) {
        yield return null; //wait a frame because reasons
        if (waitingForJoinMessage.Remove(player)) {
            chat.AddChatMessage(player.GetPlayerData(Runner).GetNickname() + " joined the room", Color.red);
            sfx.PlayOneShot(Enums.Sounds.UI_PlayerConnect);
        }

        playerList.AddPlayerEntry(player);
    }

    private IEnumerator UpdatePings() {
        while (true) {
            yield return WaitTwoSeconds;
            foreach (PlayerRef player in Runner.ActivePlayers)
                player.GetPlayerData(Runner).Ping = (int) (Runner.GetPlayerRtt(player) * 1000);
        }
    }

    public void EnterRoom() {

        if (SessionData.Instance.GameStarted) {
            OnGameStartChanged();
            return;
        }

        if (Runner.IsServer && playerPingUpdateCoroutine == null)
            playerPingUpdateCoroutine = StartCoroutine(UpdatePings());

        StartCoroutine(SetScroll());

        PlayerData data = Runner.GetLocalPlayerData();
        characterDropdown.SetValueWithoutNotify(data ? data.CharacterIndex : Settings.Instance.character);
        SetPlayerSkin(data ? data.SkinIndex : Settings.Instance.skin);
        spectateToggle.isOn = data ? data.IsManualSpectator : false;

        OpenInLobbyMenu();

        if (Runner.IsServer)
            chat.AddChatMessage("You are the room's host! Click on your players' names to control your room.", Color.red);

        SessionInfo session = Runner.SessionInfo;
        Utils.GetSessionProperty(session, Enums.NetRoomProperties.HostName, out string name);
        bool addS = !name.ToLower().EndsWith("s");
        lobbyHeaderText.text = name.ToValidUsername() + "'" + (addS ? "s" : "") + " Lobby";

        //clear chat input field
        chatTextField.SetTextWithoutNotify("");

        GlobalController.Instance.DiscordController.UpdateActivity(session);

        hostControlsGroup.interactable = data.IsRoomOwner;
        roomSettingsCallbacks.UpdateAllSettings(SessionData.Instance, false);

        PreviewLevel(SessionData.Instance.Level);
    }

    public void PreviewLevel(int levelIndex) {
        if (levelIndex < 0 || levelIndex >= maps.Count)
            levelIndex = 0;

        Camera.main.transform.position = maps[levelIndex].levelPreviewPosition.transform.position;
    }

    private IEnumerator SetScroll() {
        settingsScroll.verticalNormalizedPosition = 1;
        yield return null;
        settingsScroll.verticalNormalizedPosition = 1;
    }

    private void DisableAllMenus() {
        title.SetActive(false);
        bg.SetActive(false);
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyMenu.SetActive(false);
        creditsMenu.SetActive(false);
        privatePrompt.SetActive(false);
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
    public void OpenLobbyMenu() {
        DisableAllMenus();
        bg.SetActive(true);
        lobbyMenu.SetActive(true);

        foreach (RoomIcon room in currentRooms.Values)
            room.UpdateUI(room.session);

        EventSystem.current.SetSelectedGameObject(lobbySelected);
    }
    public void OpenCreateLobby() {
        DisableAllMenus();
        bg.SetActive(true);
        lobbyMenu.SetActive(true);
        createLobbyPrompt.SetActive(true);

        privateToggle.isOn = false;

        EventSystem.current.SetSelectedGameObject(createLobbySelected);
    }
    public void OpenOptions() {
        DisableAllMenus();
        bg.SetActive(true);
        optionsMenu.SetActive(true);

        EventSystem.current.SetSelectedGameObject(optionsSelected);
    }
    public void OpenControls() {
        DisableAllMenus();
        bg.SetActive(true);
        controlsMenu.SetActive(true);

        EventSystem.current.SetSelectedGameObject(controlsSelected);
    }
    public void OpenCredits() {
        DisableAllMenus();
        bg.SetActive(true);
        creditsMenu.SetActive(true);

        EventSystem.current.SetSelectedGameObject(creditsSelected);
    }
    public void OpenInLobbyMenu() {
        DisableAllMenus();
        bg.SetActive(true);
        inLobbyMenu.SetActive(true);

        EventSystem.current.SetSelectedGameObject(currentLobbySelected);
    }
    public void OpenPrivatePrompt() {
        privatePrompt.SetActive(true);
        lobbyJoinField.text = "";
        EventSystem.current.SetSelectedGameObject(privateSelected);
    }

    public void OpenErrorBox(Enum cause) {
        if (!errorBox.activeSelf)
            sfx.PlayOneShot(Enums.Sounds.UI_Error);

        errorBox.SetActive(true);
        errorText.text = NetworkUtils.disconnectMessages.GetValueOrDefault(cause, cause.ToString());
        EventSystem.current.SetSelectedGameObject(errorButton);
    }

    public void OpenErrorBox(string text) {
        if (!errorBox.activeSelf)
            sfx.PlayOneShot(Enums.Sounds.UI_Error);

        errorBox.SetActive(true);
        errorText.text = text;
        EventSystem.current.SetSelectedGameObject(errorButton);
    }

    private void ApplySettings() {
        nicknameField.text = Settings.Instance.nickname;

        musicSlider.value =          Settings.Instance.VolumeMusic;
        sfxSlider.value =            Settings.Instance.VolumeSFX;
        masterSlider.value =         Settings.Instance.VolumeMaster;
        ndsResolutionToggle.isOn =   Settings.Instance.ndsResolution;
        aspectToggle.interactable =  Settings.Instance.ndsResolution;
        aspectToggle.isOn =          Settings.Instance.fourByThreeRatio;
        fireballToggle.isOn =        Settings.Instance.fireballFromSprint;
        autoSprintToggle.isOn =      Settings.Instance.autoSprint;
        vsyncToggle.isOn =           Settings.Instance.vsync;
        scoreboardToggle.isOn =      Settings.Instance.scoreboardAlways;
        filterToggle.isOn =          Settings.Instance.chatFiltering;
        QualitySettings.vSyncCount = Settings.Instance.vsync ? 1 : 0;
        fullscreenToggle.isOn =      Screen.fullScreenMode == FullScreenMode.FullScreenWindow;
    }

    public void BackSound() {
        sfx.PlayOneShot(Enums.Sounds.UI_Back);
    }

    public void ConfirmSound() {
        sfx.PlayOneShot(Enums.Sounds.UI_Decide);
    }

    public void ConnectToDropdownRegion() {
        string targetRegion = NetworkHandler.Regions[regionDropdown.value];
        if (CurrentRegion == targetRegion)
            return;

        foreach (RoomIcon room in currentRooms.Values) {
            if (room.joinPrivate || !room.gameObject.activeSelf)
                continue;

            Destroy(room.gameObject);
        }
        currentRooms.Clear();
        selectedRoom = null;
        CurrentRegion = targetRegion;

        _ = NetworkHandler.ConnectToRegion(targetRegion);
    }

    public async void QuitRoom() {
        OpenLobbyMenu();
        chat.ClearChat();

        if (playerPingUpdateCoroutine != null) {
            StopCoroutine(playerPingUpdateCoroutine);
            playerPingUpdateCoroutine = null;
        }

        await NetworkHandler.ConnectToRegion();
        GlobalController.Instance.DiscordController.UpdateActivity();
    }

    public void OnGameStartChanged() {
        if (SessionData.Instance.GameStarted)
            GlobalController.Instance.loadingCanvas.Initialize();
    }

    public void StartGame() {

        //don't start if everyone's a spectator
        if (Runner.ActivePlayers.All(ap => ap.GetPlayerData(Runner).IsManualSpectator))
            return;

        //do host related stuff
        if (Runner.IsServer) {
            //set starting

            bool teamMode = SessionData.Instance.Teams;
            bool hasTwoTeams = false;
            int teamOne = -1;

            //set spectating values for players
            sbyte count = 0;
            foreach (PlayerRef player in Runner.ActivePlayers) {
                PlayerData data = player.GetPlayerData(Runner);

                if (!data)
                    continue;

                data.IsCurrentlySpectating = data.IsManualSpectator;
                if (data.IsCurrentlySpectating) {
                    data.PlayerId = -1;
                    data.Team = -1;
                } else {
                    data.PlayerId = count;
                    if (!teamMode) {
                        data.Team = count;
                    } else if (data.Team == -1) {
                        data.Team = 0;
                    }

                    if (!hasTwoTeams) {
                        if (teamOne == -1)
                            teamOne = data.Team;
                        else if (teamOne != data.Team)
                            hasTwoTeams = true;
                    }

                    count++;
                }
            }

            //don't start if there's only one team (if not singleplayer)
            if (teamMode && !hasTwoTeams && count != 1)
                return;

            SessionData.Instance.SetGameStarted(true);

            //load the correct scene
            Runner.SetActiveScene(GetCurrentSceneRef());
        }
    }

    public void SelectRoom(GameObject room) {
        if (selectedRoom)
            selectedRoom.Unselect();

        selectedRoom = room.GetComponent<RoomIcon>();
        selectedRoom.Select();

        joinRoomBtn.interactable = room != null && nicknameField.text.Length >= NicknameMin;
    }

    public void JoinSelectedRoom() {
        if (!selectedRoom)
            return;

        if (selectedRoom.joinPrivate) {
            OpenPrivatePrompt();
        } else {
            _ = NetworkHandler.JoinRoom(selectedRoom.session.Name);
        }
    }

    public void JoinSpecificRoom() {
        string id = lobbyJoinField.text.ToUpper();
        int index = id.Length > 0 ? NetworkHandler.RoomIdValidChars.IndexOf(id[0]) : -1;
        if (id.Length < 8 || index < 0 || index >= NetworkHandler.Regions.Length) {
            OpenErrorBox("Invalid Room ID");
            return;
        }

        privatePrompt.SetActive(false);
        _ = NetworkHandler.JoinRoom(id);
    }

    public void CreateRoom() {
        Settings.Instance.nickname = nicknameField.text;
        byte maxPlayers = (byte) lobbyPlayersSlider.value;

        _ = NetworkHandler.CreateRoom(new() {
            IsVisible = !privateToggle.isOn
        }, players: maxPlayers);

        createLobbyPrompt.SetActive(false);
    }

    public void UpdateStartGameButton() {
        PlayerData data = Runner.GetLocalPlayerData();
        if (!data || !data.IsRoomOwner) {
            startGameBtn.interactable = false;
            return;
        }

        IEnumerable<PlayerData> datas = Runner.ActivePlayers.Select(p => p.GetPlayerData(Runner));
        bool validRoomConfig = true;

        int realPlayers = datas.Where(pd => !pd.IsManualSpectator).Count();
        validRoomConfig &= realPlayers >= 1;

        //only do team checks if there's more than one player
        if (SessionData.Instance.Teams && datas.Count() > 1) {
            int teams = datas.Where(pd => !pd.IsManualSpectator).Select(pd => pd.Team).Distinct().Count();
            validRoomConfig &= teams > 1;
        }

        startGameBtn.interactable = validRoomConfig;
    }

    public void Kick(PlayerRef target) {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        if (target == runner.LocalPlayer) {
            chat.AddChatMessage("While you can kick yourself, it's probably not what you meant to do.", Color.red);
            return;
        }
        chat.AddChatMessage($"Successfully kicked {target.GetPlayerData(runner).GetNickname()}", Color.red);
        runner.Disconnect(target);
    }

    public void Promote(PlayerRef target) {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        if (target == runner.LocalPlayer) {
            chat.AddChatMessage("You are already the host..?", Color.red);
            return;
        }

        //PhotonNetwork.SetMasterClient(target);
        //LocalChatMessage($"Promoted {target.GetUniqueNickname()} to be the host", Color.red);
        chat.AddChatMessage("Changing hosts is not implemented yet!", Color.red);
    }

    public void Mute(PlayerRef target) {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        if (target == runner.LocalPlayer) {
            chat.AddChatMessage("While you can mute yourself, it's probably not what you meant to do.", Color.red);
            return;
        }

        PlayerData data = target.GetPlayerData(runner);
        bool newMuteState = !data.IsMuted;
        data.IsMuted = newMuteState;

        if (newMuteState) {
            chat.AddChatMessage($"Successfully muted {data.GetNickname()}", Color.red);
        } else {
            chat.AddChatMessage($"Successfully unmuted {data.GetNickname()}", Color.red);
        }
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
        if (target == runner.LocalPlayer) {
            chat.AddChatMessage("While you can ban yourself, it's probably not what you meant to do.", Color.red);
            return;
        }

        SessionData.Instance.AddBan(target);
        chat.AddChatMessage($"Successfully kicked {target.GetPlayerData(runner).GetNickname()}", Color.red);
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

    public void SwapCharacter(TMP_Dropdown dropdown) {
        byte character = (byte) dropdown.value;

        LocalData.Rpc_SetCharacterIndex(character);
        Settings.Instance.character = character;
        Settings.Instance.SaveSettingsToPreferences();

        CharacterData data = ScriptableManager.Instance.characters[dropdown.value];
        sfx.PlayOneShot(Enums.Sounds.Player_Voice_Selected, data);
        colorManager.ChangeCharacter(data);

        byte skin = LocalData.SkinIndex;
        if (skin == 0) {
            playerColorDisabledIcon.SetActive(true);
            playerColorPaletteIcon.SetActive(false);
        } else {
            playerColorDisabledIcon.SetActive(false);
            playerColorPaletteIcon.SetActive(true);
            PlayerColors colors = ScriptableManager.Instance.skins[skin].GetPlayerColors(data);
            overallsColorImage.color = colors.overallsColor;
            shirtColorImage.color = colors.hatColor;
        }
    }

    public void SetPlayerSkin(byte index) {

        if (index == 0) {
            playerColorDisabledIcon.SetActive(true);
            playerColorPaletteIcon.SetActive(false);
        } else {
            playerColorDisabledIcon.SetActive(false);
            playerColorPaletteIcon.SetActive(true);

            CharacterData character = Runner.GetLocalPlayerData().GetCharacterData();
            PlayerColors colors = ScriptableManager.Instance.skins[index].GetPlayerColors(character);
            overallsColorImage.color = colors.overallsColor;
            shirtColorImage.color = colors.hatColor;
        }

        if (LocalData)
            LocalData.Rpc_SetSkinIndex(index);

        Settings.Instance.skin = index;
        Settings.Instance.SaveSettingsToPreferences();
    }

    private void UpdateNickname() {
        validName = Settings.Instance.nickname.IsValidUsername();
        if (!validName) {
            ColorBlock colors = nicknameField.colors;
            colors.normalColor = new(1, 0.7f, 0.7f, 1);
            colors.highlightedColor = new(1, 0.55f, 0.55f, 1);
            nicknameField.colors = colors;
        } else {
            ColorBlock colors = nicknameField.colors;
            colors.normalColor = Color.white;
            nicknameField.colors = colors;
        }
    }

    public void SetUsername(TMP_InputField field) {
        Settings.Instance.nickname = field.text;
        UpdateNickname();

        Settings.Instance.SaveSettingsToPreferences();
    }

    public void OpenLinks() {
        Application.OpenURL("https://github.com/ipodtouch0218/NSMB-MarioVsLuigi/blob/master/LINKS.md");
    }

    public void Quit() {
        if (quitCoroutine != null)
            return;

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
        public string mapName;
        public GameObject levelPreviewPosition;
        public int buildIndex;
    }
}
