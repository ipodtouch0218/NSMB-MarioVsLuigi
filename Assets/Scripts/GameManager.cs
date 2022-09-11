using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using NSMB.Utils;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class GameManager : MonoBehaviour, IOnEventCallback, IInRoomCallbacks, IConnectionCallbacks, IMatchmakingCallbacks {

    private static GameManager _instance;
    public static GameManager Instance {
        get {
            if (_instance)
                return _instance;

            if (SceneManager.GetActiveScene().buildIndex >= 2)
                _instance = FindObjectOfType<GameManager>();

            return _instance;
        }
        private set => _instance = value;
    }

    public MusicData mainMusic, invincibleMusic, megaMushroomMusic;

    public int levelMinTileX, levelMinTileY, levelWidthTile, levelHeightTile;
    public float cameraMinY, cameraHeightY, cameraMinX = -1000, cameraMaxX = 1000;
    public bool loopingLevel = true;
    public Vector3 spawnpoint;
    public Tilemap tilemap;
    [ColorUsage(false)] public Color levelUIColor = new(24, 178, 170);
    public bool spawnBigPowerups = true, spawnVerticalPowerups = true;
    public string levelDesigner = "", richPresenceId = "", levelName = "Unknown";
    private TileBase[] originalTiles;
    private BoundsInt origin;
    private GameObject[] starSpawns;
    private readonly List<GameObject> remainingSpawns = new();
    public int startServerTime, endServerTime = -1;
    public long startRealTime = -1, endRealTime = -1;

    public Canvas nametagCanvas;
    public GameObject nametagPrefab;

    //Audio
    public AudioSource music, sfx;
    private LoopingMusic loopMusic;
    public Enums.MusicState? musicState = null;

    public GameObject localPlayer;
    public bool paused, loaded, started;
    public GameObject pauseUI, pausePanel, pauseButton, hostExitUI, hostExitButton;
    public bool gameover = false, musicEnabled = false;
    public readonly HashSet<Player> loadedPlayers = new();
    public int starRequirement, timedGameDuration = -1, coinRequirement;
    public bool hurryup = false;
    public bool tenSecondCountdown = false;

    public int playerCount = 1;
    public List<PlayerController> players = new();
    public EnemySpawnpoint[] enemySpawnpoints;

    private GameObject[] coins;
    public SpectationManager SpectationManager { get; private set; }

    private ParticleSystem brickBreak;

    public HashSet<Player> nonSpectatingPlayers;

    // EVENT CALLBACK
    public void SendAndExecuteEvent(Enums.NetEventIds eventId, object parameters, SendOptions sendOption, RaiseEventOptions eventOptions = null) {
        if (eventOptions == null)
            eventOptions = NetworkUtils.EventOthers;

        HandleEvent((byte) eventId, parameters, PhotonNetwork.LocalPlayer, null);
        PhotonNetwork.RaiseEvent((byte) eventId, parameters, eventOptions, sendOption);
    }
    public void OnEvent(EventData e) {
        var players = PhotonNetwork.CurrentRoom.Players;
        HandleEvent(e.Code, e.CustomData, players.ContainsKey(e.Sender) ? players[e.Sender] : null, e.Parameters);
    }
    public void HandleEvent(byte eventId, object customData, Player sender, ParameterDictionary parameters) {
        object[] data = customData as object[];

        //Debug.Log($"id:{eventId} sender:{sender} master:{sender?.IsMasterClient ?? false}");
        switch (eventId) {
        case PunEvent.Instantiation: {
            Hashtable table = (Hashtable) parameters.paramDict[245];
            string prefab = (string) table[0];
            int viewId = (int) table[7];

            //Debug.Log((sender.IsMasterClient ? "[H] " : "") + sender.NickName + " (" + sender.UserId + ") - Instantiating " + prefab);

            //even the host can't be trusted...
            if ((sender?.IsMasterClient ?? false) && (prefab.Contains("Static") || prefab.Contains("1-Up") || (musicEnabled && prefab.Contains("Player")))) {
                //abandon ship
                PhotonNetwork.Disconnect();
                return;
            }

            //server room instantiation
            if (sender == null || sender.IsMasterClient)
                return;

            PlayerController controller = players.FirstOrDefault(pl => sender == pl.photonView.Owner);
            bool invalidProjectile = controller.state != Enums.PowerupState.FireFlower && prefab.Contains("Fireball");
            invalidProjectile |= controller.state != Enums.PowerupState.IceFlower && prefab.Contains("Iceball");

            if (prefab.Contains("Enemy") || prefab.Contains("Powerup") || prefab.Contains("Static") || prefab.Contains("Bump") || prefab.Contains("BigStar") || prefab.Contains("Coin") || ((!nonSpectatingPlayers.Contains(sender) || musicEnabled) && prefab.Contains("Player"))) {
                PhotonNetwork.CloseConnection(sender);
                PhotonNetwork.DestroyPlayerObjects(sender);
            }

            break;
        }
        case (byte) Enums.NetEventIds.AllFinishedLoading: {
            if (!(sender?.IsMasterClient ?? true))
                return;

            if (loaded)
                break;

            StartCoroutine(LoadingComplete((int) customData));
            break;
        }
        case (byte) Enums.NetEventIds.EndGame: {
            if (!(sender?.IsMasterClient ?? false))
                return;

            Player winner = (Player) customData;
            StartCoroutine(EndGame(winner));
            break;
        }
        case (byte) Enums.NetEventIds.SetTile: {

            int x = (int) data[0];
            int y = (int) data[1];
            string tilename = (string) data[2];
            Vector3Int loc = new(x, y, 0);

            TileBase tile = Utils.GetTileFromCache(tilename);
            tilemap.SetTile(loc, tile);
            //Debug.Log($"SetTile by {sender?.NickName} ({sender?.UserId}): {tilename}");
            break;
        }
        case (byte) Enums.NetEventIds.SetTileBatch: {
            int x = (int) data[0];
            int y = (int) data[1];
            int width = (int) data[2];
            int height = (int) data[3];
            string[] tiles = (string[]) data[4];
            TileBase[] tileObjects = new TileBase[tiles.Length];
            for (int i = 0; i < tiles.Length; i++) {
                string tile = tiles[i];
                if (tile == "")
                    continue;

                tileObjects[i] = (TileBase) Resources.Load("Tilemaps/Tiles/" + tile);
            }
            tilemap.SetTilesBlock(new BoundsInt(x, y, 0, width, height, 1), tileObjects);
            //Debug.Log($"SetTileBatch by {sender?.NickName} ({sender?.UserId}): {tileObjects[0]}");
            break;
        }
        case (byte) Enums.NetEventIds.ResetTiles: {
            if (!(sender?.IsMasterClient ?? false))
                return;

            tilemap.SetTilesBlock(origin, originalTiles);

            foreach (GameObject coin in coins) {
                //dont use setactive cause it breaks animation cycles being syncewd
                coin.GetComponent<SpriteRenderer>().enabled = true;
                coin.GetComponent<BoxCollider2D>().enabled = true;
            }

            StartCoroutine(BigStarRespawn());

            if (!PhotonNetwork.IsMasterClient)
                return;

            foreach (EnemySpawnpoint point in enemySpawnpoints)
                point.AttemptSpawning();

            break;
        }
        case (byte) Enums.NetEventIds.SyncTilemap: {
            if (!(sender?.IsMasterClient ?? false))
                return;

            Hashtable changes = (Hashtable) customData;
            //Debug.Log($"SyncTilemap by {sender?.NickName} ({sender?.UserId}): {changes}");
            Utils.ApplyTilemapChanges(originalTiles, origin, tilemap, changes);
            break;
        }
        case (byte) Enums.NetEventIds.PlayerFinishedLoading: {
            if (sender == null || !nonSpectatingPlayers.Contains(sender))
                return;

            loadedPlayers.Add(sender);
            CheckIfAllLoaded();
            break;
        }
        case (byte) Enums.NetEventIds.BumpTile: {

            int x = (int) data[0];
            int y = (int) data[1];

            bool downwards = (bool) data[2];
            string newTile = (string) data[3];
            string spawnResult = (string) data[4];
            Vector2 spawnOffset = data.Length > 5 ? (Vector2) data[5] : Vector2.zero;

            Vector3Int loc = new(x, y, 0);

            if (tilemap.GetTile(loc) == null)
                return;

            GameObject bump = (GameObject) Instantiate(Resources.Load("Prefabs/Bump/BlockBump"), Utils.TilemapToWorldPosition(loc) + Vector3.one * 0.25f, Quaternion.identity);
            BlockBump bb = bump.GetComponentInChildren<BlockBump>();

            bb.fromAbove = downwards;
            bb.resultTile = newTile;
            bb.sprite = tilemap.GetSprite(loc);
            bb.resultPrefab = spawnResult;
            bb.spawnOffset = spawnOffset;

            tilemap.SetTile(loc, null);
            break;
        }
        case (byte) Enums.NetEventIds.SetThenBumpTile: {

            int x = (int) data[0];
            int y = (int) data[1];

            bool downwards = (bool) data[2];
            string newTile = (string) data[3];
            string spawnResult = (string) data[4];

            Vector3Int loc = new(x, y, 0);

            tilemap.SetTile(loc, Utils.GetTileFromCache(newTile));
            //Debug.Log($"SetThenBumpTile by {sender?.NickName} ({sender?.UserId}): {newTile}");
            tilemap.RefreshTile(loc);

            GameObject bump = (GameObject) Instantiate(Resources.Load("Prefabs/Bump/BlockBump"), Utils.TilemapToWorldPosition(loc) + Vector3.one * 0.25f, Quaternion.identity);
            BlockBump bb = bump.GetComponentInChildren<BlockBump>();

            bb.fromAbove = downwards;
            bb.resultTile = newTile;
            bb.sprite = tilemap.GetSprite(loc);
            bb.resultPrefab = spawnResult;

            tilemap.SetTile(loc, null);
            break;
        }
        case (byte) Enums.NetEventIds.SetCoinState: {
            if (!(sender?.IsMasterClient ?? false))
                return;

            int view = (int) data[0];
            bool visible = (bool) data[1];
            GameObject coin = PhotonView.Find(view).gameObject;
            coin.GetComponent<SpriteRenderer>().enabled = visible;
            coin.GetComponent<BoxCollider2D>().enabled = visible;
            break;
        }
        case (byte) Enums.NetEventIds.SpawnParticle: {
            int x = (int) data[0];
            int y = (int) data[1];
            string particleName = (string) data[2];
            Vector3 color = data.Length > 3 ? (Vector3) data[3] : new Vector3(1, 1, 1);
            Vector3 worldPos = Utils.TilemapToWorldPosition(new(x, y)) + new Vector3(0.25f, 0.25f);

            GameObject particle;
            if (particleName == "BrickBreak") {
                brickBreak.transform.position = worldPos;
                brickBreak.Emit(new() { startColor = new Color(color.x, color.y, color.z, 1) }, 4);
            } else {
                particle = (GameObject) Instantiate(Resources.Load("Prefabs/Particle/" + particleName), worldPos, Quaternion.identity);

                ParticleSystem system = particle.GetComponent<ParticleSystem>();
                ParticleSystem.MainModule main = system.main;
                main.startColor = new Color(color.x, color.y, color.z, 1);
            }

            break;
        }
        case (byte) Enums.NetEventIds.SpawnResizableParticle: {
            Vector2 pos = (Vector2) data[0];
            bool right = (bool) data[1];
            bool upsideDown = (bool) data[2];
            Vector2 size = (Vector2) data[3];
            string prefab = (string) data[4];
            GameObject particle = (GameObject) Instantiate(Resources.Load("Prefabs/Particle/" + prefab), pos, Quaternion.Euler(0, 0, upsideDown ? 180 : 0));

            SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();
            sr.size = size;

            Rigidbody2D body = particle.GetComponent<Rigidbody2D>();
            body.velocity = new Vector2(right ? 7 : -7, 6);
            body.angularVelocity = right ^ upsideDown ? -300 : 300;

            particle.transform.position += new Vector3(sr.size.x / 4f, size.y / 4f * (upsideDown ? -1 : 1));
            break;
        }
        }
    }

    // MATCHMAKING CALLBACKS

    public void OnFriendListUpdate(List<FriendInfo> friendList) { }
    public void OnCreatedRoom() { }
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
    public void OnLeftRoom() {
        OnDisconnected(DisconnectCause.DisconnectByServerLogic);
    }

    // ROOM CALLBACKS
    public void OnJoinedRoom() { }
    public void OnPlayerPropertiesUpdate(Player player, Hashtable playerProperties) { }
    public void OnRoomPropertiesUpdate(Hashtable properties) { }

    public void OnMasterClientSwitched(Player newMaster) {
        //TODO: chat message

        if (newMaster.IsLocal) {
            //i am de captain now
            PhotonNetwork.CurrentRoom.SetCustomProperties(new() {
                [Enums.NetRoomProperties.HostName] = newMaster.NickName
            });
        }
    }

    public void OnPlayerEnteredRoom(Player newPlayer) {
        //Spectator joined. Sync the room state.
        //TODO: chat message

        if (PhotonNetwork.IsMasterClient) {
            Utils.GetCustomProperty(Enums.NetRoomProperties.Bans, out object[] bans);
            List<NameIdPair> banList = bans.Cast<NameIdPair>().ToList();
            if (banList.Any(nip => nip.userId == newPlayer.UserId)) {

                PhotonNetwork.CloseConnection(newPlayer);
                return;
            }
        }

        //SYNCHRONIZE PLAYER STATE
        if (localPlayer)
            localPlayer.GetComponent<PlayerController>().UpdateGameState();

        //SYNCHRONIZE TILEMAPS
        if (PhotonNetwork.IsMasterClient) {
            Hashtable changes = Utils.GetTilemapChanges(originalTiles, origin, tilemap);
            RaiseEventOptions options = new() { CachingOption = EventCaching.DoNotCache, TargetActors = new int[] { newPlayer.ActorNumber } };
            PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.SyncTilemap, changes, options, SendOptions.SendReliable);

            foreach (GameObject coin in coins) {
                if (!coin.GetComponent<SpriteRenderer>().enabled)
                    PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.SetCoinState, new object[] { coin.GetPhotonView().ViewID, false }, options, SendOptions.SendReliable);
            }
        }
    }
    public void OnPlayerLeftRoom(Player otherPlayer) {
        //TODO: player disconnect message

        nonSpectatingPlayers = PhotonNetwork.CurrentRoom.Players.Values.Where(pl => !pl.IsSpectator()).ToHashSet();
        CheckIfAllLoaded();

        if (musicEnabled && FindObjectsOfType<PlayerController>().Length <= 0) {
            //all players left.
            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.EndGame, null, NetworkUtils.EventAll, SendOptions.SendReliable);
        }
    }

    // CONNECTION CALLBACKS
    public void OnConnected() { }
    public void OnDisconnected(DisconnectCause cause) {
        GlobalController.Instance.disconnectCause = cause;
        SceneManager.LoadScene(0);
    }
    public void OnRegionListReceived(RegionHandler handler) { }
    public void OnCustomAuthenticationResponse(Dictionary<string, object> response) { }
    public void OnCustomAuthenticationFailed(string failure) { }
    public void OnConnectedToMaster() { }


    //Register callbacks & controls
    public void OnEnable() {
        Instance = this;
        PhotonNetwork.AddCallbackTarget(this);
        InputSystem.controls.UI.Pause.performed += OnPause;
    }
    public void OnDisable() {
        foreach (GameManager gm in FindObjectsOfType<GameManager>()) {
            if (gm != this) {
                Instance = gm;
                return;
            }
        }
        PhotonNetwork.RemoveCallbackTarget(this);
        InputSystem.controls.UI.Pause.performed -= OnPause;
    }

    public void Awake() {
        Instance = this;
    }

    public void Start() {
        SpectationManager = GetComponent<SpectationManager>();
        loopMusic = GetComponent<LoopingMusic>();
        coins = GameObject.FindGameObjectsWithTag("coin");
        levelUIColor.a = .7f;

        InputSystem.controls.LoadBindingOverridesFromJson(GlobalController.Instance.controlsJson);

        //Spawning in editor??
        if (!PhotonNetwork.IsConnectedAndReady) {
            PhotonNetwork.OfflineMode = true;
            PhotonNetwork.CreateRoom("Debug", new() {
                CustomRoomProperties = NetworkUtils.DefaultRoomProperties
            });
        }

        nonSpectatingPlayers = PhotonNetwork.CurrentRoom.Players.Values.Where(pl => !pl.IsSpectator()).ToHashSet();

        //Respawning Tilemaps
        origin = new BoundsInt(levelMinTileX, levelMinTileY, 0, levelWidthTile, levelHeightTile, 1);
        originalTiles = tilemap.GetTilesBlock(origin);

        //Star spawning
        starSpawns = GameObject.FindGameObjectsWithTag("StarSpawn");
        Utils.GetCustomProperty(Enums.NetRoomProperties.StarRequirement, out starRequirement);
        Utils.GetCustomProperty(Enums.NetRoomProperties.CoinRequirement, out coinRequirement);

        SceneManager.SetActiveScene(gameObject.scene);

        PhotonNetwork.IsMessageQueueRunning = true;

        if (!GlobalController.Instance.joinedAsSpectator) {
            localPlayer = PhotonNetwork.Instantiate("Prefabs/" + Utils.GetCharacterData().prefab, spawnpoint, Quaternion.identity, 0);
            localPlayer.GetComponent<Rigidbody2D>().isKinematic = true;

            RaiseEventOptions options = new() { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCache };
            SendAndExecuteEvent(Enums.NetEventIds.PlayerFinishedLoading, null, SendOptions.SendReliable, options);
        } else {
            SpectationManager.Spectating = true;
        }

        brickBreak = ((GameObject) Instantiate(Resources.Load("Prefabs/Particle/BrickBreak"))).GetComponent<ParticleSystem>();
    }

    private void CheckIfAllLoaded() {
        if (loaded || !PhotonNetwork.IsMasterClient || loadedPlayers.Count < nonSpectatingPlayers.Count)
            return;

        RaiseEventOptions options = new() { CachingOption = EventCaching.AddToRoomCacheGlobal, Receivers = ReceiverGroup.All };
        SendAndExecuteEvent(Enums.NetEventIds.AllFinishedLoading, PhotonNetwork.ServerTimestamp + ((nonSpectatingPlayers.Count - 1) * 250) + 1000, SendOptions.SendReliable, options);
        loaded = true;
    }

    private PlayerController GetController(Player player) {
        foreach (PlayerController pl in players) {
            if (pl.photonView.Owner == player)
                return pl;
        }
        return null;
    }

    IEnumerator LoadingComplete(int startTimestamp) {
        GlobalController.Instance.DiscordController.UpdateActivity();

        loaded = true;
        loadedPlayers.Clear();
        enemySpawnpoints = FindObjectsOfType<EnemySpawnpoint>();
        bool spectating = GlobalController.Instance.joinedAsSpectator;
        bool gameStarting = startTimestamp - PhotonNetwork.ServerTimestamp > 0;

        StartCoroutine(BigStarRespawn(false));

        if (PhotonNetwork.IsMasterClient && !PhotonNetwork.OfflineMode) {
            //clear buffered loading complete events.
            RaiseEventOptions options = new() { Receivers = ReceiverGroup.All, CachingOption = EventCaching.RemoveFromRoomCache };
            PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.PlayerFinishedLoading, null, options, SendOptions.SendReliable);
        }

        yield return new WaitForSeconds(Mathf.Max(1f, (startTimestamp - PhotonNetwork.ServerTimestamp) / 1000f));

        GameObject canvas = GameObject.FindGameObjectWithTag("LoadingCanvas");
        if (canvas) {
            canvas.GetComponent<Animator>().SetTrigger(spectating ? "spectating" : "loaded");
            //please just dont beep at me :(
            AudioSource source = canvas.GetComponent<AudioSource>();
            source.Stop();
            source.volume = 0;
            source.enabled = false;
            Destroy(source);
        }

        started = true;

        playerCount = players.Count;
        foreach (PlayerController controllers in players)
            if (controllers) {
                if (spectating && controllers.sfx) {
                    controllers.sfxBrick.enabled = true;
                    controllers.sfx.enabled = true;
                }
                controllers.gameObject.SetActive(spectating);
            }

        try {
            ScoreboardUpdater.instance.Populate(players);
            if (Settings.Instance.scoreboardAlways)
                ScoreboardUpdater.instance.SetEnabled();
        } catch { }

        if (gameStarting) {
            yield return new WaitForSeconds(3.5f);
            sfx.PlayOneShot(Enums.Sounds.UI_StartGame.GetClip());

            if (PhotonNetwork.IsMasterClient)
                foreach (EnemySpawnpoint point in FindObjectsOfType<EnemySpawnpoint>())
                    point.AttemptSpawning();

            if (localPlayer)
                localPlayer.GetComponent<PlayerController>().OnGameStart();
        }

        startServerTime = startTimestamp + 3500;
        foreach (var wfgs in FindObjectsOfType<WaitForGameStart>())
            wfgs.AttemptExecute();

        yield return new WaitForSeconds(1f);

        musicEnabled = true;
        Utils.GetCustomProperty(Enums.NetRoomProperties.Time, out timedGameDuration);

        startRealTime = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (timedGameDuration > 0) {
            endServerTime = startTimestamp + 4500 + timedGameDuration * 1000;
            endRealTime = startRealTime + 4500 + timedGameDuration * 1000;
        }

        GlobalController.Instance.DiscordController.UpdateActivity();

        if (canvas)
            SceneManager.UnloadSceneAsync("Loading");
    }

    IEnumerator CountdownSound(float t, float times) { //The match countdown sound system. t is the tempo, and times is the # of times the sound will play (variable if match is started at 10s or less)
        for (int i = 0; i < times; i++) {
            if (gameover) //This is to ensure that if a win or draw occurs in the last 10 seconds, the countdown sound doesn't play past the match's length.
                yield break;

            if (i >= (times * 0.7f)) { //Countdown sound will speed up and play twice per second as a match's end draws near.
                sfx.PlayOneShot(Enums.Sounds.UI_Countdown_0.GetClip());
                yield return new WaitForSeconds(t / 2);
                sfx.PlayOneShot(Enums.Sounds.UI_Countdown_0.GetClip());
                yield return new WaitForSeconds(t / 2);
            } else { //Or it'll just play normally.
                sfx.PlayOneShot(Enums.Sounds.UI_Countdown_0.GetClip());
                yield return new WaitForSeconds(t);
            }
        }
    }

    private IEnumerator EndGame(Player winner) {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new() { [Enums.NetRoomProperties.GameStarted] = false });
        gameover = true;
        music.Stop();
        GameObject text = GameObject.FindWithTag("wintext");
        text.GetComponent<TMP_Text>().text = winner != null ? $"{ winner.GetUniqueNickname() } Wins!" : "It's a draw...";

        yield return new WaitForSecondsRealtime(1);
        text.GetComponent<Animator>().SetTrigger("start");

        AudioMixer mixer = music.outputAudioMixerGroup.audioMixer;
        mixer.SetFloat("MusicSpeed", 1f);
        mixer.SetFloat("MusicPitch", 1f);

        bool win = winner != null && winner.IsLocal;
        bool draw = winner == null;
        int secondsUntilMenu;
        secondsUntilMenu = draw ? 5 : 4;

        if (draw)
            music.PlayOneShot(Enums.Sounds.UI_Match_Draw.GetClip());
        else if (win)
            music.PlayOneShot(Enums.Sounds.UI_Match_Win.GetClip());
        else
            music.PlayOneShot(Enums.Sounds.UI_Match_Lose.GetClip());

        //TOOD: make a results screen?

        yield return new WaitForSecondsRealtime(secondsUntilMenu);
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.DestroyAll();
        SceneManager.LoadScene("MainMenu");
    }

    private IEnumerator BigStarRespawn(bool wait = true) {
        if (wait)
            yield return new WaitForSeconds(10.4f - playerCount / 5f);

        if (!PhotonNetwork.IsMasterClient || gameover)
            yield break;

        bigwhile:
        while (true) {
            if (remainingSpawns.Count <= 0)
                remainingSpawns.AddRange(starSpawns);

            int index = Random.Range(0, remainingSpawns.Count);
            Vector3 spawnPos = remainingSpawns[index].transform.position;
            //Check for people camping spawn
            foreach (var hit in Physics2D.OverlapCircleAll(spawnPos, 4)) {
                if (hit.gameObject.CompareTag("Player")) {
                    //cant spawn here
                    remainingSpawns.RemoveAt(index);
                    yield return new WaitForSeconds(0.2f);
                    goto bigwhile;
                }
            }

            PhotonNetwork.InstantiateRoomObject("Prefabs/BigStar", spawnPos, Quaternion.identity);
            remainingSpawns.RemoveAt(index);
            break;
        }
    }

    public void Update() {
        if (gameover)
            return;

        if (endServerTime != -1) {
            float timeRemaining = (endServerTime - PhotonNetwork.ServerTimestamp) / 1000f;

            if (timeRemaining > 0 && !gameover) {
                timeRemaining -= Time.deltaTime;
                //play hurry sound if time < 60 (will play instantly when a match starts with a time limit less than 60s)
                if (!hurryup && (timeRemaining <= 60)) {
                    hurryup = true;
                    sfx.PlayOneShot(Enums.Sounds.UI_HurryUp.GetClip());
                }
                if (!tenSecondCountdown && (timeRemaining <= 10)) {
                    StartCoroutine(CountdownSound(1.0f, timeRemaining));
                    tenSecondCountdown = true;
                }
                if (timeRemaining - Time.deltaTime <= 0) {
                    sfx.PlayOneShot(Enums.Sounds.UI_Countdown_1.GetClip());
                    CheckForWinner();
                }
            }
        }

        if (started && musicEnabled) {
            bool allNull = true;
            foreach (PlayerController controller in players) {
                if (controller) {
                    allNull = false;
                    break;
                }
            }
            if (SpectationManager.Spectating && allNull) {
                StartCoroutine(EndGame(null));
                return;
            }
        }

        if (musicEnabled)
            HandleMusic();
    }

    public void CreateNametag(PlayerController controller) {
        GameObject nametag = Instantiate(nametagPrefab, nametagPrefab.transform.parent);
        nametag.GetComponent<UserNametag>().parent = controller;
        nametag.SetActive(true);
    }

    public void CheckForWinner() {
        if (gameover || !PhotonNetwork.IsMasterClient)
            return;

        bool starGame = starRequirement != -1;
        bool timeUp = endServerTime != -1 && endServerTime - Time.deltaTime - PhotonNetwork.ServerTimestamp < 0;
        int winningStars = -1;
        List<PlayerController> winningPlayers = new();
        List<PlayerController> alivePlayers = new();
        foreach (var player in players) {
            if (player == null || player.lives == 0)
                continue;

            alivePlayers.Add(player);

            if ((starGame && player.stars >= starRequirement) || timeUp) {
                //we're in a state where this player would win.
                //check if someone has more stars
                if (player.stars > winningStars) {
                    winningPlayers.Clear();
                    winningStars = player.stars;
                    winningPlayers.Add(player);
                } else if (player.stars == winningStars) {
                    winningPlayers.Add(player);
                }
            }
        }
        //LIVES CHECKS
        if (alivePlayers.Count == 0) {
            //everyone's dead...? ok then, draw?
            PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.EndGame, null, NetworkUtils.EventAll, SendOptions.SendReliable);
            return;
        } else if (alivePlayers.Count == 1 && playerCount >= 2) {
            //one player left alive (and not in a solo game). winner!
            PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.EndGame, alivePlayers[0].photonView.Owner, NetworkUtils.EventAll, SendOptions.SendReliable);
            return;
        }
        //TIMED CHECKS
        if (timeUp) {
            Utils.GetCustomProperty(Enums.NetRoomProperties.DrawTime, out bool draw);
            //time up! check who has most stars, if a tie keep playing, if draw is on end game in a draw
            if (draw)
                // it's a draw! Thanks for playing the demo!
                PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.EndGame, null, NetworkUtils.EventAll, SendOptions.SendReliable);
            else if (winningPlayers.Count == 1)
                PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.EndGame, winningPlayers[0].photonView.Owner, NetworkUtils.EventAll, SendOptions.SendReliable);

            return;
        }
        if (starGame && winningStars >= starRequirement) {
            if (winningPlayers.Count == 1)
                PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.EndGame, winningPlayers[0].photonView.Owner, NetworkUtils.EventAll, SendOptions.SendReliable);

            return;
        }
    }

    private void PlaySong(Enums.MusicState state, MusicData musicToPlay) {
        if (musicState == state)
            return;

        loopMusic.Play(musicToPlay);
        musicState = state;
    }

    private void HandleMusic() {
        bool invincible = false;
        bool mega = false;
        bool speedup = false;

        foreach (var player in players) {
            if (!player)
                continue;

            if (player.state == Enums.PowerupState.MegaMushroom && player.giantTimer != 15)
                mega = true;
            if (player.invincible > 0)
                invincible = true;
            if ((player.stars + 1f) / starRequirement >= 0.95f || hurryup != false)
                speedup = true;
            if (player.lives == 1 && players.Count <= 2)
                speedup = true;
        }

        speedup |= players.All(pl => !pl || pl.lives == 1 || pl.lives == 0);

        if (mega) {
            PlaySong(Enums.MusicState.MegaMushroom, megaMushroomMusic);
        } else if (invincible) {
            PlaySong(Enums.MusicState.Starman, invincibleMusic);
        } else {
            PlaySong(Enums.MusicState.Normal, mainMusic);
        }

        loopMusic.FastMusic = speedup;
    }

    public void OnPause(InputAction.CallbackContext context) {
        Pause();
    }

    public void Pause() {
        if (gameover || !musicEnabled)
            return;

        paused = !paused;
        sfx.PlayOneShot(Enums.Sounds.UI_Pause.GetClip());
        pauseUI.SetActive(paused);
        pausePanel.SetActive(true);
        hostExitUI.SetActive(false);
        EventSystem.current.SetSelectedGameObject(pauseButton);
    }

    public void AttemptQuit() {

        if (PhotonNetwork.IsMasterClient) {
            sfx.PlayOneShot(Enums.Sounds.UI_Decide.GetClip());
            pausePanel.SetActive(false);
            hostExitUI.SetActive(true);
            EventSystem.current.SetSelectedGameObject(hostExitButton);
            return;
        }

        Quit();
    }

    public void HostEndMatch() {
        pauseUI.SetActive(false);
        sfx.PlayOneShot(Enums.Sounds.UI_Decide.GetClip());
        PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.EndGame, null, NetworkUtils.EventAll, SendOptions.SendReliable);
    }

    public void Quit() {
        sfx.PlayOneShot(Enums.Sounds.UI_Decide.GetClip());
        PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene("MainMenu");
    }

    public void HostQuitCancel() {
        pausePanel.SetActive(true);
        hostExitUI.SetActive(false);
        sfx.PlayOneShot(Enums.Sounds.UI_Back.GetClip());
        EventSystem.current.SetSelectedGameObject(pauseButton);
    }

    //lazy mofo
    private float? middleX, minX, minY, maxX, maxY;
    public float GetLevelMiddleX() {
        if (middleX == null)
            middleX = (GetLevelMaxX() + GetLevelMinX()) / 2;
        return (float) middleX;
    }
    public float GetLevelMinX() {
        if (minX == null)
            minX = (levelMinTileX * tilemap.transform.localScale.x) + tilemap.transform.position.x;
        return (float) minX;
    }
    public float GetLevelMinY() {
        if (minY == null)
            minY = (levelMinTileY * tilemap.transform.localScale.y) + tilemap.transform.position.y;
        return (float) minY;
    }
    public float GetLevelMaxX() {
        if (maxX == null)
            maxX = ((levelMinTileX + levelWidthTile) * tilemap.transform.localScale.x) + tilemap.transform.position.x;
        return (float) maxX;
    }
    public float GetLevelMaxY() {
        if (maxY == null)
            maxY =  ((levelMinTileY + levelHeightTile) * tilemap.transform.localScale.y) + tilemap.transform.position.y;
        return (float) maxY;
    }


    public float size = 1.39f, ySize = 0.8f;
    public Vector3 GetSpawnpoint(int playerIndex, int players = -1) {
        if (players <= -1)
            players = playerCount;
        if (players == 0)
            players = 1;

        float comp = (float) playerIndex/players * 2 * Mathf.PI + (Mathf.PI/2f) + (Mathf.PI/(2*players));
        float scale = (2-(players+1f)/players) * size;
        Vector3 spawn = spawnpoint + new Vector3(Mathf.Sin(comp) * scale, Mathf.Cos(comp) * (players > 2 ? scale * ySize : 0), 0);
        if (spawn.x < GetLevelMinX())
            spawn += new Vector3(levelWidthTile/2f, 0);
        if (spawn.x > GetLevelMaxX())
            spawn -= new Vector3(levelWidthTile/2f, 0);
        return spawn;
    }
    [Range(1,10)]
    public int playersToVisualize = 10;
    public void OnDrawGizmos() {

        if (!tilemap)
            return;

        for (int i = 0; i < playersToVisualize; i++) {
            Gizmos.color = new Color((float) i / playersToVisualize, 0, 0, 0.75f);
            Gizmos.DrawCube(GetSpawnpoint(i, playersToVisualize) + Vector3.down/4f, Vector2.one/2f);
        }

        Vector3 size = new(levelWidthTile/2f, levelHeightTile/2f);
        Vector3 origin = new(GetLevelMinX() + (levelWidthTile/4f), GetLevelMinY() + (levelHeightTile/4f), 1);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(origin, size);

        size = new Vector3(levelWidthTile/2f, cameraHeightY);
        origin = new Vector3(GetLevelMinX() + (levelWidthTile/4f), cameraMinY + (cameraHeightY/2f), 1);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(origin, size);


        if (!tilemap)
            return;
        for (int x = 0; x < levelWidthTile; x++) {
            for (int y = 0; y < levelHeightTile; y++) {
                Vector3Int loc = new(x+levelMinTileX, y+levelMinTileY, 0);
                TileBase tile = tilemap.GetTile(loc);
                if (tile is CoinTile)
                    Gizmos.DrawIcon(Utils.TilemapToWorldPosition(loc, this) + Vector3.one * 0.25f, "coin");
                if (tile is PowerupTile)
                    Gizmos.DrawIcon(Utils.TilemapToWorldPosition(loc, this) + Vector3.one * 0.25f, "powerup");
            }
        }

        Gizmos.color = new Color(1, 0.9f, 0.2f, 0.2f);
        foreach (GameObject starSpawn in GameObject.FindGameObjectsWithTag("StarSpawn")) {
            Gizmos.DrawCube(starSpawn.transform.position, Vector3.one);
            Gizmos.DrawIcon(starSpawn.transform.position, "star", true, new Color(1, 1, 1, 0.5f));
        }
    }

}
