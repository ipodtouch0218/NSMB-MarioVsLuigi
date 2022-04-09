using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;

public class MainMenuManager : MonoBehaviour, ILobbyCallbacks, IInRoomCallbacks, IOnEventCallback, IConnectionCallbacks, IMatchmakingCallbacks {
    public static MainMenuManager Instance; 
    public AudioSource musicSourceLoop, musicSourceIntro, sfx;
    public GameObject lobbiesContent, lobbyPrefab;
    public AudioClip buhBye, musicStart, musicLoop; 
    bool quit, validName;
    public GameObject connecting;
    public GameObject title, bg, mainMenu, optionsMenu, lobbyMenu, createLobbyPrompt, inLobbyMenu, creditsMenu, controlsMenu;
    public GameObject[] levelCameraPositions;
    public GameObject sliderText, lobbyText;
    public TMP_Dropdown levelDropdown, characterDropdown, primaryColorDropdown, secondaryColorDropdown;
    public RoomIcon selectedRoom;
    public Button joinRoomBtn, createRoomBtn, startGameBtn;
    public Toggle ndsResolutionToggle, fullscreenToggle, livesEnabled, powerupsEnabled, timeEnabled, fireballToggle, vsyncToggle;
    public GameObject playersContent, playersPrefab, chatContent, chatPrefab;
    public TMP_InputField nicknameField, starsText, livesField, timeField;
    public Slider musicSlider, sfxSlider, masterSlider, lobbyPlayersSlider;
    public GameObject mainMenuSelected, optionsSelected, lobbySelected, currentLobbySelected, createLobbySelected, creditsSelected, controlsSelected;
    public GameObject errorBox, errorButton, rebindPrompt;
    public TMP_Text errorText, rebindCountdown, rebindText;
    public TMP_Dropdown region;
    public RebindManager rebindManager;
    public static string lastRegion;

    public Selectable[] roomSettings;

    private Coroutine updatePingCoroutine;

    private bool pingsReceived;
    private List<string> formattedRegions;
    private Region[] pingSortedRegions;

    // LOBBY CALLBACKS
    public void OnJoinedLobby() {
        ExitGames.Client.Photon.Hashtable prop = new() {
            { Enums.NetPlayerProperties.Character, 0 },
            { Enums.NetPlayerProperties.Ping, PhotonNetwork.GetPing() },
            { Enums.NetPlayerProperties.PrimaryColor, CustomColors.Primary.Keys.ElementAt(0) }, 
            { Enums.NetPlayerProperties.SecondaryColor, CustomColors.Secondary.Keys.ElementAt(0) },
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(prop);

        if (updatePingCoroutine == null)
            updatePingCoroutine = StartCoroutine(UpdatePing());
    }
    public void OnLeftLobby() {}
    public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbies) {}
    public void OnRoomListUpdate(List<RoomInfo> roomList) {
        //clear existing
        for (int i = 0; i < lobbiesContent.transform.childCount; i++) {
            GameObject roomObj = lobbiesContent.transform.GetChild(i).gameObject;
            if (!roomObj.activeSelf) 
                continue;

            Destroy(roomObj);
        }
        //add new rooms
        //TODO refactor??
        int count = 0;
        foreach (RoomInfo room in roomList) {
            if (!room.IsVisible || room.MaxPlayers <= 0)
                continue;

            GameObject newLobby = Instantiate(lobbyPrefab, Vector3.zero, Quaternion.identity, lobbiesContent.transform);
            newLobby.SetActive(true);
            RectTransform rect = newLobby.GetComponent<RectTransform>();
            rect.offsetMin = new Vector2(0, (count-1) * 55f);
            rect.offsetMax = new Vector2(0, count * 55f);
            SetText(newLobby.transform.Find("LobbyName").gameObject, "Name: " + room.Name);
            SetText(newLobby.transform.Find("LobbyPlayers").gameObject, "Players: " + room.PlayerCount + "/" + room.MaxPlayers);
            newLobby.GetComponent<RoomIcon>().room = room;
            count--;
        }
    }

    // ROOM CALLBACKS
    public void OnPlayerPropertiesUpdate(Player player, ExitGames.Client.Photon.Hashtable playerProperties) {
        UpdatePlayerList(player);
    }
    public void OnMasterClientSwitched(Player newMaster) {
        LocalChatMessage(newMaster.NickName + " has become the Host", ColorToVector(Color.red));
    }
    public void OnJoinedRoom() {
        LocalChatMessage(PhotonNetwork.LocalPlayer.NickName + " joined the room", ColorToVector(Color.red));
        EnterRoom();
    }
    public void OnPlayerEnteredRoom(Player newPlayer) {
        LocalChatMessage(newPlayer.NickName + " joined the room", ColorToVector(Color.red));
        PopulatePlayerList();
    }
    public void OnPlayerLeftRoom(Player otherPlayer) {
        LocalChatMessage(otherPlayer.NickName + " left the room", ColorToVector(Color.red));
        PopulatePlayerList();
    }
    public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable properties) {
        if (properties == null)
            return;

        if (properties[Enums.NetRoomProperties.Level] != null)
            ChangeLevel((int) properties[Enums.NetRoomProperties.Level]);
        if (properties[Enums.NetRoomProperties.StarRequirement] != null)
            ChangeStarRequirement((int) properties[Enums.NetRoomProperties.StarRequirement]);
        if (properties[Enums.NetRoomProperties.Lives] != null)
            ChangeLives((int) properties[Enums.NetRoomProperties.Lives]);
        if (properties[Enums.NetRoomProperties.NewPowerups] != null)
            ChangeNewPowerups((bool) properties[Enums.NetRoomProperties.NewPowerups]);
        if (properties[Enums.NetRoomProperties.Time] != null)
            ChangeTime((int) properties[Enums.NetRoomProperties.Time]);
    }
    // CONNECTION CALLBACKS
    public void OnConnected() { }
    public void OnDisconnected(DisconnectCause cause) {
        Debug.Log("disconnected: " + cause.ToString());
        if (!(cause == DisconnectCause.None || cause == DisconnectCause.DisconnectByClientLogic))
            OpenErrorBox("Disconnected: " + cause.ToString());

        if (!PhotonNetwork.IsConnectedAndReady)
            PhotonNetwork.ConnectToRegion(lastRegion);
    }
    public void OnRegionListReceived(RegionHandler handler) {
        handler.PingMinimumOfRegions((handler) => {

            formattedRegions = new();
            pingSortedRegions = handler.EnabledRegions.ToArray();
            System.Array.Sort(pingSortedRegions, new RegionComparer());

            foreach (Region r in pingSortedRegions)
                formattedRegions.Add($"{r.Code} <color=#cccccc>({(r.Ping == 4000 ? "N/A" : r.Ping + "ms")})");

            lastRegion = pingSortedRegions[0].Code;
            pingsReceived = true;
        }, "");
    }
    public void OnCustomAuthenticationResponse(Dictionary<string, object> response) {}
    public void OnCustomAuthenticationFailed(string failure) {}
    public void OnConnectedToMaster() {
        Match match = Regex.Match(Application.version, "^\\w*\\.\\w*\\.\\w*");
        PhotonNetwork.JoinLobby(new TypedLobby(match.Groups[0].Value, LobbyType.Default));
    }
    // MATCHMAKING CALLBACKS
    public void OnFriendListUpdate(List<FriendInfo> friendList) {}
    public void OnLeftRoom() {
        OpenLobbyMenu();
        ClearChat();
    }
    public void OnJoinRandomFailed(short reasonId, string reasonMessage) {
        OnJoinRoomFailed(reasonId, reasonMessage);
    }
    public void OnJoinRoomFailed(short reasonId, string reasonMessage) {
        Debug.LogError("join room failed, " + reasonId + ": " + reasonMessage);
        OpenErrorBox(reasonMessage);
    }
    public void OnCreateRoomFailed(short reasonId, string reasonMessage) {
        Debug.LogError("create room failed, " + reasonId + ": " + reasonMessage);
        OpenErrorBox(reasonMessage);

        OnConnectedToMaster();
    }
    public void OnCreatedRoom() {
        Debug.Log("Created Room: " + PhotonNetwork.CurrentRoom.Name);

        ExitGames.Client.Photon.Hashtable table = new() {
            [Enums.NetRoomProperties.Level] = 0,
            [Enums.NetRoomProperties.StarRequirement] = 10,
            [Enums.NetRoomProperties.Lives] = -1,
            [Enums.NetRoomProperties.Time] = -1,
            [Enums.NetRoomProperties.NewPowerups] = true,
            [Enums.NetRoomProperties.GameStarted] = false
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(table);
    }
    // CUSTOM EVENT CALLBACKS
    public void OnEvent(EventData e) {
        switch (e.Code) {
        case (byte) Enums.NetEventIds.StartGame: {
            PhotonNetwork.IsMessageQueueRunning = false;
            SceneManager.LoadSceneAsync(1, LoadSceneMode.Single);
            SceneManager.LoadSceneAsync(levelDropdown.value + 2, LoadSceneMode.Additive);
            break;
        }
        case (byte) Enums.NetEventIds.ChatMessage: {
            object[] data = (object[]) e.CustomData;
            string message = (string) data[0];
            Vector3 color = (Vector3) data[1];
            LocalChatMessage(message, color);
            break;
        }
        }
    }

    // CALLBACK REGISTERING
    void OnEnable() {
        PhotonNetwork.AddCallbackTarget(this);
    }
    void OnDisable() {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // Unity Stuff
    void Start() {
        Instance = this;
        HorizontalCamera.OFFSET_TARGET = 0;
        HorizontalCamera.OFFSET = 0;
        GlobalController.Instance.joinedAsSpectator = false;

        PhotonNetwork.SerializationRate = 30;
        PhotonNetwork.MaxResendsBeforeDisconnect = 15;

        AudioMixer mixer = musicSourceLoop.outputAudioMixerGroup.audioMixer;
        mixer.SetFloat("MusicSpeed", 1f);
        mixer.SetFloat("MusicPitch", 1f);

        if (PhotonNetwork.InRoom)
            EnterRoom();

        PlaySong(musicLoop, musicStart);

        lobbyPrefab = lobbiesContent.transform.Find("Template").gameObject; 

        PhotonNetwork.NickName = PlayerPrefs.GetString("Nickname", "Player" + Random.Range(1000,10000));
        Camera.main.transform.position = levelCameraPositions[Random.Range(0,levelCameraPositions.Length)].transform.position;
        
        nicknameField.text = PhotonNetwork.NickName;
        musicSlider.value = Settings.Instance.VolumeMusic;
        sfxSlider.value = Settings.Instance.VolumeSFX;
        masterSlider.value = Settings.Instance.VolumeMaster;

        ndsResolutionToggle.isOn = Settings.Instance.ndsResolution;
        fullscreenToggle.isOn = Screen.fullScreenMode == FullScreenMode.FullScreenWindow;
        fireballToggle.isOn = Settings.Instance.fireballFromSprint;
        vsyncToggle.isOn = Settings.Instance.vsync;

        //custom color dropdown initialization
        primaryColorDropdown.AddOptions( new List<string>(CustomColors.Primary.Keys) );
        secondaryColorDropdown.AddOptions( new List<string>(CustomColors.Secondary.Keys) );

        QualitySettings.vSyncCount = Settings.Instance.vsync ? 1 : 0;

        rebindManager.Init();

        if (!PhotonNetwork.IsConnected) {
            PhotonNetwork.NetworkingClient.AppId = "ce540834-2db9-40b5-a311-e58be39e726a";
            PhotonNetwork.NetworkingClient.ConnectToNameServer();
        } else {
            List<string> newRegions = new();
            pingSortedRegions = PhotonNetwork.NetworkingClient.RegionHandler.EnabledRegions.ToArray();
            System.Array.Sort(pingSortedRegions, new RegionComparer());

            int index = 0;
            bool found = false;
            foreach (Region r in pingSortedRegions) {
                newRegions.Add($"{r.Code} <color=#cccccc>({(r.Ping == 4000 ? "N/A" : r.Ping + "ms")})");
                found &= r.Code == lastRegion || r.Code == PhotonNetwork.CloudRegion;
                if (!found)
                    index++;
            }

            region.ClearOptions();
            region.AddOptions(newRegions);
            
            if (found)
                region.value = index;
        }
        EventSystem.current.SetSelectedGameObject(title);
    }

    void Update() {
        bool connected = PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby;
        connecting.SetActive(!connected);

        joinRoomBtn.interactable = connected && selectedRoom != null && validName;
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

    public class RegionComparer : IComparer<Region> {
        public int Compare(Region r1, Region r2) {
            return r1.Ping - r2.Ping;
        }
    }


    private Coroutine loopCoroutine;
    private void PlaySong(AudioClip loop, AudioClip intro = null) {
        if (loopCoroutine != null) {
            StopCoroutine(loopCoroutine);
            loopCoroutine = null;
        }

        musicSourceLoop.Stop();
        musicSourceIntro.Stop();

        musicSourceLoop.clip = loop;
        musicSourceLoop.loop = true;
        if (intro) {
            musicSourceIntro.clip = intro;
            musicSourceIntro.Play();
            StartCoroutine(LoopMusic(musicSourceIntro, musicSourceLoop));
        } else {
            musicSourceLoop.Play();
        }
    }
    IEnumerator LoopMusic(AudioSource intro, AudioSource loop) {
        yield return new WaitUntil(() => intro.isPlaying);
        loop.PlayDelayed(intro.clip.length - intro.time);
        loopCoroutine = null;
    }

    IEnumerator UpdatePing() {
        // push our ping into our player properties every N seconds. 2 seems good.
        while (true) {
            yield return new WaitForSeconds(2);
            if (PhotonNetwork.InRoom) {
                ExitGames.Client.Photon.Hashtable prop = new() {
                    { Enums.NetPlayerProperties.Ping, PhotonNetwork.GetPing() }
                };
                PhotonNetwork.LocalPlayer.SetCustomProperties(prop);
            }
        }
    }

    void EnterRoom() {
        Room room = PhotonNetwork.CurrentRoom;

        object started = room.CustomProperties[Enums.NetRoomProperties.GameStarted];
        if (started != null && (bool) started) {
            //start as spectator
            GlobalController.Instance.joinedAsSpectator = true;
            PhotonNetwork.IsMessageQueueRunning = false;
            SceneManager.LoadSceneAsync(1, LoadSceneMode.Single);
            SceneManager.LoadSceneAsync(levelDropdown.value + 2, LoadSceneMode.Additive);
            return;
        }
        
        OpenInLobbyMenu(room);
        PopulatePlayerList();
        characterDropdown.SetValueWithoutNotify(Utils.GetCharacterIndex());

        OnRoomPropertiesUpdate(room.CustomProperties);

        if (updatePingCoroutine == null)
            updatePingCoroutine = StartCoroutine(UpdatePing());

        if (PhotonNetwork.IsMasterClient) {
            room.IsVisible = true;
            room.IsOpen = true;
        }
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

        EventSystem.current.SetSelectedGameObject(creditsSelected);
    }
    public void OpenInLobbyMenu(RoomInfo room) {
        title.SetActive(false);
        bg.SetActive(true);
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyMenu.SetActive(true);
        creditsMenu.SetActive(false);

        lobbyText.GetComponent<TextMeshProUGUI>().text = room.Name;
        EventSystem.current.SetSelectedGameObject(currentLobbySelected);
    }
    public void OpenErrorBox(string text) {
        errorBox.SetActive(true);
        errorText.text = text;
        EventSystem.current.SetSelectedGameObject(errorButton);
    }

    public void ConnectToDropdownRegion() {
        Region targetRegion = pingSortedRegions[region.value];
        if (PhotonNetwork.CloudRegion == targetRegion.Code)
            return;

        for (int i = 0; i < lobbiesContent.transform.childCount; i++) {
            GameObject roomObj = lobbiesContent.transform.GetChild(i).gameObject;
            if (!roomObj.activeSelf)
                continue;

            Destroy(roomObj);
        }
        selectedRoom = null;

        PhotonNetwork.Disconnect();
    }

    public void QuitRoom() {
        PhotonNetwork.LeaveRoom();
    }
    public void StartGame() {
        PhotonNetwork.CurrentRoom.IsOpen = true;
        PhotonNetwork.CurrentRoom.IsVisible = true;

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
        Debug.Log($"lives changed to {lives}");
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
        levelDropdown.value = index;
        levelDropdown.RefreshShownValue();
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
        if (selectedRoom)
            selectedRoom.Unselect();

        selectedRoom = room.GetComponent<RoomIcon>();
        selectedRoom.Select();
        joinRoomBtn.interactable = room != null && PhotonNetwork.NickName.Length >= 3;
    }
    public void JoinSelectedRoom() {
        if (selectedRoom == null)
            return;

        PhotonNetwork.JoinRoom(selectedRoom.room.Name);
    }
    public void CreateRoom() {
        bool endswithS = nicknameField.text.EndsWith("s");
        string room = nicknameField.text + "'" + (endswithS ? "" : "s") + " Lobby";

        byte players = (byte) lobbyPlayersSlider.value;
        if (players < 2)
            return;

        PhotonNetwork.CreateRoom(room, new() { MaxPlayers = players, IsVisible = true, PublishUserId = true }, TypedLobby.Default);
        createLobbyPrompt.SetActive(false);
    }
    public void SetMaxPlayersText(Slider slider) {
        sliderText.GetComponent<TextMeshProUGUI>().text = "" + slider.value;
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
    }
    public void PopulatePlayerList() {
        for (int i = 0; i < playersContent.transform.childCount; i++) {
            GameObject pl = playersContent.transform.GetChild(i).gameObject;
            if (!pl.activeSelf) 
                continue;
            Destroy(pl);
        }

        int count = 0;
        SortedDictionary<int, Player> sortedPlayers = new(PhotonNetwork.CurrentRoom.Players);
        foreach (KeyValuePair<int, Player> player in sortedPlayers) {
            Player pl = player.Value;

            GameObject newPl = Instantiate(playersPrefab, Vector3.zero, Quaternion.identity);
            newPl.name = pl.UserId;
            newPl.transform.SetParent(playersContent.transform);
            newPl.transform.localPosition = new Vector3(0, -(count-- * 40f), 0);
            newPl.transform.localScale = Vector3.one;
            newPl.SetActive(true);
            RectTransform tf = newPl.GetComponent<RectTransform>();
            tf.offsetMax = new Vector2(330, tf.offsetMax.y);
            UpdatePlayerList(pl, newPl.transform);
        }

        UpdateSettingEnableStates();
    }
    public void UpdatePlayerList(Player pl, Transform nameObject = null) {
        string characterString = Utils.GetCharacterData(pl).uistring;
        pl.CustomProperties.TryGetValue(Enums.NetPlayerProperties.Ping, out object ping);
        if (ping == null) 
            ping = -1;
        string pingColor;
        if ((int) ping < 0) {
            pingColor = "black";    
        } else if ((int) ping < 80) {
            pingColor = "#00b900";
        } else if ((int) ping < 120) {
            pingColor = "orange";
        } else {
            pingColor = "red";
        }

        if (nameObject == null) 
            nameObject = playersContent.transform.Find(pl.UserId);
        if (nameObject == null) 
            return;
        SetText(nameObject.Find("NameText").gameObject, (pl.IsMasterClient ? "<sprite=5>" : "") + characterString + pl.NickName);
        SetText(nameObject.Find("PingText").gameObject, "<color=" + pingColor + ">" + (int) ping);
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
    public void SendChat(TMP_InputField input) {
        string text = input.text.Trim();
        if (input.text == null || input.text == "")
            return;
        
        GlobalChatMessage(PhotonNetwork.NickName + ": " + text, ColorToVector(Color.black));
        input.text = "";

        StartCoroutine(SelectNextFrame(input));
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
        
        sfx.PlayOneShot((AudioClip) Resources.Load("Sound/" + data.soundFolder + "/selected"));
    }

    public void SwapPrimaryColor(TMP_Dropdown dropdown){
        string colorName = dropdown.captionText.text;
        ExitGames.Client.Photon.Hashtable prop = new() {
            { Enums.NetPlayerProperties.PrimaryColor, colorName }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(prop);

        dropdown.captionText.color = dropdown.value == 0
            ? new Color(1f,1f,1f,1f)
            : CustomColors.Primary[colorName];
    }

    public void SwapSecondaryColor(TMP_Dropdown dropdown){
        string colorName = dropdown.captionText.text;
        ExitGames.Client.Photon.Hashtable prop = new() {
            { Enums.NetPlayerProperties.SecondaryColor, colorName }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(prop);

        dropdown.captionText.color = dropdown.value == 0
            ? new Color(1f,1f,1f,1f)
            : CustomColors.Secondary[colorName];
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
        sfx.PlayOneShot(buhBye);
        quit = true;
        yield return new WaitForSeconds(buhBye.length);
        Application.Quit();
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

    public void ChangeTime(int time) {
        Debug.Log($"time changed to {time}s");
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
}
