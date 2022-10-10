using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class MainMenuManager : MonoBehaviour {

    public const int NICKNAME_MIN = 2, NICKNAME_MAX = 20;

    public static MainMenuManager Instance;

    public AudioSource sfx, music;
    public GameObject lobbiesContent, lobbyPrefab;
    bool quit, validName;
    public GameObject connecting;

    public GameObject title, bg, mainMenu, optionsMenu, lobbyMenu, createLobbyPrompt, inLobbyMenu, creditsMenu, controlsMenu, privatePrompt, updateBox;
    public GameObject[] levelCameraPositions;
    public GameObject sliderText, lobbyText, currentMaxPlayers, settingsPanel;
    public TMP_Dropdown levelDropdown, characterDropdown;
    public RoomIcon selectedRoom, privateJoinRoom;
    public Button joinRoomBtn, createRoomBtn, startGameBtn;
    public Toggle ndsResolutionToggle, fullscreenToggle, fireballToggle, vsyncToggle, privateToggle, aspectToggle, spectateToggle, scoreboardToggle, filterToggle;
    public GameObject playersContent, playersPrefab, chatContent, chatPrefab;
    public TMP_InputField nicknameField, lobbyJoinField, chatTextField;
    public Slider musicSlider, sfxSlider, masterSlider, lobbyPlayersSlider;
    public GameObject mainMenuSelected, optionsSelected, lobbySelected, currentLobbySelected, createLobbySelected, creditsSelected, controlsSelected, privateSelected, reconnectSelected, updateBoxSelected;
    public GameObject errorBox, errorButton, rebindPrompt, reconnectBox;
    public TMP_Text errorText, rebindCountdown, rebindText, reconnectText, updateText;
    public TMP_Dropdown region;
    public RebindManager rebindManager;
    public static string lastRegion;
    public string connectThroughSecret = "";
    public bool askedToJoin;

    //---Serialize Fields
    [SerializeField] public PlayerListHandler playerList;
    [SerializeField] private ColorChooser colorManager;
    [SerializeField] public ChatManager chat;
    [SerializeField] public RoomSettingsCallbacks roomSettingsCallbacks;

    public Image overallColor, shirtColor;
    public GameObject palette, paletteDisabled;

    public ScrollRect settingsScroll;

    public Selectable[] roomSettings;

    public List<string> maps;

    private readonly Dictionary<string, RoomIcon> currentRooms = new();


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
            region.SetValueWithoutNotify(index);
    }

    // ROOM CALLBACKS

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
        waitingForJoinMessage.Add(player);
    }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        chat.LocalChatMessage(player.GetPlayerData(runner).GetNickname() + " left the room", Color.red);
        sfx.PlayOneShot(Enums.Sounds.UI_PlayerDisconnect.GetClip());
    }

    // CONNECTION CALLBACKS
    public void OnShutdown(NetworkRunner runner, ShutdownReason cause) {

        if (cause != ShutdownReason.Ok)
            OpenErrorBox(cause);

        selectedRoom = null;
        if (!runner.IsCloudReady) {

            foreach ((string key, RoomIcon value) in currentRooms.ToArray()) {
                Destroy(value);
                currentRooms.Remove(key);
            }

        }
    }


    //public void OnRegionListReceived(RegionHandler handler) {
    //    handler.PingMinimumOfRegions((handler) => {

    //        formattedRegions = new();
    //        pingSortedRegions = handler.EnabledRegions.ToArray();
    //        System.Array.Sort(pingSortedRegions, NetworkUtils.PingComparer);

    //        foreach (Region r in pingSortedRegions)
    //            formattedRegions.Add($"{r.Code} <color=#bbbbbb>({(r.Ping == 4000 ? "N/A" : r.Ping + "ms")})");

    //        lastRegion = pingSortedRegions[0].Code;
    //        pingsReceived = true;
    //    }, "");
    //}
    // MATCHMAKING CALLBACKS
    //case (byte) Enums.NetEventIds.PlayerChatMessage: {
    //}

    // Unity Stuff
    public void Start() {

        /*
         * dear god this needs a refactor. does every UI element seriously have to have
         * their callbacks into this one fuckin script?
         */
        Instance = this;

        //Clear game-specific settings so they don't carry over
        HorizontalCamera.OFFSET_TARGET = 0;
        HorizontalCamera.OFFSET = 0;
        Time.timeScale = 1;

        if (GlobalController.Instance.disconnectCause != null) {
            OpenErrorBox(GlobalController.Instance.disconnectCause.Value);
            GlobalController.Instance.disconnectCause = null;
        }

        Camera.main.transform.position = levelCameraPositions[UnityEngine.Random.Range(0, maps.Count)].transform.position;
        levelDropdown.AddOptions(maps);

        //Photon stuff.
        if (!Runner.IsCloudReady) {
            //initial connection to the game
            OpenTitleScreen();
            LoadSettings(true);
            _ = NetworkHandler.ConnectToRegion();

        } else if (Runner.SessionInfo.IsValid) {
            //call enterroom callback
            EnterRoom();
        }

        region.ClearOptions();
        region.AddOptions(NetworkHandler.Regions.ToList());
        //TODO: change to current region


        lobbyPrefab = lobbiesContent.transform.Find("Template").gameObject;
        nicknameField.characterLimit = NICKNAME_MAX;

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

        //add loading screen
        SceneManager.LoadScene(1, LoadSceneMode.Additive);
    }

    private void LoadSettings(bool nickname) {
        if (nickname)
            nicknameField.text = Settings.Instance.nickname;
        else
            nicknameField.SetTextWithoutNotify(Settings.Instance.nickname);

        musicSlider.value = Settings.Instance.VolumeMusic;
        sfxSlider.value = Settings.Instance.VolumeSFX;
        masterSlider.value = Settings.Instance.VolumeMaster;

        aspectToggle.interactable = ndsResolutionToggle.isOn = Settings.Instance.ndsResolution;
        aspectToggle.isOn = Settings.Instance.fourByThreeRatio;
        fullscreenToggle.isOn = Screen.fullScreenMode == FullScreenMode.FullScreenWindow;
        fireballToggle.isOn = Settings.Instance.fireballFromSprint;
        vsyncToggle.isOn = Settings.Instance.vsync;
        scoreboardToggle.isOn = Settings.Instance.scoreboardAlways;
        filterToggle.isOn = Settings.Instance.filter;
        QualitySettings.vSyncCount = Settings.Instance.vsync ? 1 : 0;
    }

    public void OnEnable() {
        NetworkHandler.OnPlayerJoined += OnPlayerJoined;
        NetworkHandler.OnPlayerLeft += OnPlayerLeft;
        NetworkHandler.OnLobbyConnect += OnLobbyConnect;
        NetworkHandler.OnSessionListUpdated += OnSessionListUpdated;
        NetworkHandler.OnShutdown += OnShutdown;
        NetworkHandler.OnJoinSessionFailed += OnShutdown;
    }

    public void OnDisable() {
        NetworkHandler.OnPlayerJoined -= OnPlayerJoined;
        NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
        NetworkHandler.OnLobbyConnect -= OnLobbyConnect;
        NetworkHandler.OnSessionListUpdated -= OnSessionListUpdated;
        NetworkHandler.OnShutdown -= OnShutdown;
        NetworkHandler.OnJoinSessionFailed -= OnShutdown;
    }

    public void Update() {
        bool connected = Runner && Runner.State == NetworkRunner.States.Starting && Runner.IsCloudReady;
        connecting.SetActive(!connected && lobbyMenu.activeInHierarchy);
        privateJoinRoom.gameObject.SetActive(connected);

        joinRoomBtn.interactable = connected && selectedRoom != null && validName;
        createRoomBtn.interactable = connected && validName;
        region.interactable = connected;
    }

    private List<PlayerRef> waitingForJoinMessage = new();
    public IEnumerator OnPlayerDataValidated(PlayerRef player) {
        yield return null; //wait a frame because reasons
        if (waitingForJoinMessage.Remove(player)) {
            chat.LocalChatMessage(player.GetPlayerData(Runner).GetNickname() + " joined the room", Color.red);
            sfx.PlayOneShot(Enums.Sounds.UI_PlayerConnect.GetClip());
        }

        playerList.AddPlayerEntry(player);
    }

    private IEnumerator UpdateUsernames() {
        while (Runner.SessionInfo?.IsValid ?? false) {
            playerList.UpdateAllPlayerEntries();
            yield return new WaitForSecondsRealtime(2f);
        }
    }

    public void EnterRoom() {

        if (LobbyData.Instance.GameStarted) {
            //start early, we're joining late.
            StartGame();
            return;
        }

        StartCoroutine(UpdateUsernames());
        StartCoroutine(SetScroll());

        PlayerData data = Runner.GetLocalPlayerData();
        characterDropdown.SetValueWithoutNotify(data?.CharacterIndex ?? Settings.Instance.character);
        SetPlayerSkin(data?.SkinIndex ?? Settings.Instance.skin);
        spectateToggle.isOn = data?.IsManualSpectator ?? false;

        OpenInLobbyMenu();

        if (Runner.IsServer)
            chat.LocalChatMessage("You are the room's host! Click on your player's names to control your room.", Color.red);

        SessionInfo session = Runner.SessionInfo;
        Utils.GetSessionProperty(session, Enums.NetRoomProperties.HostName, out string name);
        SetText(lobbyText, $"{name.ToValidUsername()}'s Lobby", true);

        //clear text field
        chatTextField.SetTextWithoutNotify("");

        GlobalController.Instance.DiscordController.UpdateActivity();
    }

    private IEnumerator SetScroll() {
        settingsScroll.verticalNormalizedPosition = 1;
        yield return null;
        settingsScroll.verticalNormalizedPosition = 1;
    }

    public void OpenTitleScreen() {
        title.SetActive(true);
        bg.SetActive(false);
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyMenu.SetActive(false);
        creditsMenu.SetActive(false);
        privatePrompt.SetActive(false);

        EventSystem.current.SetSelectedGameObject(mainMenuSelected);
    }
    public void OpenMainMenu() {
        title.SetActive(false);
        bg.SetActive(true);
        mainMenu.SetActive(true);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyMenu.SetActive(false);
        creditsMenu.SetActive(false);
        privatePrompt.SetActive(false);
        updateBox.SetActive(false);

        EventSystem.current.SetSelectedGameObject(mainMenuSelected);

    }
    public void OpenLobbyMenu() {
        title.SetActive(false);
        bg.SetActive(true);
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        lobbyMenu.SetActive(true);
        createLobbyPrompt.SetActive(false);
        inLobbyMenu.SetActive(false);
        creditsMenu.SetActive(false);
        privatePrompt.SetActive(false);

        foreach (RoomIcon room in currentRooms.Values)
            room.UpdateUI(room.session);

        EventSystem.current.SetSelectedGameObject(lobbySelected);
    }
    public void OpenCreateLobby() {
        title.SetActive(false);
        bg.SetActive(true);
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        lobbyMenu.SetActive(true);
        createLobbyPrompt.SetActive(true);
        inLobbyMenu.SetActive(false);
        creditsMenu.SetActive(false);
        privatePrompt.SetActive(false);

        privateToggle.isOn = false;

        EventSystem.current.SetSelectedGameObject(createLobbySelected);
    }
    public void OpenOptions() {
        title.SetActive(false);
        bg.SetActive(true);
        mainMenu.SetActive(false);
        optionsMenu.SetActive(true);
        controlsMenu.SetActive(false);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyMenu.SetActive(false);
        creditsMenu.SetActive(false);
        privatePrompt.SetActive(false);

        EventSystem.current.SetSelectedGameObject(optionsSelected);
    }
    public void OpenControls() {
        title.SetActive(false);
        bg.SetActive(true);
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(true);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyMenu.SetActive(false);
        creditsMenu.SetActive(false);
        privatePrompt.SetActive(false);

        EventSystem.current.SetSelectedGameObject(controlsSelected);
    }
    public void OpenCredits() {
        title.SetActive(false);
        bg.SetActive(true);
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyMenu.SetActive(false);
        creditsMenu.SetActive(true);
        privatePrompt.SetActive(false);

        EventSystem.current.SetSelectedGameObject(creditsSelected);
    }
    public void OpenInLobbyMenu() {
        title.SetActive(false);
        bg.SetActive(true);
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyMenu.SetActive(true);
        creditsMenu.SetActive(false);
        privatePrompt.SetActive(false);

        EventSystem.current.SetSelectedGameObject(currentLobbySelected);
    }
    public void OpenPrivatePrompt() {
        privatePrompt.SetActive(true);
        lobbyJoinField.text = "";
        EventSystem.current.SetSelectedGameObject(privateSelected);
    }

    public void OpenErrorBox(ShutdownReason cause) {
        if (!errorBox.activeSelf)
            sfx.PlayOneShot(Enums.Sounds.UI_Error.GetClip());

        errorBox.SetActive(true);
        errorText.text = NetworkUtils.disconnectMessages.GetValueOrDefault(cause, cause.ToString());
        EventSystem.current.SetSelectedGameObject(errorButton);
    }

    public void OpenErrorBox(string text) {
        if (!errorBox.activeSelf)
            sfx.PlayOneShot(Enums.Sounds.UI_Error.GetClip());

        errorBox.SetActive(true);
        errorText.text = text;
        EventSystem.current.SetSelectedGameObject(errorButton);
    }

    public void BackSound() {
        sfx.PlayOneShot(Enums.Sounds.UI_Back.GetClip());
    }

    public void ConfirmSound() {
        sfx.PlayOneShot(Enums.Sounds.UI_Decide.GetClip());
    }

    public void ConnectToDropdownRegion() {
        string targetRegion = NetworkHandler.Regions[region.value];
        if (lastRegion == targetRegion)
            return;

        for (int i = 0; i < lobbiesContent.transform.childCount; i++) {
            GameObject roomObj = lobbiesContent.transform.GetChild(i).gameObject;
            if (roomObj.GetComponent<RoomIcon>().joinPrivate || !roomObj.activeSelf)
                continue;

            Destroy(roomObj);
        }
        selectedRoom = null;
        lastRegion = targetRegion;

        _ = NetworkHandler.ConnectToRegion(targetRegion);
    }

    public async void QuitRoom() {
        OpenLobbyMenu();
        ClearChat();

        await NetworkHandler.ConnectToRegion();
        GlobalController.Instance.DiscordController.UpdateActivity();
    }
    public void StartGame() {

        if (Runner.ActivePlayers.All(ap => ap.GetPlayerData(Runner).IsManualSpectator))
            return;

        //do host related stuff
        if (Runner.IsServer) {
            //set starting
            LobbyData.Instance.SetGameStarted(true);

            //set spectating values for players
            foreach (PlayerRef player in Runner.ActivePlayers) {
                PlayerData data = player.GetPlayerData(Runner);

                if (!data)
                    continue;

                data.IsCurrentlySpectating = data.IsManualSpectator;
            }

            //load the correct scene
            Runner.SetActiveScene(LobbyData.Instance.Level + 2);
        }
    }

    public void SelectRoom(GameObject room) {
        if (selectedRoom)
            selectedRoom.Unselect();

        selectedRoom = room.GetComponent<RoomIcon>();
        selectedRoom.Select();

        joinRoomBtn.interactable = room != null && nicknameField.text.Length >= NICKNAME_MIN;
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
        byte players = (byte) lobbyPlayersSlider.value;

        _ = NetworkHandler.CreateRoom(new() {
            Initialized = (runner) => {
                runner.SessionInfo.IsVisible = !privateToggle.isOn;
            },
        });

        createLobbyPrompt.SetActive(false);
        //ChangeMaxPlayers(players);
    }
    public void ClearChat() {
        for (int i = 0; i < chatContent.transform.childCount; i++) {
            GameObject chatMsg = chatContent.transform.GetChild(i).gameObject;
            if (!chatMsg.activeSelf)
                continue;
            Destroy(chatMsg);
        }
    }

    //public void UpdateSettingEnableStates() {
    //    NetworkRunner runner = NetworkHandler.Instance.runner;
    //    bool host = runner.IsServer;

    //    foreach (Selectable s in roomSettings)
    //        s.interactable = host;

    //    livesField.interactable = host && livesEnabled.isOn;
    //    timeField.interactable = host && timeEnabled.isOn;
    //    drawTimeupToggle.interactable = host && timeEnabled.isOn;

    //    //TODO: add to array
    //    privateToggleRoom.interactable = host;

    //    int realPlayers = NetworkHandler.Instance.runner.ActivePlayers.Where(pl => !pl.GetPlayerData(runner).IsManualSpectator).Count();
    //    startGameBtn.interactable = host && realPlayers >= 1;
    //}


    public void Kick(PlayerRef target) {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        if (target == runner.LocalPlayer) {
            chat.LocalChatMessage("While you can kick yourself, it's probably not what you meant to do.", Color.red);
            return;
        }
        chat.LocalChatMessage($"Successfully kicked {target.GetPlayerData(runner).GetNickname()}", Color.red);
        runner.Disconnect(target);
    }

    public void Promote(PlayerRef target) {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        if (target == runner.LocalPlayer) {
            chat.LocalChatMessage("You are already the host..?", Color.red);
            return;
        }

        //PhotonNetwork.SetMasterClient(target);
        //LocalChatMessage($"Promoted {target.GetUniqueNickname()} to be the host", Color.red);
        chat.LocalChatMessage("Changing hosts is not implemented yet!", Color.red);
    }

    public void Mute(PlayerRef target) {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        if (target == runner.LocalPlayer) {
            chat.LocalChatMessage("While you can mute yourself, it's probably not what you meant to do.", Color.red);
            return;
        }

        PlayerData data = target.GetPlayerData(runner);
        data.IsMuted = !data.IsMuted;

        if (data.IsMuted) {
            chat.LocalChatMessage($"Successfully muted {data.GetNickname()}", Color.red);
        } else {
            chat.LocalChatMessage($"Successfully unmuted {data.GetNickname()}", Color.red);
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
        if (target == Runner.LocalPlayer) {
            chat.LocalChatMessage("While you can ban yourself, it's probably not what you meant to do.", Color.red);
            return;
        }


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

    /*
    private void RunCommand(string[] args) {
        if (Runner.IsClient) {
            chat.LocalChatMessage("You cannot use room commands if you aren't the host!", Color.red);
            return;
        }
        string command = args.Length > 0 ? args[0].ToLower() : "";
        switch (command) {
        case "kick": {
            if (args.Length < 2) {
                LocalChatMessage("Usage: /kick <player name>", Color.red);
                return;
            }
            string strTarget = args[1].ToLower();
            Player target = PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault(pl => pl.GetUniqueNickname().ToLower() == strTarget);
            if (target == null) {
                LocalChatMessage($"Error: Unknown player {args[1]}", Color.red);
                return;
            }
            Kick(target);
            return;
        }
        case "host": {
            if (args.Length < 2) {
                LocalChatMessage("Usage: /host <player name>", Color.red);
                return;
            }
            string strTarget = args[1].ToLower();
            Player target = PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault(pl => pl.GetUniqueNickname().ToLower() == strTarget);
            if (target == null) {
                LocalChatMessage($"Error: Unknown player {args[1]}", Color.red);
                return;
            }
            Promote(target);
            return;
        }
        case "help": {
            string sub = args.Length > 1 ? args[1] : "";
            string msg = sub switch {
                "kick" => "/kick <player name> - Kick a player from the room",
                "ban" => "/ban <player name> - Ban a player from rejoining the room",
                "host" => "/host <player name> - Make a player the host for the room",
                "mute" => "/mute <playername> - Prevents a player from talking in chat",
                //"debug" => "/debug - Enables debug & in-development features",
                _ => "Available commands: /kick, /host, /mute, /ban",
            };
            LocalChatMessage(msg, Color.red);
            return;
        }
        case "mute": {
            if (args.Length < 2) {
                LocalChatMessage("Usage: /mute <player name>", Color.red);
                return;
            }
            string strTarget = args[1].ToLower();
            Player target = PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault(pl => pl.NickName.ToLower() == strTarget);
            if (target == null) {
                LocalChatMessage($"Unknown player {args[1]}", Color.red);
                return;
            }
            Mute(target);
            return;
        }
        case "ban": {
            if (args.Length < 2) {
                LocalChatMessage("Usage: /ban <player name>", Color.red);
                return;
            }
            BanOrUnban(args[1]);
            return;
        }
        }
        LocalChatMessage($"Error: Unknown command. Try /help for help.", Color.red);
        return;
    }
    */

    public void SwapCharacter(TMP_Dropdown dropdown) {
        byte character = (byte) dropdown.value;

        LocalData.Rpc_SetCharacterIndex(character);
        Settings.Instance.character = character;
        Settings.Instance.SaveSettingsToPreferences();

        CharacterData data = GlobalController.Instance.characters[dropdown.value];
        sfx.PlayOneShot(Enums.Sounds.Player_Voice_Selected.GetClip(data));
        colorManager.ChangeCharacter(data);

        byte skin = LocalData.SkinIndex;
        if (skin == 0) {
            paletteDisabled.SetActive(true);
            palette.SetActive(false);
        } else {
            paletteDisabled.SetActive(false);
            palette.SetActive(true);
            PlayerColors colors = GlobalController.Instance.skins[skin].GetPlayerColors(data);
            overallColor.color = colors.overallsColor;
            shirtColor.color = colors.hatColor;
        }
    }

    public void SetPlayerSkin(byte index) {

        if (index == 0) {
            paletteDisabled.SetActive(true);
            palette.SetActive(false);
        } else {
            paletteDisabled.SetActive(false);
            palette.SetActive(true);

            CharacterData character = Runner.GetLocalPlayerData().GetCharacterData();
            PlayerColors colors = GlobalController.Instance.skins[index].GetPlayerColors(character);
            overallColor.color = colors.overallsColor;
            shirtColor.color = colors.hatColor;
        }

        LocalData?.Rpc_SetSkinIndex(index);

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
    private void SetText(GameObject obj, string txt, bool filter) {
        TextMeshProUGUI textComp = obj.GetComponent<TextMeshProUGUI>();
        textComp.text = filter ? txt.Filter() : txt;
    }
    public void OpenLinks() {
        Application.OpenURL("https://github.com/ipodtouch0218/NSMB-MarioVsLuigi/blob/master/LINKS.md");
    }
    public void Quit() {
        if (quit)
            return;

        StartCoroutine(FinishQuitting());
    }
    private IEnumerator FinishQuitting() {
        AudioClip clip = Enums.Sounds.UI_Quit.GetClip();
        sfx.PlayOneShot(clip);
        quit = true;

        yield return new WaitForSecondsRealtime(clip.length);

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
}
