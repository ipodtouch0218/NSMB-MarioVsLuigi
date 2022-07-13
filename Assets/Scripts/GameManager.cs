using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using NSMB.Utils;

public class GameManager : MonoBehaviour, IOnEventCallback, IInRoomCallbacks, IConnectionCallbacks, IMatchmakingCallbacks {
    private static GameManager _instance;
    public static GameManager Instance {
        get {
            if (!_instance && SceneManager.GetActiveScene().buildIndex >= 2)
                _instance = FindObjectOfType<GameManager>();

            return _instance;
        }
        private set {
            _instance = value;
        }
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
    TileBase[] originalTiles;
    BoundsInt origin;
    GameObject currentStar = null;
    GameObject[] starSpawns;
    readonly List<GameObject> remainingSpawns = new();
    float spawnStarCount;
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
    public readonly List<string> loadedPlayers = new();
    public int starRequirement, timedGameDuration = -1, coinRequirement;
    public bool hurryup = false;

    public int playerCount;
    public List<PlayerController> allPlayers = new();
    public EnemySpawnpoint[] enemySpawnpoints;

    private GameObject[] coins;
    public SpectationManager SpectationManager { get; private set; }

    ParticleSystem brickBreak;



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

        switch (eventId) {
        case PunEvent.Instantiation: {
            string prefab = (string) ((Hashtable) parameters.paramDict[245])[0];

            //server room instantiation
            if (sender == null)
                return;

            if (!PhotonNetwork.IsMasterClient || sender.IsMasterClient)
                return;

            if (prefab.Contains("Enemy") || prefab.Contains("Powerup") || prefab.Contains("Static") || prefab.Contains("Bump") || prefab.Contains("BigStar") || prefab.Contains("Coin")) {
                PhotonNetwork.CloseConnection(sender);
                PhotonNetwork.DestroyPlayerObjects(sender);
            }
            break;
        }
        case (byte) Enums.NetEventIds.AllFinishedLoading: {
            if (loaded)
                break;

            StartCoroutine(LoadingComplete((int) customData));
            break;
        }
        case (byte) Enums.NetEventIds.EndGame: {
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
            break;
        }
        case (byte) Enums.NetEventIds.ResetTiles: {
            ResetTiles();
            break;
        }
        case (byte) Enums.NetEventIds.SyncTilemap: {
            Hashtable changes = (Hashtable) customData;
            Utils.ApplyTilemapChanges(originalTiles, origin, tilemap, changes);
            break;
        }
        case (byte) Enums.NetEventIds.PlayerFinishedLoading: {
            Player player = (Player) customData;
            loadedPlayers.Add(player.NickName);
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
            bb.prefab = spawnResult;
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
            tilemap.RefreshTile(loc);

            GameObject bump = (GameObject) Instantiate(Resources.Load("Prefabs/Bump/BlockBump"), Utils.TilemapToWorldPosition(loc) + Vector3.one * 0.25f, Quaternion.identity);
            BlockBump bb = bump.GetComponentInChildren<BlockBump>();

            bb.fromAbove = downwards;
            bb.resultTile = newTile;
            bb.sprite = tilemap.GetSprite(loc);
            bb.prefab = spawnResult;

            tilemap.SetTile(loc, null);
            break;
        }
        case (byte) Enums.NetEventIds.SetCoinState: {
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
        case (byte) Enums.NetEventIds.PlayerDamagePlayer: {
            PhotonView attacker = PhotonView.Find((int) data[0]);
            PhotonView target = PhotonView.Find((int) data[1]);

            if (!attacker || !target)
                return;

            PlayerController attackerPlayer = attacker.GetComponent<PlayerController>();
            PlayerController targetPlayer = target.GetComponent<PlayerController>();

            if (!targetPlayer || !attackerPlayer)
                return;

            //attacker must be invincible or mega, and near the player
            if (Utils.WrappedDistance(targetPlayer.body.position, attackerPlayer.body.position) > 2)
                return;

            if (targetPlayer.invincible > 0 || targetPlayer.hitInvincibilityCounter > 0)
                return;

            if (!((attackerPlayer.state == Enums.PowerupState.BlueShell && attackerPlayer.inShell) ||
                attackerPlayer.invincible > 0 ||
                (attackerPlayer.state == Enums.PowerupState.MegaMushroom && attackerPlayer.giantTimer > 0) ||
                (attackerPlayer.groundpound && targetPlayer.state == Enums.PowerupState.MiniMushroom)))

                return;

            targetPlayer.photonView.RPC("Powerdown", RpcTarget.All, false);
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
    public void OnPlayerPropertiesUpdate(Player player, Hashtable playerProperties) {
        foreach (PlayerController players in allPlayers) {
            if (players == null)
                continue;

            if (players.photonView.Owner == player)
                players.LoadFromGameState();
        }
    }
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

        if (FindObjectsOfType<PlayerController>().Length <= 0) {
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
        PhotonNetwork.AddCallbackTarget(this);
        InputSystem.controls.UI.Pause.performed += OnPause;
    }
    public void OnDisable() {
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
            SendAndExecuteEvent(Enums.NetEventIds.PlayerFinishedLoading, PhotonNetwork.LocalPlayer, SendOptions.SendReliable, options);
        } else {
            SpectationManager.Spectating = true;
        }

        brickBreak = ((GameObject) Instantiate(Resources.Load("Prefabs/Particle/BrickBreak"))).GetComponent<ParticleSystem>();
    }

    IEnumerator LoadingComplete(int startTimestamp) {
        GlobalController.Instance.discordController.UpdateActivity();

        loaded = true;
        loadedPlayers.Clear();
        enemySpawnpoints = FindObjectsOfType<EnemySpawnpoint>();
        bool spectating = GlobalController.Instance.joinedAsSpectator;

        if (PhotonNetwork.IsMasterClient && !PhotonNetwork.OfflineMode) {
            //clear buffered loading complete events.
            RaiseEventOptions options = new() { Receivers = ReceiverGroup.All, CachingOption = EventCaching.RemoveFromRoomCache };
            PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.PlayerFinishedLoading, null, options, SendOptions.SendReliable);
        }


        if (!spectating) {
            yield return new WaitForSecondsRealtime((startTimestamp - PhotonNetwork.ServerTimestamp) / 1000f);
        } else {
            yield return new WaitForSeconds(3f);
        }

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

        playerCount = allPlayers.Count;
        started = true;

        if (!spectating) {
            foreach (PlayerController controllers in allPlayers)
                if (controllers)
                    controllers.gameObject.SetActive(false);

            yield return new WaitForSeconds(3.5f);

            sfx.PlayOneShot(Enums.Sounds.UI_StartGame.GetClip());

            if (PhotonNetwork.IsMasterClient)
                foreach (EnemySpawnpoint point in FindObjectsOfType<EnemySpawnpoint>())
                    point.AttemptSpawning();


            localPlayer.GetComponent<PlayerController>().OnGameStart();
        } else {
            foreach (PlayerController player in allPlayers)
                player.sfx.enabled = true;
        }

        startServerTime = startTimestamp + 3500;
        foreach (var wfgs in FindObjectsOfType<WaitForGameStart>())
            wfgs.AttemptExecute();

        try {
            ScoreboardUpdater.instance.Populate(allPlayers);
        } catch { }

        yield return new WaitForSeconds(1f);

        musicEnabled = true;
        Utils.GetCustomProperty(Enums.NetRoomProperties.Time, out timedGameDuration);

        startRealTime = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (timedGameDuration > 0) {
            endServerTime = startTimestamp + 4500 + timedGameDuration * 1000;
            endRealTime = startRealTime + 4500 + timedGameDuration * 1000;
        }

        GlobalController.Instance.discordController.UpdateActivity();

        if (canvas)
            SceneManager.UnloadSceneAsync("Loading");
    }

    IEnumerator EndGame(Player winner) {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new() { [Enums.NetRoomProperties.GameStarted] = false });
        gameover = true;
        music.Stop();
        music.Stop();
        GameObject text = GameObject.FindWithTag("wintext");
        text.GetComponent<TMP_Text>().text = winner != null ? $"{ winner.NickName } Wins!" : "It's a draw!";

        yield return new WaitForSecondsRealtime(1);
        text.GetComponent<Animator>().SetTrigger("start");

        AudioMixer mixer = music.outputAudioMixerGroup.audioMixer;
        mixer.SetFloat("MusicSpeed", 1f);
        mixer.SetFloat("MusicPitch", 1f);

        bool win = winner != null && winner.IsLocal;
        music.PlayOneShot((win ? Enums.Sounds.UI_Match_Win : Enums.Sounds.UI_Match_Lose).GetClip());
        //TOOD: make a results screen?

        yield return new WaitForSecondsRealtime(4);
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.DestroyAll();
        SceneManager.LoadScene("MainMenu");
    }

    public void Update() {
        if (gameover)
            return;

        if (endServerTime != -1) {
            float timeRemaining = (endServerTime - PhotonNetwork.ServerTimestamp) / 1000f;

            if (timeRemaining > 0 && gameover != true) {
                timeRemaining -= Time.deltaTime;
                //play hurry sound if time < 10 OR less than 10%
                if (hurryup != true && (timeRemaining <= 10 || timeRemaining < (timedGameDuration * 0.2f))) {
                    hurryup = true;
                    sfx.PlayOneShot(Enums.Sounds.UI_HurryUp.GetClip());
                }
                if (timeRemaining - Time.deltaTime <= 0) {
                    CheckForWinner();
                }
            }
        }

        if (started) {
            bool allNull = true;
            foreach (PlayerController controller in allPlayers) {
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

        if (PhotonNetwork.IsMasterClient) {
            int players = 0;
            foreach (var player in PhotonNetwork.CurrentRoom.Players) {
                Utils.GetCustomProperty(Enums.NetPlayerProperties.Spectator, out bool spectating, player.Value.CustomProperties);
                if (!spectating)
                    players++;
            }

            if (!loaded && loadedPlayers.Count >= players) {
                RaiseEventOptions options = new() { CachingOption = EventCaching.AddToRoomCacheGlobal, Receivers = ReceiverGroup.All };
                SendAndExecuteEvent(Enums.NetEventIds.AllFinishedLoading, PhotonNetwork.ServerTimestamp + ((players-1) * 250) + 1000, SendOptions.SendReliable, options);
                loaded = true;
            }
        }

        //TODO: change to coroutine?
        if (!currentStar) {
            if (PhotonNetwork.IsMasterClient) {
                if ((spawnStarCount -= Time.deltaTime) <= 0) {
                    if (remainingSpawns.Count <= 0)
                        remainingSpawns.AddRange(starSpawns);

                    int index = Random.Range(0, remainingSpawns.Count);
                    Vector3 spawnPos = remainingSpawns[index].transform.position;
                    //Check for people camping spawn
                    foreach (var hit in Physics2D.OverlapCircleAll(spawnPos, 4)) {
                        if (hit.gameObject.CompareTag("Player")) {
                            //cant spawn here
                            remainingSpawns.RemoveAt(index);
                            return;
                        }
                    }

                    currentStar = PhotonNetwork.InstantiateRoomObject("Prefabs/BigStar", spawnPos, Quaternion.identity);
                    remainingSpawns.RemoveAt(index);
                    spawnStarCount = 10.4f - (PhotonNetwork.CurrentRoom.PlayerCount / 5f);
                }
            } else {
                currentStar = GameObject.FindGameObjectWithTag("bigstar");
            }
        }
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
        foreach (var player in allPlayers) {
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

    void HandleMusic() {
        bool invincible = false;
        bool mega = false;
        bool speedup = false;

        foreach (var player in allPlayers) {
            if (!player)
                continue;

            if (player.state == Enums.PowerupState.MegaMushroom && player.giantTimer != 15)
                mega = true;
            if (player.invincible > 0)
                invincible = true;
            if ((player.stars + 1f) / starRequirement >= 0.95f || hurryup != false)
                speedup = true;
        }

        if (mega) {
            PlaySong(Enums.MusicState.MegaMushroom, megaMushroomMusic);
        } else if (invincible) {
            PlaySong(Enums.MusicState.Starman, invincibleMusic);
        } else {
            PlaySong(Enums.MusicState.Normal, mainMusic);
        }

        AudioMixer mixer = music.outputAudioMixerGroup.audioMixer;
        if (speedup) {
            mixer.SetFloat("MusicSpeed", 1.25f);
            mixer.SetFloat("MusicPitch", 1f / 1.25f);
        } else {
            mixer.SetFloat("MusicSpeed", 1f);
            mixer.SetFloat("MusicPitch", 1f);
        }
    }

    public void ResetTiles() {
        tilemap.SetTilesBlock(origin, originalTiles);

        foreach (GameObject coin in GameObject.FindGameObjectsWithTag("coin")) {
            coin.GetComponent<SpriteRenderer>().enabled = true;
            coin.GetComponent<BoxCollider2D>().enabled = true;
        }

        if (!PhotonNetwork.IsMasterClient)
            return;
        foreach (EnemySpawnpoint point in enemySpawnpoints)
            point.AttemptSpawning();
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
            pausePanel.SetActive(false);
            hostExitUI.SetActive(true);
            EventSystem.current.SetSelectedGameObject(hostExitButton);
            return;
        }

        Quit();
    }

    public void HostEndMatch() {
        Pause();
        PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.EndGame, null, NetworkUtils.EventAll, SendOptions.SendReliable);
    }

    public void Quit() {
        PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene("MainMenu");
    }

    public void HostQuitCancel() {
        pausePanel.SetActive(true);
        hostExitUI.SetActive(false);
        EventSystem.current.SetSelectedGameObject(pauseButton);
    }


    public float GetLevelMiddleX() {
        return (GetLevelMaxX() + GetLevelMinX()) / 2;
    }
    public float GetLevelMinX() {
        return (levelMinTileX * tilemap.transform.localScale.x) + tilemap.transform.position.x;
    }
    public float GetLevelMinY() {
        return (levelMinTileY * tilemap.transform.localScale.y) + tilemap.transform.position.y;
    }
    public float GetLevelMaxX() {
        return ((levelMinTileX + levelWidthTile) * tilemap.transform.localScale.x) + tilemap.transform.position.x;
    }
    public float GetLevelMaxY() {
        return ((levelMinTileY + levelHeightTile) * tilemap.transform.localScale.y) + tilemap.transform.position.y;
    }


    public float size = 1.39f, ySize = 0.8f;
    public Vector3 GetSpawnpoint(int playerIndex, int players = -1) {
        if (players <= -1)
            players = PhotonNetwork.CurrentRoom.PlayerCount;
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
