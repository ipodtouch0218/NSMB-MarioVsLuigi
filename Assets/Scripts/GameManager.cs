using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class GameManager : NetworkBehaviour {

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

    //---Networked Variables
    [Networked] private TickTimer BigStarRespawnTimer { get; set; }
    [Networked] public TickTimer GameStartTimer { get; set; }
    [Networked] public TickTimer GameEndTimer { get; set; }
    [Networked, Capacity(10)] public NetworkLinkedList<PlayerController> AlivePlayers => default;
    [Networked] public int GameStartTick { get; set; } = -1;
    [Networked] public int RealPlayerCount { get; set; }
    [Networked] public NetworkBool IsMusicEnabled { get; set; }

    //---Serialized Variables
    [SerializeField] private MusicData mainMusic, invincibleMusic, megaMushroomMusic;
    [SerializeField] public int levelMinTileX, levelMinTileY, levelWidthTile, levelHeightTile;
    [SerializeField] public float cameraMinY, cameraHeightY, cameraMinX = -1000, cameraMaxX = 1000;
    [SerializeField] public bool loopingLevel = true;
    [SerializeField] public Vector3 spawnpoint;
    [SerializeField] private GameObject pauseUI, pausePanel, pauseButton, hostExitUI, hostExitButton;
    [SerializeField, ColorUsage(false)] public Color levelUIColor = new(24, 178, 170);
    [SerializeField] public Tilemap tilemap;
    [SerializeField] public bool spawnBigPowerups = true, spawnVerticalPowerups = true;
    [SerializeField] public string levelDesigner = "", richPresenceId = "", levelName = "Unknown";

    //---Private Variables
    private TickTimer StartMusicTimer { get; set; }


    public Canvas nametagCanvas;
    public GameObject nametagPrefab;

    //Audio
    public Enums.MusicState? musicState = null;

    public PlayerController localPlayer;

    public bool paused, loaded, gameover;
    private bool hurryUpSoundPlayed;

    //---Properties
    public NetworkRNG Random { get; set; }

    //---Public Variables
    public SingleParticleManager particleManager;
    public TeamManager teamManager = new();
    public long gameStartTimestamp, gameEndTimestamp;

    //---Private Variables
    private readonly List<GameObject> activeStarSpawns = new();
    private EnemySpawnpoint[] enemySpawns;
    private FloatingCoin[] coins;
    private GameObject[] starSpawns;
    private TileBase[] originalTiles;
    private BoundsInt originalTilesOrigin;

    //---Components
    public SpectationManager spectationManager;
    private LoopingMusic loopMusic;
    public AudioSource music, sfx;


    // EVENT CALLBACK
    public void SendAndExecuteEvent(object eventId, object parameters, object sendOption, object eventOptions = null) {
        //if (eventOptions == null)
        //    eventOptions = NetworkUtils.EventOthers;
        //
        //HandleEvent((byte) eventId, parameters, PhotonNetwork.LocalPlayer, null);
        //PhotonNetwork.RaiseEvent((byte) eventId, parameters, eventOptions, sendOption);
    }
    /*
        public void HandleEvent(byte eventId, object customData, Player sender, ParameterDictionary parameters) {
            object[] data = customData as object[];

            //Debug.Log($"id:{eventId} sender:{sender} master:{sender?.IsMasterClient ?? false}");
            switch (eventId) {
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
            case (byte) Enums.NetEventIds.SyncTilemap: {
                if (!(sender?.IsMasterClient ?? false))
                    return;

                Hashtable changes = (Hashtable) customData;
                //Debug.Log($"SyncTilemap by {sender?.NickName} ({sender?.UserId}): {changes}");
                Utils.ApplyTilemapChanges(originalTiles, originalTilesOrigin, tilemap, changes);
                break;
            }
            case (byte) Enums.NetEventIds.BumpTile: {

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

            }
            }
        }
    */


    public void CreateBlockBump(int tileX, int tileY, bool downwards, string newTile, NetworkPrefabRef? spawnPrefab, bool spawnCoin, Vector2 spawnOffset = default, bool setAndBump = false) {

        Vector3Int loc = new(tileX, tileY, 0);

        if (tilemap.GetTile(loc) == null) {
            if (setAndBump)
                return;

            tilemap.SetTile(loc, (TileBase) Resources.Load("Tilemaps/Tiles/" + newTile));
        }

        Sprite sprite = tilemap.GetSprite(loc);
        Vector3 spawnLocation = Utils.TilemapToWorldPosition(loc) + Vector3.one * 0.25f;

        Runner.Spawn(PrefabList.Instance.Obj_BlockBump, spawnLocation, onBeforeSpawned: (runner, obj) => {
            obj.GetComponentInChildren<BlockBump>().OnBeforeSpawned(loc, newTile, spawnPrefab, downwards, spawnCoin, spawnOffset);
        });

        tilemap.SetTile(loc, null);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_SetTile(int x, int y, string tilename) {
        Vector3Int loc = new(x, y, 0);

        TileBase tile = Utils.GetTileFromCache(tilename);
        tilemap.SetTile(loc, tile);
    }

    public void BulkModifyTilemap(Vector3Int tileOrigin, Vector2Int tileDimensions, string[] tiles) {
        TileBase[] tileObjects = new TileBase[tiles.Length];
        for (int i = 0; i < tiles.Length; i++) {
            string tile = tiles[i];
            if (tile == "")
                continue;

            tileObjects[i] = (TileBase) Resources.Load("Tilemaps/Tiles/" + tile);
        }

        tilemap.SetTilesBlock(new BoundsInt(tileOrigin.x, tileOrigin.y, 0, tileDimensions.x, tileDimensions.y, 1), tileObjects);
    }

    public void SpawnResizableParticle(Vector2 pos, bool right, bool flip, Vector2 size, GameObject prefab) {
        GameObject particle = Instantiate(prefab, pos, Quaternion.Euler(0, 0, flip ? 180 : 0));

        SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();
        sr.size = size;

        Rigidbody2D body = particle.GetComponent<Rigidbody2D>();
        body.velocity = new Vector2(right ? 7 : -7, 6);
        body.angularVelocity = right ^ flip ? -300 : 300;

        particle.transform.position += new Vector3(sr.size.x / 4f, size.y / 4f * (flip ? -1 : 1));
    }

    // MATCHMAKING CALLBACKS
    // ROOM CALLBACKS

    public void OnPlayerEnteredRoom(PlayerRef player) {
        //Spectator joined. Sync the room state.

        //TODO:

        ////SYNCHRONIZE TILEMAPS
        //if (PhotonNetwork.IsMasterClient) {
        //    Hashtable changes = Utils.GetTilemapChanges(originalTiles, origin, tilemap);
        //    RaiseEventOptions options = new() { CachingOption = EventCaching.DoNotCache, TargetActors = new int[] { player.ActorNumber } };
        //    PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.SyncTilemap, changes, options, SendOptions.SendReliable);

        //}
    }
    public void OnPlayerLeftRoom(PlayerRef player) {
        //TODO: player disconnect message

        //nonSpectatingPlayers = PhotonNetwork.CurrentRoom.Players.Values.Where(pl => !pl.IsSpectator()).ToHashSet();
        //CheckIfAllLoaded();

        //if (musicEnabled && FindObjectsOfType<PlayerController>().Length <= 0) {
        //    //all players left.
        //    if (PhotonNetwork.IsMasterClient)
        //        PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.EndGame, null, NetworkUtils.EventAll, SendOptions.SendReliable);
        //}
    }

    //Register pause event
    public void OnEnable() {
        InputSystem.controls.UI.Pause.performed += OnPause;
        NetworkHandler.OnShutdown += OnShutdown;
        NetworkHandler.OnPlayerLeft += OnPlayerLeft;
    }

    public void OnDisable() {
        InputSystem.controls.UI.Pause.performed -= OnPause;
        NetworkHandler.OnShutdown -= OnShutdown;
        NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
    }

    public void Awake() {
        Instance = this;
        spectationManager = GetComponent<SpectationManager>();
        loopMusic = GetComponent<LoopingMusic>();
        particleManager = GetComponentInChildren<SingleParticleManager>();

        //Make UI color translucent
        levelUIColor.a = .7f;
    }

    public void Start() {
        //spawning in editor
        if (!NetworkHandler.Runner.SessionInfo.IsValid) {
            //uhhh

            _ = NetworkHandler.CreateRoom(new() {
                Scene = SceneManager.GetActiveScene().buildIndex,
            }, GameMode.Single);
        }
    }

    public override void Spawned() {
        //by default, spectate. when we get assigned a player object, we disable it there.
        spectationManager.Spectating = true;

        //Load + enable player controls
        InputSystem.controls.LoadBindingOverridesFromJson(GlobalController.Instance.controlsJson);
        Runner.ProvideInput = true;

        //Setup respawning tilemap
        originalTilesOrigin = new(levelMinTileX, levelMinTileY, 0, levelWidthTile, levelHeightTile, 1);
        originalTiles = tilemap.GetTilesBlock(originalTilesOrigin);
        tilemap.RefreshAllTiles();

        //Find objects in the scene
        starSpawns = GameObject.FindGameObjectsWithTag("StarSpawn");
        coins = FindObjectsOfType<FloatingCoin>();
        enemySpawns = FindObjectsOfType<EnemySpawnpoint>();

        //create player instances
        if (Runner.IsServer) {
            foreach (PlayerRef player in Runner.ActivePlayers) {
                PlayerData data = player.GetPlayerData(Runner);
                if (data.IsCurrentlySpectating)
                    continue;

                Runner.Spawn(data.GetCharacterData().prefab, spawnpoint, inputAuthority: player);
                RealPlayerCount++;
            }
        }

        //finished loading
        if (Runner.IsSinglePlayer) {
            CheckIfAllPlayersLoaded();
        } else {
            //tell our host that we're done loading
            PlayerData data;
            if (data = NetworkHandler.Runner.GetLocalPlayerData())
                data.Rpc_FinishedLoading();
        }

        Camera.main.transform.position = spawnpoint;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_ResetTilemap() {
        tilemap.SetTilesBlock(originalTilesOrigin, originalTiles);

        foreach (FloatingCoin coin in coins)
            coin.ResetCoin();

        foreach (EnemySpawnpoint point in enemySpawns)
            point.AttemptSpawning();

        BigStarRespawnTimer = TickTimer.CreateFromSeconds(Runner, 10.4f - RealPlayerCount / 5f);
    }

    public void OnPlayerLoaded() {
        CheckIfAllPlayersLoaded();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        CheckForWinner();
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
        GlobalController.Instance.disconnectCause = shutdownReason;
        SceneManager.LoadScene(0);
    }

    private void CheckIfAllPlayersLoaded() {
        if (!NetworkHandler.Runner.IsServer || loaded)
            return;

        foreach (PlayerRef player in NetworkHandler.Runner.ActivePlayers) {
            PlayerData data = player.GetPlayerData(NetworkHandler.Runner);

            if (data == null || data.IsCurrentlySpectating)
                continue;

            if (!data.IsLoaded)
                return;
        }

        loaded = true;
        SceneManager.SetActiveScene(gameObject.scene);
        GameStartTimer = TickTimer.CreateFromSeconds(NetworkHandler.Runner, 3.7f);

        if (Runner.IsServer)
            Rpc_LoadingComplete();

        foreach (PlayerRef player in NetworkHandler.Runner.ActivePlayers)
            player.GetPlayerData(NetworkHandler.Runner).IsLoaded = false;
    }

    //TODO: invokeresim?
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_EndGame(int team) {

        //TODO: don't use a coroutine.
        StartCoroutine(EndGame(team));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_LoadingComplete() {

        //Populate scoreboard
        ScoreboardUpdater.Instance.Populate(AlivePlayers);
        if (Settings.Instance.scoreboardAlways)
            ScoreboardUpdater.Instance.SetEnabled();

        GlobalController.Instance.loadingCanvas.EndLoading();
    }

    private void StartGame() {

        //Spawn players
        foreach (PlayerController player in AlivePlayers)
            player.PreRespawn();

        //Play start sfx
        if (Runner.IsForward)
            sfx.PlayOneShot(Enums.Sounds.UI_StartGame);

        //Respawn enemies
        foreach (EnemySpawnpoint point in enemySpawns)
            point.AttemptSpawning();

        //Start timer
        int timer = LobbyData.Instance.Timer;
        if (timer > 0)
            GameEndTimer = TickTimer.CreateFromSeconds(Runner, timer);

        //Start some things that should be booted
        foreach (var wfgs in FindObjectsOfType<WaitForGameStart>())
            wfgs.AttemptExecute();

        //Big star
        SpawnBigStar();

        GameStartTick = Runner.Simulation.Tick.Raw;

        gameStartTimestamp = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        gameEndTimestamp = (timer > 0) ? gameStartTimestamp + timer * 1000 : 0;

        GlobalController.Instance.DiscordController.UpdateActivity();

        StartMusicTimer = TickTimer.CreateFromSeconds(Runner, 1.3f);
    }

    private static readonly string[] teams = { "Red Team", "Green Team", "Blue Team", "Yellow Team", "Magenta Team" };
    private IEnumerator EndGame(int winningTeam) {
        //TODO:
        //PhotonNetwork.CurrentRoom.SetCustomProperties(new() { [Enums.NetRoomProperties.GameStarted] = false });
        gameover = true;
        LobbyData.Instance.SetGameStarted(false);

        music.Stop();
        GameObject text = GameObject.FindWithTag("wintext");
        TMP_Text tmpText = text.GetComponent<TMP_Text>();

        if (winningTeam == -1) {
            tmpText.text = "It's a draw...";
        } else {
            if (LobbyData.Instance.Teams) {
                tmpText.text = teams[winningTeam] + " Wins!";
            } else {
                tmpText.text = teamManager.GetTeamMembers(winningTeam).First().data.GetNickname() + " Wins!";
            }
        }

        yield return new WaitForSecondsRealtime(1);
        text.GetComponent<Animator>().SetTrigger("start");

        AudioMixer mixer = music.outputAudioMixerGroup.audioMixer;
        mixer.SetFloat("MusicSpeed", 1f);
        mixer.SetFloat("MusicPitch", 1f);

        bool draw = winningTeam == -1;
        bool win = !draw && winningTeam == Runner.GetLocalPlayerData().Team;
        int secondsUntilMenu = draw ? 5 : 4;

        if (draw)
            music.PlayOneShot(Enums.Sounds.UI_Match_Draw);
        else if (win)
            music.PlayOneShot(Enums.Sounds.UI_Match_Win);
        else
            music.PlayOneShot(Enums.Sounds.UI_Match_Lose);

        //TOOD: make a results screen?

        if (Runner.IsServer) {
            //handle player states
            foreach (PlayerRef player in Runner.ActivePlayers) {
                PlayerData data = player.GetPlayerData(Runner);

                //set loading state = false
                data.IsLoaded = false;

                //disable spectating
                data.IsCurrentlySpectating = false;

                //move non-teams into valid teams range
                data.Team = (sbyte) ((data.Object.InputAuthority + 1) % 5);
            }
        }

        yield return new WaitForSecondsRealtime(secondsUntilMenu);
        Runner.SetActiveScene(0);
    }

    private void SpawnBigStar() {

        for (int i = 0; i < starSpawns.Length; i++) {
            if (activeStarSpawns.Count <= 0)
                activeStarSpawns.AddRange(starSpawns);

            int index = Random.RangeExclusive(0, activeStarSpawns.Count);
            Vector3 spawnPos = activeStarSpawns[index].transform.position;

            if (Runner.GetPhysicsScene2D().OverlapCircle(spawnPos, 4, Layers.MaskOnlyPlayers)) {
                //a player is too close to the spawn
                activeStarSpawns.RemoveAt(index);
                continue;
            }

            //Valid spawn
            Runner.Spawn(PrefabList.Instance.Obj_BigStar, spawnPos, onBeforeSpawned: (runner, obj) => {
                obj.GetComponent<StarBouncer>().OnBeforeSpawned(0, true, false);
            });
            activeStarSpawns.RemoveAt(index);
            return;
        }

        //no star could spawn. wait a second and try again...
        BigStarRespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
    }

    public override void FixedUpdateNetwork() {

        //check if all players left.
        //if (GameStartTick != -1 && musicEnabled) {
        //    bool allNull = true;
        //    foreach (PlayerController controller in players) {
        //        if (controller) {
        //            allNull = false;
        //            break;
        //        }
        //    }
        //    if (spectationManager.Spectating && allNull) {
        //        StartCoroutine(EndGame(PlayerRef.None));
        //        return;
        //    }
        //}

        if (gameover)
            return;

        Random = new(Runner.Simulation.Tick);

        if (IsMusicEnabled)
            HandleMusic();

        if (BigStarRespawnTimer.Expired(Runner)) {
            BigStarRespawnTimer = TickTimer.None;
            SpawnBigStar();
        }

        if (GameStartTimer.Expired(Runner)) {
            GameStartTimer = TickTimer.None;
            StartGame();
        }

        if (StartMusicTimer.Expired(Runner)) {
            StartMusicTimer = TickTimer.None;
            IsMusicEnabled = true;
        }

        if (GameEndTimer.IsRunning) {
            if (GameEndTimer.Expired(Runner)) {
                CheckForWinner();

                //time end sfx
                sfx.PlayOneShot(Enums.Sounds.UI_Countdown_1);
                GameEndTimer = TickTimer.None;
                return;
            }

            int tickrate = Runner.Config.Simulation.TickRate;
            int remainingTicks = GameEndTimer.RemainingTicks(Runner) ?? 0;

            if (!hurryUpSoundPlayed && remainingTicks < 60 * tickrate) {
                //60 second warning
                hurryUpSoundPlayed = true;
                sfx.PlayOneShot(Enums.Sounds.UI_HurryUp);
            } else if (remainingTicks < (10 * tickrate)) {
                //10 second "dings"
                if (remainingTicks % tickrate == 0)
                    sfx.PlayOneShot(Enums.Sounds.UI_Countdown_0);

                //at 3 seconds, double speed
                if (remainingTicks < (3 * tickrate) && remainingTicks % (tickrate / 2) == 0)
                    sfx.PlayOneShot(Enums.Sounds.UI_Countdown_0);
            }
        }
    }

    public void CreateNametag(PlayerController controller) {
        GameObject nametag = Instantiate(nametagPrefab, nametagPrefab.transform.parent);
        nametag.GetComponent<UserNametag>().parent = controller;
        nametag.SetActive(true);
    }

    public void CheckForWinner() {
        if (gameover || !Runner.IsServer)
            return;

        int requiredStars = LobbyData.Instance.StarRequirement;
        bool starGame = requiredStars != -1;

        bool hasFirstPlace = teamManager.HasFirstPlaceTeam(out int firstPlaceTeam, out int firstPlaceStars);
        int aliveTeams = teamManager.GetAliveTeamCount();
        bool timeUp = GameEndTimer.Expired(Runner);

        if (aliveTeams == 0) {
            //all teams dead, draw?
            Rpc_EndGame(PlayerRef.None);
            return;
        }

        if (aliveTeams == 1 && RealPlayerCount > 1) {
            //one team left alive (and it's not a solo game), they win.
            Rpc_EndGame(firstPlaceTeam);
            return;
        }

        if (hasFirstPlace) {
            //we have a team that's clearly in first...
            if (starGame && firstPlaceStars >= requiredStars) {
                //and they have enough stars.
                Rpc_EndGame(firstPlaceTeam);
                return;
            }
            //they don't have enough stars. wait 'till later
        }

        if (timeUp) {
            //ran out of time, instantly end if DrawOnTimeUp is set
            if (LobbyData.Instance.DrawOnTimeUp) {
                //no one wins
                Rpc_EndGame(PlayerRef.None);
                return;
            }
            //keep playing
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

        foreach (var player in AlivePlayers) {
            if (!player)
                continue;

            mega |= player.State == Enums.PowerupState.MegaMushroom && player.GiantStartTimer.ExpiredOrNotRunning(Runner);
            invincible |= player.IsStarmanInvincible;
        }

        speedup |= teamManager.GetFirstPlaceStars() + 1 >= LobbyData.Instance.StarRequirement;
        speedup |= AlivePlayers.All(pl => !pl || pl.Lives == 1 || pl.Lives == 0);

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
        if (gameover || !IsMusicEnabled)
            return;

        paused = !paused;
        sfx.PlayOneShot(Enums.Sounds.UI_Pause);
        pauseUI.SetActive(paused);
        pausePanel.SetActive(true);
        hostExitUI.SetActive(false);
        EventSystem.current.SetSelectedGameObject(pauseButton);
    }

    public void AttemptQuit() {

        if (Runner.IsServer) {
            sfx.PlayOneShot(Enums.Sounds.UI_Decide);
            pausePanel.SetActive(false);
            hostExitUI.SetActive(true);
            EventSystem.current.SetSelectedGameObject(hostExitButton);
            return;
        }

        Quit();
    }

    public void HostEndMatch() {
        pauseUI.SetActive(false);
        sfx.PlayOneShot(Enums.Sounds.UI_Decide);
        Rpc_EndGame(PlayerRef.None);
    }

    public void Quit() {
        sfx.PlayOneShot(Enums.Sounds.UI_Decide);
        Runner.Shutdown();
        SceneManager.LoadScene("MainMenu");
    }

    public void HostQuitCancel() {
        pausePanel.SetActive(true);
        hostExitUI.SetActive(false);
        sfx.PlayOneShot(Enums.Sounds.UI_Back);
        EventSystem.current.SetSelectedGameObject(pauseButton);
    }

    //lazy mofo
    private float? middleX, minX, minY, maxX, maxY;
    public float GetLevelMiddleX() {
        middleX ??= (GetLevelMaxX() + GetLevelMinX()) / 2;
        return (float) middleX;
    }
    public float GetLevelMinX() {
        minX ??= (levelMinTileX * tilemap.transform.localScale.x) + tilemap.transform.position.x;
        return (float) minX;
    }
    public float GetLevelMinY() {
        minY ??= (levelMinTileY * tilemap.transform.localScale.y) + tilemap.transform.position.y;
        return (float) minY;
    }
    public float GetLevelMaxX() {
        maxX ??= ((levelMinTileX + levelWidthTile) * tilemap.transform.localScale.x) + tilemap.transform.position.x;
        return (float) maxX;
    }
    public float GetLevelMaxY() {
        maxY ??=  ((levelMinTileY + levelHeightTile) * tilemap.transform.localScale.y) + tilemap.transform.position.y;
        return (float) maxY;
    }


    public float size = 1.39f, ySize = 0.8f;
    public Vector3 GetSpawnpoint(int playerIndex, int players = -1) {
        if (players <= -1)
            players = RealPlayerCount;
        if (players == 0)
            players = 1;

        Debug.Log($"getting spawn, playerindex: {playerIndex}, players: {players}");

        float comp = (float) playerIndex/players * 2 * Mathf.PI + (Mathf.PI/2f) + (Mathf.PI/(2*players));
        float scale = (2-(players+1f)/players) * size;

        Vector3 spawn = spawnpoint + new Vector3(Mathf.Sin(comp) * scale, Mathf.Cos(comp) * (players > 2 ? scale * ySize : 0), 0);
        Utils.WrapWorldLocation(ref spawn);

        return spawn;
    }

    [SerializeField, Range(1,10)] private int playersToVisualize = 10;
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
