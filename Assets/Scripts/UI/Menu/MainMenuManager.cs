using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour, ILobbyCallbacks, IInRoomCallbacks, IOnEventCallback, IConnectionCallbacks, IMatchmakingCallbacks {
    public static MainMenuManager Instance;
    public AudioSource sfx, music;
    public GameObject lobbiesContent, lobbyPrefab;
    bool quit, validName;
    public GameObject connecting;
    public GameObject title, bg, mainMenu, optionsMenu, lobbyMenu, createLobbyPrompt, inLobbyMenu, creditsMenu, controlsMenu, privatePrompt;
    public GameObject[] levelCameraPositions;
    public GameObject sliderText, lobbyText, currentMaxPlayers, settingsPanel;
    public TMP_Dropdown levelDropdown, characterDropdown;
    public RoomIcon selectedRoomIcon, privateJoinRoom;
    public Button joinRoomBtn, createRoomBtn, startGameBtn;
    public Toggle ndsResolutionToggle, fullscreenToggle, livesEnabled, powerupsEnabled, timeEnabled, fireballToggle, vsyncToggle, privateToggle, privateToggleRoom, aspectToggle;
    public GameObject playersContent, playersPrefab, chatContent, chatPrefab;
    public TMP_InputField nicknameField, starsText, livesField, timeField, lobbyJoinField, chatTextField;
    public Slider musicSlider, sfxSlider, masterSlider, lobbyPlayersSlider, changePlayersSlider;
    public GameObject mainMenuSelected, optionsSelected, lobbySelected, currentLobbySelected, createLobbySelected, creditsSelected, controlsSelected, privateSelected, reconnectSelected;
    public GameObject errorBox, errorButton, rebindPrompt, reconnectBox;
    public TMP_Text errorText, rebindCountdown, rebindText, reconnectText;
    public TMP_Dropdown region;
    public RebindManager rebindManager;
    public static string lastRegion;
    public string connectThroughSecret = "";
    public string selectedRoom;
    public bool askedToJoin;

    public Image overallColor, shirtColor;
    public GameObject palette, paletteDisabled;

    public ScrollRect settingsScroll;

    public Selectable[] roomSettings;

    private bool pingsReceived;
    private List<string> formattedRegions;
    private Region[] pingSortedRegions;

    private readonly Dictionary<string, RoomIcon> currentRooms = new();

    private static readonly string roomNameChars = "BCDFGHJKLMNPRQSTVWXYZ";

    Coroutine updatePingCoroutine;

    // LOBBY CALLBACKS
    public void OnJoinedLobby() {
        ExitGames.Client.Photon.Hashtable prop = new() {
            { Enums.NetPlayerProperties.Character, 0 },
            { Enums.NetPlayerProperties.Ping, PhotonNetwork.GetPing() },
            { Enums.NetPlayerProperties.PlayerColor, 0 },
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(prop);

        if (connectThroughSecret != "") {
            PhotonNetwork.JoinRoom(connectThroughSecret);
            connectThroughSecret = "";
        }

        if (updatePingCoroutine == null)
            updatePingCoroutine = StartCoroutine(UpdatePing());
    }
    public void OnLeftLobby() {}
    public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbies) {}
    public void OnRoomListUpdate(List<RoomInfo> roomList) {
        List<string> invalidRooms = new();

        foreach (RoomInfo room in roomList) {
            if (!room.IsVisible || room.RemovedFromList) {
                invalidRooms.Add(room.Name);
                continue;
            }

            RoomIcon roomIcon;
            if (currentRooms.ContainsKey(room.Name)) {
                roomIcon = currentRooms[room.Name];
            } else {
                GameObject newLobby = Instantiate(lobbyPrefab, Vector3.zero, Quaternion.identity);
                newLobby.name = room.Name;
                newLobby.SetActive(true);
                newLobby.transform.SetParent(lobbiesContent.transform, false);

                currentRooms[room.Name] = roomIcon = newLobby.GetComponent<RoomIcon>();
                roomIcon.room = room;
            }
            if (room.Name == selectedRoom) {
                selectedRoomIcon = roomIcon;
            }

            roomIcon.UpdateUI(room);
        }

        foreach (string key in invalidRooms) {
            if (!currentRooms.ContainsKey(key))
                continue;

            Destroy(currentRooms[key].gameObject);
            currentRooms.Remove(key);
        }

        if (askedToJoin && selectedRoomIcon != null) {
            JoinSelectedRoom();
            askedToJoin = false;
            selectedRoom = null;
            selectedRoomIcon = null;
        }

        privateJoinRoom.transform.SetAsLastSibling();
    }

    // ROOM CALLBACKS
    public void OnPlayerPropertiesUpdate(Player player, ExitGames.Client.Photon.Hashtable playerProperties) { }
    public void OnMasterClientSwitched(Player newMaster) {
        LocalChatMessage(newMaster.NickName + " has become the Host", ColorToVector(Color.red));
        
        if (newMaster.IsLocal) {
            //i am de captain now
            PhotonNetwork.CurrentRoom.SetCustomProperties(new() {
                [Enums.NetRoomProperties.HostName] = newMaster.NickName
            });
        }
        UpdateSettingEnableStates();
    }
    public void OnJoinedRoom() {
        LocalChatMessage(PhotonNetwork.LocalPlayer.NickName + " joined the room", ColorToVector(Color.red));

        Room room = PhotonNetwork.CurrentRoom;
        Player local = PhotonNetwork.LocalPlayer;
        if (room.Players.Values.Where(pl => pl != local).Any(pl => pl.NickName.ToLower() == local.NickName.ToLower())) {
            //rename
            int dupes = room.Players.Values.Where(pl => pl != local).Where(pl => Regex.Match(pl.NickName, local.NickName + "\\(\\d\\)").Success).Count();
            for (int i = 1; i <= dupes + 1; i++) {
                string dupeName = PhotonNetwork.NickName + "(" + i + ")"; ;
                if (!room.Players.Values.Any(pl => pl.NickName == dupeName)) {
                    PhotonNetwork.NickName = dupeName;
                    break;
                }
            }
        }

        EnterRoom();
    }
    public void OnPlayerEnteredRoom(Player newPlayer) {
        LocalChatMessage(newPlayer.NickName + " joined the room", ColorToVector(Color.red));
    }
    public void OnPlayerLeftRoom(Player otherPlayer) {
        LocalChatMessage(otherPlayer.NickName + " left the room", ColorToVector(Color.red));
    }
    public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable updatedProperties) {
        if (updatedProperties == null)
            return;

        AttemptToUpdateProperty<int>(updatedProperties, Enums.NetRoomProperties.Level, ChangeLevel);
        AttemptToUpdateProperty<int>(updatedProperties, Enums.NetRoomProperties.StarRequirement, ChangeStarRequirement);
        AttemptToUpdateProperty<int>(updatedProperties, Enums.NetRoomProperties.Lives, ChangeLives);
        AttemptToUpdateProperty<bool>(updatedProperties, Enums.NetRoomProperties.NewPowerups, ChangeNewPowerups);
        AttemptToUpdateProperty<int>(updatedProperties, Enums.NetRoomProperties.Time, ChangeTime);
        AttemptToUpdateProperty<string>(updatedProperties, Enums.NetRoomProperties.HostName, ChangeLobbyHeader);
    }
    private void AttemptToUpdateProperty<T>(ExitGames.Client.Photon.Hashtable updatedProperties, string key, System.Action<T> updateAction) {
        if (updatedProperties[key] == null)
            return;

        updateAction((T) updatedProperties[key]);
    }
    // CONNECTION CALLBACKS
    public void OnConnected() {
        Debug.Log("[PHOTON] Connected to Photon.");
    }
    public void OnDisconnected(DisconnectCause cause) {
        Debug.Log("[PHOTON] Disconnected: " + cause.ToString());
        if (!(cause == DisconnectCause.None || cause == DisconnectCause.DisconnectByClientLogic))
            OpenErrorBox("Disconnected: " + cause.ToString());

        selectedRoom = null;
        selectedRoomIcon = null;
        if (!PhotonNetwork.IsConnectedAndReady)
            PhotonNetwork.ConnectToRegion(lastRegion);
    }
    public void OnRegionListReceived(RegionHandler handler) {
        handler.PingMinimumOfRegions((handler) => {

            formattedRegions = new();
            pingSortedRegions = handler.EnabledRegions.ToArray();
            System.Array.Sort(pingSortedRegions, NetworkUtils.RegionPingComparer);

            foreach (Region r in pingSortedRegions)
                formattedRegions.Add($"{r.Code} <color=#cccccc>({(r.Ping == 4000 ? "N/A" : r.Ping + "ms")})");

            lastRegion = pingSortedRegions[0].Code;
            pingsReceived = true;
        }, "");
    }
    public void OnCustomAuthenticationResponse(Dictionary<string, object> response) {}
    public void OnCustomAuthenticationFailed(string failure) {}
    public void OnConnectedToMaster() {
        JoinMainLobby();
    }
    // MATCHMAKING CALLBACKS
    public void OnFriendListUpdate(List<FriendInfo> friendList) {}
    public void OnLeftRoom() {
        PhotonNetwork.NickName = nicknameField.text;
        OpenLobbyMenu();
        ClearChat();
        GlobalController.Instance.discordController.UpdateActivity();
    }
    public void OnJoinRandomFailed(short reasonId, string reasonMessage) {
        OnJoinRoomFailed(reasonId, reasonMessage);
    }
    public void OnJoinRoomFailed(short reasonId, string reasonMessage) {
        Debug.LogError($"[PHOTON] Join room failed ({reasonId}, {reasonMessage})");
        OpenErrorBox(reasonMessage);
        JoinMainLobby();
    }
    public void OnCreateRoomFailed(short reasonId, string reasonMessage) {
        Debug.LogError($"[PHOTON] Create room failed ({reasonId}, {reasonMessage})");
        OpenErrorBox(reasonMessage);

        OnConnectedToMaster();
    }
    public void OnCreatedRoom() {
        Debug.Log($"[PHOTON] Created Room ({PhotonNetwork.CurrentRoom.Name})");
    }
    // CUSTOM EVENT CALLBACKS
    public void OnEvent(EventData e) {
        switch (e.Code) {
        case (byte) Enums.NetEventIds.StartGame: {
            Utils.GetCustomProperty(Enums.NetRoomProperties.Level, out int level);
            PhotonNetwork.IsMessageQueueRunning = false;
            SceneManager.LoadSceneAsync(1, LoadSceneMode.Single);
            SceneManager.LoadSceneAsync(level + 2, LoadSceneMode.Additive);
            break;
        }
        case (byte) Enums.NetEventIds.ChatMessage: {
            object[] data = (object[]) e.CustomData;
            string message = (string) data[0];
            Vector3 color = (Vector3) data[1];
            LocalChatMessage(message, color);
            break;
        }
        case (byte) Enums.NetEventIds.ChangeMaxPlayers: {
            ChangeMaxPlayers((byte) e.CustomData);
            break;
        }
        case (byte) Enums.NetEventIds.ChangePrivate: {
            ChangePrivate();
            break;
        }
        }
    }

    private void JoinMainLobby() {
        Match match = Regex.Match(Application.version, "^\\w*\\.\\w*\\.\\w*");
        PhotonNetwork.JoinLobby(new TypedLobby(match.Groups[0].Value, LobbyType.Default));
    }

    // CALLBACK REGISTERING
    void OnEnable() {
        PhotonNetwork.AddCallbackTarget(this);
    }
    void OnDisable() {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

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
        GlobalController.Instance.joinedAsSpectator = false;
        Time.timeScale = 1;

        if (GlobalController.Instance.disconnectCause != null) {
            OpenErrorBox("Disconnected: " + GlobalController.Instance.disconnectCause.ToString());
            GlobalController.Instance.disconnectCause = null;
        }

        AudioMixer mixer = music.outputAudioMixerGroup.audioMixer;
        mixer.SetFloat("MusicSpeed", 1f);
        mixer.SetFloat("MusicPitch", 1f);

        Camera.main.transform.position = levelCameraPositions[Random.Range(0, levelCameraPositions.Length)].transform.position;

        //Photon stuff.
        if (!PhotonNetwork.IsConnected) {
            OpenTitleScreen();
            PhotonNetwork.NetworkingClient.AppId = "ce540834-2db9-40b5-a311-e58be39e726a";
            PhotonNetwork.NetworkingClient.ConnectToNameServer();
        } else {
            if (PhotonNetwork.InRoom) {
                EnterRoom();
            } else {
                PhotonNetwork.Disconnect();
            }

            List<string> newRegions = new();
            pingSortedRegions = PhotonNetwork.NetworkingClient.RegionHandler.EnabledRegions.ToArray();
            System.Array.Sort(pingSortedRegions, NetworkUtils.RegionPingComparer);

            int index = 0;
            for (int i = 0; i < pingSortedRegions.Length; i++) {
                Region r = pingSortedRegions[i];
                newRegions.Add($"{r.Code} <color=#cccccc>({(r.Ping == 4000 ? "N/A" : r.Ping + "ms")})");
                if (r.Code == lastRegion)
                    index = i;
            }

            region.ClearOptions();
            region.AddOptions(newRegions);

            region.value = index;
        }


        lobbyPrefab = lobbiesContent.transform.Find("Template").gameObject;

        //Apply
        nicknameField.text = PhotonNetwork.NickName = Settings.Instance.nickname;
        musicSlider.value = Settings.Instance.VolumeMusic;
        sfxSlider.value = Settings.Instance.VolumeSFX;
        masterSlider.value = Settings.Instance.VolumeMaster;

        aspectToggle.interactable = ndsResolutionToggle.isOn = Settings.Instance.ndsResolution;
        aspectToggle.isOn = Settings.Instance.fourByThreeRatio;
        fullscreenToggle.isOn = Screen.fullScreenMode == FullScreenMode.FullScreenWindow;
        fireballToggle.isOn = Settings.Instance.fireballFromSprint;
        vsyncToggle.isOn = Settings.Instance.vsync;
        QualitySettings.vSyncCount = Settings.Instance.vsync ? 1 : 0;

        rebindManager.Init();

        GlobalController.Instance.discordController.UpdateActivity();
        EventSystem.current.SetSelectedGameObject(title);
    }

    void Update() {
        bool connected = PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby;
        connecting.SetActive(!connected);
        privateJoinRoom.gameObject.SetActive(connected);

        joinRoomBtn.interactable = connected && selectedRoomIcon != null && validName;
        createRoomBtn.interactable = connected && validName;
        region.interactable = connected;

        if (pingsReceived) {
            pingsReceived = false;

            region.ClearOptions();
            region.AddOptions(formattedRegions);
            region.value = 0;

            PhotonNetwork.Disconnect();
        }
    }

    IEnumerator UpdatePing() {
        // push our ping into our player properties every N seconds. 2 seems good.
        while (true) {
            yield return new WaitForSecondsRealtime(2);
            if (PhotonNetwork.InRoom) {
                PhotonNetwork.LocalPlayer.SetCustomProperties(new() {
                    { Enums.NetPlayerProperties.Ping, PhotonNetwork.GetPing() }
                });
            }
        }
    }

    void EnterRoom() {
        Room room = PhotonNetwork.CurrentRoom;

        object started = room.CustomProperties[Enums.NetRoomProperties.GameStarted];
        if (started != null && (bool) started) {
            //start as spectator
            GlobalController.Instance.joinedAsSpectator = true;
            OnEvent(new() { Code = (byte) Enums.NetEventIds.StartGame });
            return;
        }
        
        OpenInLobbyMenu();
        characterDropdown.SetValueWithoutNotify(Utils.GetCharacterIndex());

        Utils.GetCustomProperty(Enums.NetPlayerProperties.PlayerColor, out int value, PhotonNetwork.LocalPlayer.CustomProperties);
        SetPlayerColor(value);

        OnRoomPropertiesUpdate(room.CustomProperties);
        ChangeMaxPlayers(room.MaxPlayers);
        ChangePrivate();

        StartCoroutine(SetScroll());

        PhotonNetwork.LocalPlayer.SetCustomProperties(new() {
            [Enums.NetPlayerProperties.GameState] = null,
        });
        if (updatePingCoroutine == null)
            updatePingCoroutine = StartCoroutine(UpdatePing());
        GlobalController.Instance.discordController.UpdateActivity();
    }

    IEnumerator SetScroll() {
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
    public void OpenErrorBox(string text) {
        errorBox.SetActive(true);
        errorText.text = text;
        EventSystem.current.SetSelectedGameObject(errorButton);
    }

    public void ConnectToDropdownRegion() {
        Region targetRegion = pingSortedRegions[region.value];
        if (lastRegion == targetRegion.Code)
            return;

        for (int i = 0; i < lobbiesContent.transform.childCount; i++) {
            GameObject roomObj = lobbiesContent.transform.GetChild(i).gameObject;
            if (roomObj.GetComponent<RoomIcon>().joinPrivate || !roomObj.activeSelf)
                continue;

            Destroy(roomObj);
        }
        selectedRoomIcon = null;
        selectedRoom = null;
        lastRegion = targetRegion.Code;

        PhotonNetwork.Disconnect();
    }

    public void QuitRoom() {
        PhotonNetwork.LeaveRoom();
    }
    public void StartGame() {

        //set started game
        PhotonNetwork.CurrentRoom.SetCustomProperties(new() { [Enums.NetRoomProperties.GameStarted] = true });

        //start game with all players
        RaiseEventOptions options = new() { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.StartGame, null, options, SendOptions.SendReliable);
    }
    public void ChangeNewPowerups(bool value) {
        powerupsEnabled.SetIsOnWithoutNotify(value);
    }

    public void ChangeLives(int lives) {
        livesEnabled.SetIsOnWithoutNotify(lives != -1);
        UpdateSettingEnableStates();
        if (lives == -1)
            return;

        livesField.SetTextWithoutNotify(lives.ToString());
    }
    public void SetLives(TMP_InputField input) {
        if (!PhotonNetwork.IsMasterClient)
            return;

        int.TryParse(input.text, out int newValue);
        if (newValue == -1)
            return;

        if (newValue < 1)
            newValue = 5;
        if (newValue == (int) PhotonNetwork.CurrentRoom.CustomProperties[Enums.NetRoomProperties.Lives])
            return;

        ExitGames.Client.Photon.Hashtable table = new() {
            [Enums.NetRoomProperties.Lives] = newValue
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(table);
        //ChangeLives(newValue);
    }
    public void SetNewPowerups(Toggle toggle) {
        ExitGames.Client.Photon.Hashtable properties = new() {
            [Enums.NetRoomProperties.NewPowerups] = toggle.isOn
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }
    public void EnableLives(Toggle toggle) {
        ExitGames.Client.Photon.Hashtable properties = new() {
            [Enums.NetRoomProperties.Lives] = toggle.isOn ? int.Parse(livesField.text) : -1
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);    
    }
    public void ChangeLevel(int index) {
        levelDropdown.SetValueWithoutNotify(index);
        Camera.main.transform.position = levelCameraPositions[index].transform.position;
    }
    public void SetLevelIndex() {
        if (!PhotonNetwork.IsMasterClient) 
            return;

        int newLevelIndex = levelDropdown.value;
        if (newLevelIndex == (int) PhotonNetwork.CurrentRoom.CustomProperties[Enums.NetRoomProperties.Level]) 
            return;

        GlobalChatMessage("Map set to: " + levelDropdown.captionText.text, ColorToVector(Color.red));

        ExitGames.Client.Photon.Hashtable table = new() {
            [Enums.NetRoomProperties.Level] = levelDropdown.value
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(table);
    }
    public void SelectRoom(GameObject room) {
        if (selectedRoomIcon)
            selectedRoomIcon.Unselect();

        selectedRoomIcon = room.GetComponent<RoomIcon>();
        selectedRoomIcon.Select();
        selectedRoom = selectedRoomIcon.room?.Name ?? null;

        joinRoomBtn.interactable = room != null && PhotonNetwork.NickName.Length >= 3;
    }
    public void JoinSelectedRoom() {
        if (selectedRoomIcon?.joinPrivate ?? false) {
            OpenPrivatePrompt();
            return;
        }
        if (selectedRoom == null)
            return;

        PhotonNetwork.JoinRoom(selectedRoomIcon.room.Name);
    }
    public void JoinSpecificRoom() {
        if (lobbyJoinField.text.Length < 8) {
            OpenErrorBox("Invalid Room ID");
            return;
        }
        PhotonNetwork.JoinRoom(lobbyJoinField.text.ToUpper());
        privatePrompt.SetActive(false);
    }
    public void CreateRoom() {
        byte players = (byte) lobbyPlayersSlider.value;
        string roomName = "";
        for (int i = 0; i < 8; i++)
            roomName += roomNameChars[Random.Range(0, roomNameChars.Length)];

        ExitGames.Client.Photon.Hashtable properties = NetworkUtils.DefaultRoomProperties;
        properties[Enums.NetRoomProperties.HostName] = PhotonNetwork.NickName;

        RoomOptions options = new() {
            MaxPlayers = players,
            IsVisible = !privateToggle.isOn,
            PublishUserId = true,
            CustomRoomProperties = properties,
            CustomRoomPropertiesForLobby = NetworkUtils.LobbyVisibleRoomProperties,
        };
        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
        createLobbyPrompt.SetActive(false);
        ChangeMaxPlayers(players);
    }
    public void ClearChat() {
        for (int i = 0; i < chatContent.transform.childCount; i++) {
            GameObject chatMsg = chatContent.transform.GetChild(i).gameObject;
            if (!chatMsg.activeSelf) 
                continue;
            Destroy(chatMsg);
        }
    }
    public void UpdateSettingEnableStates() {
        foreach (Selectable s in roomSettings)
            s.interactable = PhotonNetwork.IsMasterClient;

        livesField.interactable = PhotonNetwork.IsMasterClient && livesEnabled.isOn;
        timeField.interactable = PhotonNetwork.IsMasterClient && timeEnabled.isOn;
        changePlayersSlider.interactable = PhotonNetwork.IsMasterClient;
    }
    
    public void GlobalChatMessage(string message, Vector3 color) {
        RaiseEventOptions options = new() { Receivers = ReceiverGroup.All };
        object[] parameters = new object[] { message, color };
        PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.ChatMessage, parameters, options, SendOptions.SendReliable);
    }
    public void LocalChatMessage(string message, Vector3 color) {
        float y = 0;
        for (int i = 0; i < chatContent.transform.childCount; i++) {
            GameObject child = chatContent.transform.GetChild(i).gameObject;
            if (!child.activeSelf)
                continue;

            y -= child.GetComponent<RectTransform>().rect.height + 20;
        }

        GameObject chat = Instantiate(chatPrefab, Vector3.zero, Quaternion.identity);
        chat.transform.SetParent(chatContent.transform);
        chat.transform.localPosition = new Vector3(0, y, 0);
        chat.transform.localScale = Vector3.one;
        chat.SetActive(true);
        GameObject txtObject = chat.transform.Find("Text").gameObject;
        SetText(txtObject, message, new Color(color.x, color.y, color.z));
        Canvas.ForceUpdateCanvases();
        RectTransform tf = txtObject.GetComponent<RectTransform>();
        Bounds bounds = txtObject.GetComponent<TextMeshProUGUI>().textBounds;
        tf.sizeDelta = new Vector2(tf.sizeDelta.x, bounds.max.y - bounds.min.y - 15f);
    }
    public void SendChat() {
        string text = chatTextField.text.Trim();
        chatTextField.text = "";
        if (text == null || text == "") {
            return;
        }
        if (text.StartsWith("/")) {
            RunCommand(text[1..].Split(" "));
            return;
        }

        GlobalChatMessage(PhotonNetwork.NickName + ": " + text, ColorToVector(Color.black));
        StartCoroutine(SelectNextFrame(chatTextField));
    }

    private void RunCommand(string[] args) {
        if (!PhotonNetwork.IsMasterClient) {
            LocalChatMessage("You cannot use room commands if you aren't the host!", ColorToVector(Color.red));
            return;
        }
        string command = args.Length > 0 ? args[0].ToLower() : "";
        switch (command) {
        case "kick": {
            if (args.Length < 2) {
                LocalChatMessage("Usage: /kick <player name>", ColorToVector(Color.red));
                return;
            }
            string strTarget = args[1].ToLower();
            Player target = PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault(pl => pl.NickName.ToLower() == strTarget);
            if (target == null) {
                LocalChatMessage($"Unknown player {args[2]}", ColorToVector(Color.red));
                return;
            }
            if (target.IsLocal) {
                LocalChatMessage("While you can kick yourself, it's probably not what you meant to do.", ColorToVector(Color.red));
                return;
            }
            PhotonNetwork.CloseConnection(target);
            LocalChatMessage($"Successfully kicked {target.NickName}", ColorToVector(Color.red));
            return;
        }
        case "host": {
            if (args.Length < 2) {
                LocalChatMessage("Usage: /host <player name>", ColorToVector(Color.red));
                return;
            }
            string strTarget = args[1].ToLower();
            Player target = PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault(pl => pl.NickName.ToLower() == strTarget);
            if (target == null) {
                LocalChatMessage($"Unknown player {args[2]}", ColorToVector(Color.red));
                return;
            }
            if (target.IsLocal) {
                LocalChatMessage("You are already the host..?", ColorToVector(Color.red));
                return;
            }
            PhotonNetwork.SetMasterClient(target);
            LocalChatMessage($"Promoted {target.NickName} to the host!", ColorToVector(Color.red));
            return;
        }
        case "help": {
            string sub = args.Length > 1 ? args[1] : "";
            string msg = sub switch {
                "kick" => "/kick <player name>",
                "host" => "/host <player name>",
                _ => "Available commands: kick, host",
            };
            LocalChatMessage(msg, ColorToVector(Color.red));
            return;
        }
        }
        LocalChatMessage($"Unknown command. Try /help for help.", ColorToVector(Color.red));
        return;
    }

    IEnumerator SelectNextFrame(TMP_InputField input) {
        yield return new WaitForEndOfFrame();
        input.ActivateInputField();
    }
    public static Vector3 ColorToVector(Color color) {
        return new Vector3(color.r, color.g, color.b);
    }
    public void SwapCharacter(TMP_Dropdown dropdown) {
        ExitGames.Client.Photon.Hashtable prop = new() {
            { Enums.NetPlayerProperties.Character, dropdown.value }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(prop);

        PlayerData data = GlobalController.Instance.characters[dropdown.value];
        
        sfx.PlayOneShot(Enums.Sounds.Player_Voice_Selected.GetClip(data));
    }

    public void SetPlayerColor(int index) {
        ExitGames.Client.Photon.Hashtable prop = new() {
            { Enums.NetPlayerProperties.PlayerColor, index }
        };
        if (index == 0) {
            paletteDisabled.SetActive(true);
            palette.SetActive(false);
        } else {
            paletteDisabled.SetActive(false);
            palette.SetActive(true);
            overallColor.color = CustomColors.Colors[index].overalls;
            shirtColor.color = CustomColors.Colors[index].hat;
        }
        PhotonNetwork.LocalPlayer.SetCustomProperties(prop);
    }

    public void SetUsername(TMP_InputField field) {
        PhotonNetwork.NickName = field.text;
        validName = field.text.Length > 2;
        if (!validName) {
            ColorBlock colors = field.colors;
            colors.normalColor = new Color(1, 0.7f, 0.7f, 1);
            colors.highlightedColor = new Color(1, 0.55f, 0.55f, 1);
            field.colors = colors;
        } else {
            ColorBlock colors = field.colors;
            colors.normalColor = Color.white;
            field.colors = colors;
        }

        PlayerPrefs.SetString("Nickname", field.text);
        PlayerPrefs.Save();
    }
    private void SetText(GameObject obj, string txt) {
        TextMeshProUGUI textComp = obj.GetComponent<TextMeshProUGUI>();
        textComp.text = txt;
    }
    private void SetText(GameObject obj, string txt, Color color) {
        TextMeshProUGUI textComp = obj.GetComponent<TextMeshProUGUI>();
        textComp.text = txt;
        textComp.color = color;
    }
    public void OpenLinks() {
        Application.OpenURL("https://github.com/ipodtouch0218/NSMB-MarioVsLuigi/blob/master/LINKS.md");
    }
    public void Quit() {
        if (quit)
            return;

        StartCoroutine(FinishQuitting());
    }
    IEnumerator FinishQuitting() {
        AudioClip clip = Enums.Sounds.UI_Quit.GetClip();
        sfx.PlayOneShot(clip);
        quit = true;

        yield return new WaitForSeconds(clip.length);
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void ChangeStarRequirement(int stars) {
        starsText.text = stars.ToString();
    }
    public void SetStarRequirement(TMP_InputField input) {
        if (!PhotonNetwork.IsMasterClient) 
            return;

        int.TryParse(input.text, out int newValue);
        if (newValue < 1) {
            newValue = 5;
            input.text = newValue.ToString();
        }
        if (newValue == (int) PhotonNetwork.CurrentRoom.CustomProperties[Enums.NetRoomProperties.StarRequirement]) 
            return;

        ExitGames.Client.Photon.Hashtable table = new() {
            [Enums.NetRoomProperties.StarRequirement] = newValue
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(table);
        //ChangeStarRequirement(newValue);
    }

    public void CopyRoomCode() {
        TextEditor te = new();
        te.text = PhotonNetwork.CurrentRoom.Name;
        te.SelectAll();
        te.Copy();
    }

    public void ChangePrivate() {
        privateToggleRoom.SetIsOnWithoutNotify(!PhotonNetwork.CurrentRoom.IsVisible);
    }
    public void SetPrivate(Toggle toggle) {
        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonNetwork.CurrentRoom.IsVisible = !toggle.isOn;
        PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.ChangePrivate, null, NetworkUtils.EventAll, SendOptions.SendReliable);
    }
    public void ChangeMaxPlayers(byte value) {
        changePlayersSlider.SetValueWithoutNotify(value);
        currentMaxPlayers.GetComponent<TextMeshProUGUI>().text = "" + value;
    }
    public void SetMaxPlayers(Slider slider) {
        if (!PhotonNetwork.InRoom) {
            sliderText.GetComponent<TMP_Text>().text = slider.value.ToString();
            return;
        }
        if (!PhotonNetwork.IsMasterClient)
            return;

        byte players = PhotonNetwork.CurrentRoom.PlayerCount;
        if (slider.value < players)
            slider.SetValueWithoutNotify(players);

        if (slider.value == PhotonNetwork.CurrentRoom.MaxPlayers)
            return;

        PhotonNetwork.CurrentRoom.MaxPlayers = (byte) slider.value;
        PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.ChangeMaxPlayers, (byte) slider.value, NetworkUtils.EventAll, SendOptions.SendReliable);
    }


    public void ChangeTime(int time) {
        timeEnabled.SetIsOnWithoutNotify(time != -1);
        UpdateSettingEnableStates();
        if (time == -1)
            return;

        int minutes = time / 60;
        int seconds = time % 60;

        timeField.SetTextWithoutNotify($"{minutes}:{seconds:D2}");
    }
    public void SetTime(TMP_InputField input) {
        if (!PhotonNetwork.IsMasterClient)
            return;

        int seconds = ParseTimeToSeconds(input.text);

        if (seconds == -1)
            return;

        if (seconds < 1)
            seconds = 300;

        ChangeTime(seconds);

        if (seconds == (int) PhotonNetwork.CurrentRoom.CustomProperties[Enums.NetRoomProperties.Time])
            return;

        ExitGames.Client.Photon.Hashtable table = new()
        {
            [Enums.NetRoomProperties.Time] = seconds
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(table);
    }
    public void EnableTime(Toggle toggle) {
        ExitGames.Client.Photon.Hashtable properties = new()
        {
            [Enums.NetRoomProperties.Time] = toggle.isOn ? ParseTimeToSeconds(timeField.text) : -1
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }

    public int ParseTimeToSeconds(string time) {

        int minutes;
        int seconds;

        if (time.Contains(":")) {
            string[] split = time.Split(":");
            int.TryParse(split[0], out minutes);
            int.TryParse(split[1], out seconds);
        } else {
            minutes = 0;
            int.TryParse(time, out seconds);
        }

        if (seconds >= 60) {
            minutes += seconds / 60;
            seconds %= 60;
        }

        seconds = minutes * 60 + seconds;

        return seconds;
    }
    public void ChangeLobbyHeader(string name) {
        SetText(lobbyText, $"{name}'s Lobby");
    }
}
