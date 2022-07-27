﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using TMPro;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using NSMB.Utils;

public class MainMenuManager : MonoBehaviour, ILobbyCallbacks, IInRoomCallbacks, IOnEventCallback, IConnectionCallbacks, IMatchmakingCallbacks {
    public static MainMenuManager Instance;
    public AudioSource sfx, music;
    public GameObject lobbiesContent, lobbyPrefab;
    bool quit, validName;
    public GameObject connecting;
    public GameObject title, bg, mainMenu, optionsMenu, lobbyMenu, createLobbyPrompt, inLobbyMenu, creditsMenu, controlsMenu, privatePrompt, updateBox;
    public GameObject[] levelCameraPositions;
    public GameObject sliderText, lobbyText, currentMaxPlayers, settingsPanel;
    public TMP_Dropdown levelDropdown, characterDropdown;
    public RoomIcon selectedRoomIcon, privateJoinRoom;
    public Button joinRoomBtn, createRoomBtn, startGameBtn;
    public Toggle ndsResolutionToggle, fullscreenToggle, livesEnabled, powerupsEnabled, timeEnabled, drawTimeupToggle, fireballToggle, vsyncToggle, privateToggle, privateToggleRoom, aspectToggle, spectateToggle;
    public GameObject playersContent, playersPrefab, chatContent, chatPrefab;
    public TMP_InputField nicknameField, starsText, coinsText, livesField, timeField, lobbyJoinField, chatTextField;
    public Slider musicSlider, sfxSlider, masterSlider, lobbyPlayersSlider, changePlayersSlider;
    public GameObject mainMenuSelected, optionsSelected, lobbySelected, currentLobbySelected, createLobbySelected, creditsSelected, controlsSelected, privateSelected, reconnectSelected, updateBoxSelected;
    public GameObject errorBox, errorButton, rebindPrompt, reconnectBox;
    public TMP_Text errorText, rebindCountdown, rebindText, reconnectText, updateText;
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

    public List<string> maps, debugMaps;

    private bool pingsReceived, joinedLate;
    private List<string> formattedRegions;
    private Region[] pingSortedRegions;

    private readonly Dictionary<string, RoomIcon> currentRooms = new();

    private static readonly string roomNameChars = "BCDFGHJKLMNPRQSTVWXYZ";

    private readonly Dictionary<Player, double> lastMessage = new();

    Coroutine updatePingCoroutine;

    // LOBBY CALLBACKS
    public void OnJoinedLobby() {
        Hashtable prop = new() {
            { Enums.NetPlayerProperties.Character, 0 },
            { Enums.NetPlayerProperties.Ping, PhotonNetwork.GetPing() },
            { Enums.NetPlayerProperties.PlayerColor, 0 },
            { Enums.NetPlayerProperties.Spectator, false },
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
            if (!room.IsVisible || room.RemovedFromList || room.MaxPlayers > 10 || room.MaxPlayers == 0) {
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
    public void OnPlayerPropertiesUpdate(Player player, Hashtable playerProperties) {
        // increase or remove when toadette or another character is added
        Utils.GetCustomProperty(Enums.NetRoomProperties.Debug, out bool debug);
        if (PhotonNetwork.IsMasterClient && Utils.GetCharacterIndex(player) > 1 && !debug) {
            PhotonNetwork.CloseConnection(player);
        }
        UpdateSettingEnableStates();
    }

    public void OnMasterClientSwitched(Player newMaster) {
        LocalChatMessage(newMaster.NickName + " has become the Host", ColorToVector(Color.red));

        if (newMaster.IsLocal) {
            //i am de captain now
            PhotonNetwork.CurrentRoom.SetCustomProperties(new() {
                [Enums.NetRoomProperties.HostName] = newMaster.NickName
            });
            LocalChatMessage("You are the room's host! You can use chat commands like /ban, /mute, /kick, etc. to control your room. Do /help for help.", ColorToVector(Color.red));
        }
        UpdateSettingEnableStates();
    }
    public void OnJoinedRoom() {
        LocalChatMessage(PhotonNetwork.LocalPlayer.NickName + " joined the room", ColorToVector(Color.red));

        Room room = PhotonNetwork.CurrentRoom;
        Player local = PhotonNetwork.LocalPlayer;
        if (room.Players.Values.Where(pl => pl != local).Any(pl => pl.NickName.Filter().ToLower() == local.NickName.Filter().ToLower())) {
            //rename
            int dupes = room.Players.Values.Where(pl => pl != local).Where(pl => Regex.Match(pl.NickName.Filter(), local.NickName.Filter() + "\\(\\d\\)").Success).Count();
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
    IEnumerator KickPlayer(Player player) {
        while (PhotonNetwork.CurrentRoom.Players.Values.Contains(player)) {
            PhotonNetwork.CloseConnection(player);
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
    public void OnPlayerEnteredRoom(Player newPlayer) {
        Utils.GetCustomProperty(Enums.NetRoomProperties.Bans, out object[] bans);
        List<NameIdPair> banList = bans.Cast<NameIdPair>().ToList();
        if (banList.Any(nip => nip.userId == newPlayer.UserId)) {

            if (PhotonNetwork.IsMasterClient)
                StartCoroutine(KickPlayer(newPlayer));
            return;
        }
        LocalChatMessage(newPlayer.NickName + " joined the room", ColorToVector(Color.red));
        sfx.PlayOneShot(Enums.Sounds.UI_PlayerConnect.GetClip());
    }
    public void OnPlayerLeftRoom(Player otherPlayer) {
        Utils.GetCustomProperty(Enums.NetRoomProperties.Bans, out object[] bans);
        List<NameIdPair> banList = bans.Cast<NameIdPair>().ToList();
        if (banList.Any(nip => nip.userId == otherPlayer.UserId)) {
            return;
        }
        LocalChatMessage(otherPlayer.NickName + " left the room", ColorToVector(Color.red));
        sfx.PlayOneShot(Enums.Sounds.UI_PlayerDisconnect.GetClip());
    }
    public void OnRoomPropertiesUpdate(Hashtable updatedProperties) {
        if (updatedProperties == null)
            return;

        AttemptToUpdateProperty<bool>(updatedProperties, Enums.NetRoomProperties.Debug, ChangeDebugState);
        AttemptToUpdateProperty<int>(updatedProperties, Enums.NetRoomProperties.Level, ChangeLevel);
        AttemptToUpdateProperty<int>(updatedProperties, Enums.NetRoomProperties.StarRequirement, ChangeStarRequirement);
        AttemptToUpdateProperty<int>(updatedProperties, Enums.NetRoomProperties.CoinRequirement, ChangeCoinRequirement);
        AttemptToUpdateProperty<int>(updatedProperties, Enums.NetRoomProperties.Lives, ChangeLives);
        AttemptToUpdateProperty<bool>(updatedProperties, Enums.NetRoomProperties.NewPowerups, ChangeNewPowerups);
        AttemptToUpdateProperty<int>(updatedProperties, Enums.NetRoomProperties.Time, ChangeTime);
        AttemptToUpdateProperty<bool>(updatedProperties, Enums.NetRoomProperties.DrawTime, ChangeDrawTime);
        AttemptToUpdateProperty<string>(updatedProperties, Enums.NetRoomProperties.HostName, ChangeLobbyHeader);
    }

    public void ChangeDebugState(bool enabled) {
        int index = levelDropdown.value;
        levelDropdown.SetValueWithoutNotify(0);
        levelDropdown.ClearOptions();
        levelDropdown.AddOptions(maps);
        levelDropdown.SetValueWithoutNotify(Mathf.Clamp(index, 0, maps.Count - 1));

        if (enabled) {
            levelDropdown.AddOptions(debugMaps);
        } else if (PhotonNetwork.IsMasterClient) {
            Utils.GetCustomProperty(Enums.NetRoomProperties.Level, out int level);
            if (level >= maps.Count) {
                Hashtable props = new() {
                    [Enums.NetRoomProperties.Level] = maps.Count - 1,
                };

                PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            }
        }
        UpdateSettingEnableStates();
    }

    private void AttemptToUpdateProperty<T>(Hashtable updatedProperties, string key, System.Action<T> updateAction) {
        if (updatedProperties[key] == null)
            return;

        updateAction((T) updatedProperties[key]);
    }
    // CONNECTION CALLBACKS
    public void OnConnected() {
        Debug.Log("[PHOTON] Connected to Photon.");
        PlayerPrefs.SetString("id", PhotonNetwork.AuthValues.UserId);
    }
    public void OnDisconnected(DisconnectCause cause) {
        Debug.Log("[PHOTON] Disconnected: " + cause.ToString());
        if (!(cause == DisconnectCause.None || cause == DisconnectCause.DisconnectByClientLogic))
            OpenErrorBox("Disconnected: " + cause.ToString());

        selectedRoom = null;
        selectedRoomIcon = null;
        if (!PhotonNetwork.IsConnectedAndReady) {

            foreach ((string key, RoomIcon value) in currentRooms.ToArray()) {
                Destroy(value);
                currentRooms.Remove(key);
            }

            PhotonNetwork.ConnectToRegion(lastRegion);
        }
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
    public void OnCustomAuthenticationResponse(Dictionary<string, object> response) { }
    public void OnCustomAuthenticationFailed(string failure) { }
    public void OnConnectedToMaster() {
        JoinMainLobby();

        /*
        string reconnectRoom = PlayerPrefs.GetString("in-room", null);
        if (reconnectRoom != null) {
            PhotonNetwork.RejoinRoom(reconnectRoom);
        }
        */
    }
    // MATCHMAKING CALLBACKS
    public void OnFriendListUpdate(List<FriendInfo> friendList) {}
    public void OnLeftRoom() {
        OpenLobbyMenu();
        ClearChat();
        GlobalController.Instance.DiscordController.UpdateActivity();
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
            PlayerPrefs.SetString("in-room", PhotonNetwork.CurrentRoom.Name);
            PlayerPrefs.Save();
            Utils.GetCustomProperty(Enums.NetPlayerProperties.Spectator, out bool spectate, PhotonNetwork.LocalPlayer.CustomProperties);
            GlobalController.Instance.joinedAsSpectator = spectate || joinedLate;
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

            var players = PhotonNetwork.CurrentRoom.Players;
            if (players.ContainsKey(e.Sender)) {

                Player pl = players[e.Sender];

                double time = lastMessage.GetValueOrDefault(pl);
                if (PhotonNetwork.Time - time < 1f)
                    return;

                lastMessage[pl] = PhotonNetwork.Time;

                if (!pl.IsMasterClient) {
                    Utils.GetCustomProperty(Enums.NetRoomProperties.Mutes, out object[] mutes);
                    if (mutes.Contains(pl.UserId))
                        return;
                }
            }

            LocalChatMessage(message.Replace("<", "«").Replace(">", "»").Substring(0, Mathf.Min(128, message.Length)), color);
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
        //Match match = Regex.Match(Application.version, "^\\w*\\.\\w*\\.\\w*");
        //PhotonNetwork.JoinLobby(new TypedLobby(match.Groups[0].Value, LobbyType.Default));

        PhotonNetwork.JoinLobby();
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
        AudioMixer mixer = music.outputAudioMixerGroup.audioMixer;
        mixer.SetFloat("MusicSpeed", 1f);
        mixer.SetFloat("MusicPitch", 1f);

        if (GlobalController.Instance.disconnectCause != null) {
            OpenErrorBox("Disconnected: " + GlobalController.Instance.disconnectCause.ToString());
            GlobalController.Instance.disconnectCause = null;
        }

        Camera.main.transform.position = levelCameraPositions[Random.Range(0, maps.Count)].transform.position;
        levelDropdown.AddOptions(maps);
        LoadSettings(!PhotonNetwork.InRoom);

        //Photon stuff.
        if (!PhotonNetwork.IsConnected) {
            OpenTitleScreen();
            PhotonNetwork.NetworkingClient.AppId = "ce540834-2db9-40b5-a311-e58be39e726a";

            //version separation
            Match match = Regex.Match(Application.version, "^\\w*\\.\\w*\\.\\w*");
            PhotonNetwork.NetworkingClient.AppVersion = match.Groups[0].Value;

            string id = PlayerPrefs.GetString("id", null);
            if (id != null)
                PhotonNetwork.AuthValues = new() { UserId = id };

            PhotonNetwork.NetworkingClient.ConnectToNameServer();
        } else {
            if (PhotonNetwork.InRoom) {
                EnterRoom();
                nicknameField.SetTextWithoutNotify(Settings.Instance.nickname);
                UpdateNickname();

            } else {
                PhotonNetwork.Disconnect();
                nicknameField.text = Settings.Instance.nickname;
            }
        }

        if (PhotonNetwork.NetworkingClient.RegionHandler != null) {
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
        QualitySettings.vSyncCount = Settings.Instance.vsync ? 1 : 0;
    }

    void Update() {
        bool connected = PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby;
        connecting.SetActive(!connected && lobbyMenu.activeInHierarchy);
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
        PlayerPrefs.SetString("in-room", null);
        PlayerPrefs.Save();

        Utils.GetCustomProperty(Enums.NetRoomProperties.GameStarted, out bool started);
        if (started) {
            //start as spectator
            joinedLate = true;
            OnEvent(new() { Code = (byte) Enums.NetEventIds.StartGame });
            return;
        }

        OpenInLobbyMenu();
        characterDropdown.SetValueWithoutNotify(Utils.GetCharacterIndex());

        if (PhotonNetwork.IsMasterClient)
            LocalChatMessage("You are the room's host! You can use chat commands like /ban, /mute, /kick, etc. to control your room. Do /help for help.", ColorToVector(Color.red));

        Utils.GetCustomProperty(Enums.NetPlayerProperties.PlayerColor, out int value, PhotonNetwork.LocalPlayer.CustomProperties);
        SetPlayerColor(value);

        OnRoomPropertiesUpdate(room.CustomProperties);
        ChangeMaxPlayers(room.MaxPlayers);
        ChangePrivate();

        StartCoroutine(SetScroll());

        PhotonNetwork.LocalPlayer.SetCustomProperties(new() {
            [Enums.NetPlayerProperties.GameState] = null,
            [Enums.NetPlayerProperties.Status] = Debug.isDebugBuild || Application.isEditor,
        });
        if (updatePingCoroutine == null)
            updatePingCoroutine = StartCoroutine(UpdatePing());
        GlobalController.Instance.DiscordController.UpdateActivity();

        Utils.GetCustomProperty(Enums.NetPlayerProperties.Spectator, out bool spectating, PhotonNetwork.LocalPlayer.CustomProperties);
        spectateToggle.isOn = spectating;
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
            room.UpdateUI(room.room);

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
        ChangeLives(newValue);
        if (newValue == (int) PhotonNetwork.CurrentRoom.CustomProperties[Enums.NetRoomProperties.Lives])
            return;

        ExitGames.Client.Photon.Hashtable table = new() {
            [Enums.NetRoomProperties.Lives] = newValue
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(table);
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

        ChangeLevel(newLevelIndex);
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

        joinRoomBtn.interactable = room != null && nicknameField.text.Length >= 3;
    }
    public void JoinSelectedRoom() {
        if (selectedRoomIcon?.joinPrivate ?? false) {
            OpenPrivatePrompt();
            return;
        }
        if (selectedRoom == null)
            return;

        PhotonNetwork.NickName = nicknameField.text;
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
        PhotonNetwork.NickName = nicknameField.text;
        for (int i = 0; i < 8; i++)
            roomName += roomNameChars[Random.Range(0, roomNameChars.Length)];

        Hashtable properties = NetworkUtils.DefaultRoomProperties;
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
        drawTimeupToggle.interactable = PhotonNetwork.IsMasterClient && timeEnabled.isOn;

        Utils.GetCustomProperty(Enums.NetRoomProperties.Debug, out bool debug);
        privateToggleRoom.interactable = PhotonNetwork.IsMasterClient && !debug;

        int playingPlayers = PhotonNetwork.CurrentRoom.Players.Where(pl => {
            Utils.GetCustomProperty(Enums.NetPlayerProperties.Spectator, out bool spectating, pl.Value.CustomProperties);
            return !spectating;
        }).Count();

        startGameBtn.interactable = PhotonNetwork.IsMasterClient && playingPlayers >= 1;
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

        GameObject chat = Instantiate(chatPrefab, Vector3.zero, Quaternion.identity, chatContent.transform);
        chat.SetActive(true);

        GameObject txtObject = chat.transform.Find("Text").gameObject;
        SetText(txtObject, message, new Color(color.x, color.y, color.z));
        Canvas.ForceUpdateCanvases();

        //RectTransform tf = txtObject.GetComponent<RectTransform>();
        //Bounds bounds = txtObject.GetComponent<TextMeshProUGUI>().textBounds;
        //tf.sizeDelta = new Vector2(tf.sizeDelta.x, bounds.max.y - bounds.min.y - 15f);
    }
    public void SendChat() {
        string text = chatTextField.text.Replace("<", "«").Replace(">", "»").Trim();
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
                LocalChatMessage($"Unknown player {args[1]}", ColorToVector(Color.red));
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
                LocalChatMessage($"Unknown player {args[1]}", ColorToVector(Color.red));
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
                "kick" => "/kick <player name> - Kick a player from the room",
                "ban" => "/ban <player name> - Ban a player from rejoining the room",
                "host" => "/host <player name> - Make a player the host for the room",
                "mute" => "/mute <playername> - Prevents a player from talking in chat",
                "debug" => "/debug - Enables debug & in-development features",
                _ => "Available commands: /kick, /host, /debug, /mute, /ban",
            };
            LocalChatMessage(msg, ColorToVector(Color.red));
            return;
        }
        case "debug": {
            Utils.GetCustomProperty(Enums.NetRoomProperties.Debug, out bool debugEnabled);
            if (PhotonNetwork.CurrentRoom.IsVisible) {
                LocalChatMessage("Error: You can only enable debug / in development features in private lobbies.", ColorToVector(Color.red));
                return;
            }

            if (debugEnabled) {
                LocalChatMessage("Debug features have been disabled.", ColorToVector(Color.red));
            } else {
                LocalChatMessage("Debug features have been enabled.", ColorToVector(Color.red));
            }
            PhotonNetwork.CurrentRoom.SetCustomProperties(new() {
                [Enums.NetRoomProperties.Debug] = !debugEnabled
            });
            return;
        }
        case "mute": {
            if (args.Length < 2) {
                LocalChatMessage("Usage: /mute <player name>", ColorToVector(Color.red));
                return;
            }
            string strTarget = args[1].ToLower();
            Player target = PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault(pl => pl.NickName.ToLower() == strTarget);
            if (target == null) {
                LocalChatMessage($"Unknown player {args[1]}", ColorToVector(Color.red));
                return;
            }
            if (target.IsLocal) {
                LocalChatMessage("While you can mute yourself, it's probably not what you meant to do.", ColorToVector(Color.red));
                return;
            }
            Utils.GetCustomProperty(Enums.NetRoomProperties.Mutes, out object[] mutes);
            List<object> mutesList = new(mutes);
            if (mutes.Contains(target.UserId)) {
                LocalChatMessage($"Successfully unmuted {target.NickName}", ColorToVector(Color.red));
                mutesList.Remove(target.UserId);
            } else {
                LocalChatMessage($"Successfully muted {target.NickName}", ColorToVector(Color.red));
                mutesList.Add(target.UserId);
            }
            Hashtable table = new() {
                [Enums.NetRoomProperties.Mutes] = mutesList.ToArray(),
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(table);
            return;
        }
        case "ban": {
            if (args.Length < 2) {
                LocalChatMessage("Usage: /ban <player name>", ColorToVector(Color.red));
                return;
            }
            Utils.GetCustomProperty(Enums.NetRoomProperties.Bans, out object[] bans);
            List<NameIdPair> pairs = bans.Cast<NameIdPair>().ToList();

            string strTarget = args[1].ToLower();
            Player target = PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault(pl => pl.NickName.ToLower() == strTarget);

            string targetId = target?.UserId;
            if (targetId == null && (targetId = pairs.FirstOrDefault(nip => nip.name.ToLower() == strTarget)?.userId) == null) {
                LocalChatMessage($"Unknown player {args[1]}", ColorToVector(Color.red));
                return;
            }
            if (targetId == PhotonNetwork.LocalPlayer.UserId) {
                LocalChatMessage("While you can ban yourself, it's probably not what you meant to do.", ColorToVector(Color.red));
                return;
            }

            NameIdPair existingPair = pairs.FirstOrDefault(nid => nid.userId == targetId);
            if (existingPair != null) {
                LocalChatMessage($"Successfully unbanned {args[1]}", ColorToVector(Color.red));
                pairs.Remove(existingPair);
            } else {
                LocalChatMessage($"Successfully banned {args[1]}", ColorToVector(Color.red));
                pairs.Add(new NameIdPair() {
                    name = strTarget,
                    userId = targetId,
                });
                PhotonNetwork.CloseConnection(target);
            }
            Hashtable table = new() {
                [Enums.NetRoomProperties.Bans] = pairs.ToArray(),
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(table);
            return;
        }
        }
        LocalChatMessage($"Error: Unknown command. Try /help for help.", ColorToVector(Color.red));
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
        Hashtable prop = new() {
            { Enums.NetPlayerProperties.Character, dropdown.value }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(prop);

        PlayerData data = GlobalController.Instance.characters[dropdown.value];

        sfx.PlayOneShot(Enums.Sounds.Player_Voice_Selected.GetClip(data));
    }

    public void SetPlayerColor(int index) {
        Hashtable prop = new() {
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

    private void UpdateNickname() {
        validName = PhotonNetwork.NickName.Length > 2;
        if (!validName) {
            ColorBlock colors = nicknameField.colors;
            colors.normalColor = new Color(1, 0.7f, 0.7f, 1);
            colors.highlightedColor = new Color(1, 0.55f, 0.55f, 1);
            nicknameField.colors = colors;
        } else {
            ColorBlock colors = nicknameField.colors;
            colors.normalColor = Color.white;
            nicknameField.colors = colors;
        }
    }

    public void SetUsername(TMP_InputField field) {
        PhotonNetwork.NickName = field.text;
        UpdateNickname();

        Settings.Instance.nickname = field.text;
        Settings.Instance.SaveSettingsToPreferences();
    }
    private void SetText(GameObject obj, string txt) {
        TextMeshProUGUI textComp = obj.GetComponent<TextMeshProUGUI>();
        textComp.text = txt.Filter();
    }
    private void SetText(GameObject obj, string txt, Color color) {
        TextMeshProUGUI textComp = obj.GetComponent<TextMeshProUGUI>();
        textComp.text = txt.Filter();
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

        Hashtable table = new() {
            [Enums.NetRoomProperties.StarRequirement] = newValue
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(table);
        //ChangeStarRequirement(newValue);
    }

    public void ChangeCoinRequirement(int coins) {
        coinsText.text = coins.ToString();
    }
    public void SetCoinRequirement(TMP_InputField input) {
        if (!PhotonNetwork.IsMasterClient)
            return;

        int.TryParse(input.text, out int newValue);
        if (newValue < 1 || newValue > 99) {
            newValue = 8;
            input.text = newValue.ToString();
        }
        if (newValue == (int) PhotonNetwork.CurrentRoom.CustomProperties[Enums.NetRoomProperties.CoinRequirement])
            return;

        Hashtable table = new() {
            [Enums.NetRoomProperties.CoinRequirement] = newValue
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(table);
        //ChangeCoinRequirement(newValue);
    }

    public void CopyRoomCode() {
        TextEditor te = new();
        te.text = PhotonNetwork.CurrentRoom.Name;
        te.SelectAll();
        te.Copy();
    }

    public void OpenDownloadsPage() {
        Application.OpenURL("https://github.com/ipodtouch0218/NSMB-MarioVsLuigi/releases/latest");
        OpenMainMenu();
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

        Hashtable table = new()
        {
            [Enums.NetRoomProperties.Time] = seconds
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(table);
    }

    public void EnableSpectator(Toggle toggle) {
        Hashtable properties = new() {
            [Enums.NetPlayerProperties.Spectator] = toggle.isOn,
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
    }

    public void EnableTime(Toggle toggle) {
        Hashtable properties = new() {
            [Enums.NetRoomProperties.Time] = toggle.isOn ? ParseTimeToSeconds(timeField.text) : -1
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }
    public void ChangeDrawTime(bool value) {
        drawTimeupToggle.SetIsOnWithoutNotify(value);
    }
    public void SetDrawTime(Toggle toggle) {
        if (!PhotonNetwork.IsMasterClient)
            return;

        Hashtable properties = new() {
            [Enums.NetRoomProperties.DrawTime] = toggle.isOn
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
