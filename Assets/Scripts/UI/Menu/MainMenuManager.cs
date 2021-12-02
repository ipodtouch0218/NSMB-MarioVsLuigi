using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class MainMenuManager : MonoBehaviourPun {
    public static MainMenuManager Instance;
    Color defaultColor = Color.white;
    AudioSource music, sfx;
    public GameObject lobbiesContent, lobbyPrefab;
    public AudioClip buhBye, musicStart, musicLoop; 
    bool quit, validName;
    public GameObject connecting;
    public GameObject mainMenu, optionsMenu, lobbyMenu, createLobbyPrompt, inLobbyPrompt, creditsPage;
    public GameObject[] levelCameraPositions;
    public GameObject sliderText, lobbyText;
    public TMP_Dropdown levelDropdown;
    public RoomIcon selectedRoom;
    public Button joinRoomBtn, createRoomBtn, startGameBtn;
    public Toggle ndsResolutionToggle, fullscreenToggle;
    public GameObject playersContent, playersPrefab, chatContent, chatPrefab; 
    public TMP_InputField nicknameField, lobbyNameField;
    public Slider musicSlider, sfxSlider, masterSlider;
    public Slider starSlider;
    public TMP_Text starsText;
    public GameObject mainMenuSelected, optionsSelected, lobbySelected, currentLobbySelected, createLobbySelected, creditsSelected;
    private int prevWidth = 1280, prevHeight = 720;

    void Awake() {
        Instance = this;
    }
    void Start() {
        Debug.Log(PhotonNetwork.InRoom);
        if (PhotonNetwork.InRoom) {
            NetworkManager.instance.OnJoinedRoom();
        }

        music = GetComponents<AudioSource>()[0];
        sfx = GetComponents<AudioSource>()[1];
        music.clip = musicStart;
        music.Play();
        lobbyPrefab = lobbiesContent.transform.Find("Template").gameObject; 

        PhotonNetwork.NickName = PlayerPrefs.GetString("Nickname");
        Camera.main.transform.position = levelCameraPositions[Random.Range(0,levelCameraPositions.Length-1)].transform.position;
        
        nicknameField.text = PhotonNetwork.NickName;
        musicSlider.value = GlobalController.Instance.volumeMusic;
        sfxSlider.value = GlobalController.Instance.volumeSFX;
        masterSlider.value = GlobalController.Instance.volumeMaster;

        ndsResolutionToggle.isOn = PlayerPrefs.GetInt("NDSResolution", 0) == 1;
        fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 0) == 1;
        OnToggleUpdate();
    }

    void Update() {
        bool connected = PhotonNetwork.IsConnectedAndReady;
        connecting.SetActive(!connected);
        if (quit && !sfx.isPlaying) {
            Application.Quit();
        }
        if (!music.isPlaying) {
            music.clip = musicLoop;
            music.loop = true;
            music.Play();
        }

        joinRoomBtn.interactable = connected && selectedRoom != null && validName;
        createRoomBtn.interactable = connected && validName;
    }

    public void OpenMainMenu() {
        mainMenu.SetActive(true);
        optionsMenu.SetActive(false);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyPrompt.SetActive(false);
        creditsPage.SetActive(false);

        EventSystem.current.SetSelectedGameObject(mainMenuSelected);
    }
    public void OpenLobbyMenu() {
        if (!PhotonNetwork.IsConnected) {
            PhotonNetwork.ConnectUsingSettings();
        }
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        lobbyMenu.SetActive(true);
        createLobbyPrompt.SetActive(false);
        inLobbyPrompt.SetActive(false);
        creditsPage.SetActive(false);

        nicknameField.interactable = true;
        EventSystem.current.SetSelectedGameObject(lobbySelected);
    }
    public void OpenCreateLobby() {
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        lobbyMenu.SetActive(true);
        createLobbyPrompt.SetActive(true);
        inLobbyPrompt.SetActive(false);
        creditsPage.SetActive(false);

        nicknameField.interactable = false;
        bool endswithS = nicknameField.text.EndsWith("s");
        lobbyNameField.text = nicknameField.text + "'" + (endswithS ? "" : "s") + " Lobby";
        EventSystem.current.SetSelectedGameObject(createLobbySelected);
    }
    public void OpenOptions() {
        mainMenu.SetActive(false);
        optionsMenu.SetActive(true);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyPrompt.SetActive(false);
        creditsPage.SetActive(false);

        EventSystem.current.SetSelectedGameObject(optionsSelected);
    }
    public void OpenCredits() {
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyPrompt.SetActive(false);
        creditsPage.SetActive(true);

        EventSystem.current.SetSelectedGameObject(creditsSelected);
    }
    public void OpenInLobbyMenu(RoomInfo room) {
        mainMenu.SetActive(false);
        optionsMenu.SetActive(false);
        lobbyMenu.SetActive(false);
        createLobbyPrompt.SetActive(false);
        inLobbyPrompt.SetActive(true);
        creditsPage.SetActive(false);

        lobbyText.GetComponent<TextMeshProUGUI>().text = room.Name;
        if (room.CustomProperties[NetworkManager.PROPKEY_MAP] != null) {
            SetLevelIndex((int) room.CustomProperties[NetworkManager.PROPKEY_MAP]);
        }
        EventSystem.current.SetSelectedGameObject(currentLobbySelected);
    }
    public void QuitRoom() {
        PhotonNetwork.LeaveRoom();
        MainMenuManager.Instance.OpenLobbyMenu();
        MainMenuManager.Instance.ClearChat();
    }
    public void StartGame() {
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        photonView.RPC("StartLoading", RpcTarget.AllViaServer);
    }
    
    [PunRPC]
    public void StartLoading() {
        PhotonNetwork.IsMessageQueueRunning = false;
        SceneManager.LoadSceneAsync(1, LoadSceneMode.Single);
        SceneManager.LoadSceneAsync(levelDropdown.value + 2, LoadSceneMode.Additive);
    }
    public void SetLevelIndex() {
        Camera.main.transform.position = levelCameraPositions[levelDropdown.value].transform.position;
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient) {
            photonView.RPC("ChatMessage", RpcTarget.All, "Map set to: " + levelDropdown.captionText.text, ColorToVector(Color.red));
            ExitGames.Client.Photon.Hashtable table = new ExitGames.Client.Photon.Hashtable();
            table[NetworkManager.PROPKEY_MAP] = levelDropdown.value;
            PhotonNetwork.CurrentRoom.SetCustomProperties(table);
        }
    }
    public void SetLevelIndex(int index) {
        if (PhotonNetwork.IsMasterClient && levelDropdown.value == index) return;
        levelDropdown.value = index;
        levelDropdown.RefreshShownValue();
        SetLevelIndex();
    }
    public void SelectRoom(GameObject room) {
        if (selectedRoom) {
            selectedRoom.Unselect();
        }
        selectedRoom = room.GetComponent<RoomIcon>();
        selectedRoom.Select();
        joinRoomBtn.interactable = room != null && PhotonNetwork.NickName.Length >= 3;
    }
    public void JoinSelectedRoom() {
        if (selectedRoom == null) {
            return;
        }
        PhotonNetwork.JoinRoom(selectedRoom.room.Name);
    }
    public void CreateRoom(GameObject lobbyInfo) {
        string room = lobbyNameField.text;
        byte players = (byte) lobbyInfo.transform.Find("MaxPlayers").Find("Slider").gameObject.GetComponent<Slider>().value;
        if (room == null || room == "" || players < 2) {
            return;
        }
        PhotonNetwork.CreateRoom(room, new RoomOptions{MaxPlayers=players, IsVisible=true}, TypedLobby.Default);
        Camera.main.transform.position = levelCameraPositions[0].transform.position;
        createLobbyPrompt.SetActive(false);
    }
    public void SetMaxPlayersText(Slider slider) {
        sliderText.GetComponent<TextMeshProUGUI>().text = "" + slider.value;
    }
    public void ClearChat() {
        for (int i = 0; i < chatContent.transform.childCount; i++) {
            GameObject chatMsg = chatContent.transform.GetChild(i).gameObject;
            if (!chatMsg.activeSelf) continue;
            GameObject.Destroy(chatMsg);
        }
    }
    public void PopulatePlayerList() {
        for (int i = 0; i < playersContent.transform.childCount; i++) {
            GameObject pl = playersContent.transform.GetChild(i).gameObject;
            if (!pl.activeSelf) continue;
            GameObject.Destroy(pl);
        }

        int count = 0;
        SortedDictionary<int, Photon.Realtime.Player> sortedPlayers = new SortedDictionary<int,Photon.Realtime.Player>(PhotonNetwork.CurrentRoom.Players);
        foreach (KeyValuePair<int, Photon.Realtime.Player> player in sortedPlayers) {
            Player pl = player.Value;
            
            GameObject newPl = GameObject.Instantiate(playersPrefab, Vector3.zero, Quaternion.identity);
            newPl.transform.SetParent(playersContent.transform);
            newPl.transform.localPosition = new Vector3(0, -(count-- * 40f), 0);
            newPl.transform.localScale = Vector3.one;
            newPl.SetActive(true);
            SetText(newPl.transform.Find("Text").gameObject, (pl.IsMasterClient ? "(Host) " : "") + pl.NickName);
            RectTransform tf = newPl.GetComponent<RectTransform>();
            tf.offsetMax = new Vector2(330, tf.offsetMax.y);
        }

        startGameBtn.interactable = PhotonNetwork.IsMasterClient;
        levelDropdown.interactable = PhotonNetwork.IsMasterClient;
        starSlider.interactable = PhotonNetwork.IsMasterClient;
    }
    
    [PunRPC]
    public void ChatMessage(string message, Vector3 color) {
        float y = 0;
        for (int i = 0; i < chatContent.transform.childCount; i++) {
            GameObject child = chatContent.transform.GetChild(i).gameObject;
            if (!child.activeSelf) continue;
            y -= (child.GetComponent<RectTransform>().rect.height + 20);
        }

        GameObject chat = GameObject.Instantiate(chatPrefab, Vector3.zero, Quaternion.identity);
        chat.transform.SetParent(chatContent.transform);
        chat.transform.localPosition = new Vector3(0, y, 0);
        chat.transform.localScale = Vector3.one;
        chat.SetActive(true);
        GameObject txtObject = chat.transform.Find("Text").gameObject;
        SetText(txtObject, message, new Color(color.x, color.y, color.z));
        Canvas.ForceUpdateCanvases();
        RectTransform tf = txtObject.GetComponent<RectTransform>();
        Rect rect = tf.rect;
        Bounds bounds = txtObject.GetComponent<TextMeshProUGUI>().textBounds;
        tf.sizeDelta = new Vector2(tf.sizeDelta.x, (bounds.max.y - bounds.min.y) - 15f);
    }
    public void SendChat(TMP_InputField input) {
        string text = input.text;
        if (input.text == null || input.text == "")
            return;
        
        photonView.RPC("ChatMessage", RpcTarget.All, PhotonNetwork.NickName + ": " + text, ColorToVector(Color.black));
        input.text = "";
        // EventSystem.current.SetSelectedGameObject(input.gameObject);
    }
    public static Vector3 ColorToVector(Color color) {
        return new Vector3(color.r, color.g, color.b);
    }
    public void OnRoomListUpdate(List<RoomInfo> roomList) {
        for (int i = 0; i < lobbiesContent.transform.childCount; i++) {
            GameObject roomObj = lobbiesContent.transform.GetChild(i).gameObject;
            if (!roomObj.activeSelf) continue;
            GameObject.Destroy(roomObj);
        }
        int count = 0;
        foreach (RoomInfo room in roomList) {
            if (!room.IsVisible || !room.IsOpen || room.MaxPlayers <= 0) {
                continue;
            } 
            GameObject newLobby = GameObject.Instantiate(lobbyPrefab, Vector3.zero, Quaternion.identity, lobbiesContent.transform);
            newLobby.SetActive(true);
            RectTransform rect = newLobby.GetComponent<RectTransform>();
            rect.offsetMin = new Vector2(0, (count-1) * 55f);
            rect.offsetMax = new Vector2(0, (count) * 55f);
            SetText(newLobby.transform.Find("LobbyName").gameObject, "Name: " + room.Name);
            SetText(newLobby.transform.Find("LobbyPlayers").gameObject, "Players: " + room.PlayerCount + "/" + room.MaxPlayers);
            newLobby.GetComponent<RoomIcon>().room = room;
            count--;
        }
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
    public void Quit() {
        if (quit) return;
        sfx.PlayOneShot(buhBye);
        quit = true;
    }

    public void SetStarRequirementSlider(Slider slider) {
        SetStarRequirement(slider.value * 5);
    }
    public void SetStarRequirement(float value) {
        starsText.text = "" + value;
        
        if (PhotonNetwork.IsMasterClient) {
            ExitGames.Client.Photon.Hashtable table = new ExitGames.Client.Photon.Hashtable();
            table[NetworkManager.PROPKEY_STARS] = (int) value;
            PhotonNetwork.CurrentRoom.SetCustomProperties(table);
        }
        GlobalController.Instance.starRequirement = (int) value;
        starSlider.value = value / 5;
    }

    public void OnToggleUpdate() {
        bool value = ndsResolutionToggle.isOn;
        PlayerPrefs.SetInt("NDSResolution", value ? 1 : 0);
        if (!value) return;
        
        // Screen.SetResolution(256, 192, Screen.fullScreenMode);
    }
    public void OnFullscreenUpdate() {
        bool value = fullscreenToggle.isOn;
        PlayerPrefs.SetInt("Fullscreen", value ? 1 : 0);
        if (value) {
            prevWidth = Screen.width;
            prevHeight = Screen.height;
            Resolution max = Screen.resolutions[Screen.resolutions.Length-1];
            Screen.SetResolution(max.width, max.height, FullScreenMode.FullScreenWindow);
        } else {
            Screen.SetResolution(prevWidth, prevHeight, FullScreenMode.Windowed);
        }
    }
}
