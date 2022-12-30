using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

            if (SceneManager.GetActiveScene().buildIndex >= 2 || SceneManager.GetActiveScene().buildIndex < 0)
                _instance = FindObjectOfType<GameManager>();

            return _instance;
        }
        private set => _instance = value;
    }

    //---Networked Variables
    [Networked] public TickTimer BigStarRespawnTimer { get; set; }
    [Networked] public TickTimer GameStartTimer { get; set; }
    [Networked] public TickTimer GameEndTimer { get; set; }
    [Networked, Capacity(10)] public NetworkLinkedList<PlayerController> AlivePlayers => default;
    [Networked, Capacity(60)] public NetworkLinkedList<FireballMover> PooledFireballs => default;
    [Networked] public float GameStartTime { get; set; } = -1;
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
    [SerializeField] public GameObject objectPoolParent;

    //---Properties
    public NetworkRNG Random { get; set; }

    private float? levelWidth, levelHeight, middleX, minX, minY, maxX, maxY;
    public float LevelWidth => levelWidth ??= levelWidthTile * tilemap.transform.localScale.x;
    public float LevelHeight => levelHeight ??= levelHeightTile * tilemap.transform.localScale.x;
    public float LevelMinX => minX ??= (levelMinTileX * tilemap.transform.localScale.x) + tilemap.transform.position.x;
    public float LevelMaxX => maxX ??= ((levelMinTileX + levelWidthTile) * tilemap.transform.localScale.x) + tilemap.transform.position.x;
    public float LevelMiddleX => middleX ??= LevelMinX + (LevelWidth * 0.5f);
    public float LevelMinY => minY ??= (levelMinTileY * tilemap.transform.localScale.y) + tilemap.transform.position.y;
    public float LevelMaxY => maxY ??= ((levelMinTileY + levelHeightTile) * tilemap.transform.localScale.y) + tilemap.transform.position.y;

    //---Public Variables
    public readonly HashSet<NetworkObject> networkObjects = new();
    public SingleParticleManager particleManager;
    public TeamManager teamManager = new();
    public GameEventRpcs rpcs;
    public Canvas nametagCanvas;
    public GameObject nametagPrefab;
    public PlayerController localPlayer;
    public long gameStartTimestamp, gameEndTimestamp;
    public bool paused, loaded, gameover;

    public EnemySpawnpoint[] enemySpawns;
    public TileBase[] originalTiles;
    public BoundsInt originalTilesOrigin;

    //---Private Variables
    private TickTimer StartMusicTimer { get; set; }
    private readonly List<GameObject> activeStarSpawns = new();
    private GameObject[] starSpawns;

    //Audio
    public Enums.MusicState? musicState = null;
    private bool hurryUpSoundPlayed;

    //---Components
    public SpectationManager spectationManager;
    private LoopingMusic loopMusic;
    public AudioSource music, sfx;

    public void BulkModifyTilemap(Vector3Int tileOrigin, Vector2Int tileDimensions, string[] tiles) {
        TileBase[] tileObjects = new TileBase[tiles.Length];
        for (int i = 0; i < tiles.Length; i++) {
            string tile = tiles[i];
            if (tile == "")
                continue;

            tileObjects[i] = Utils.GetCacheTile(tile);
        }

        tilemap.SetTilesBlock(new BoundsInt(tileOrigin.x, tileOrigin.y, 0, tileDimensions.x, tileDimensions.y, 1), tileObjects);
    }

    //TODO: convert to RPC...?
    public void SpawnResizableParticle(Vector2 pos, bool right, bool flip, Vector2 size, GameObject prefab) {
        GameObject particle = Instantiate(prefab, pos, Quaternion.Euler(0, 0, flip ? 180 : 0));

        SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();
        sr.size = size;

        Rigidbody2D body = particle.GetComponent<Rigidbody2D>();
        body.velocity = new Vector2(right ? 7 : -7, 6);
        body.angularVelocity = right ^ flip ? -300 : 300;

        particle.transform.position += new Vector3(sr.size.x * 0.25f, size.y * 0.25f * (flip ? -1 : 1));
    }

    //Register pause event
    public void OnEnable() {
        ControlSystem.controls.UI.Pause.performed += OnPause;
        NetworkHandler.OnShutdown +=     OnShutdown;
        NetworkHandler.OnPlayerJoined += OnPlayerJoined;
        NetworkHandler.OnPlayerLeft +=   OnPlayerLeft;
    }

    public void OnDisable() {
        ControlSystem.controls.UI.Pause.performed -= OnPause;
        NetworkHandler.OnShutdown -=     OnShutdown;
        NetworkHandler.OnPlayerJoined -= OnPlayerJoined;
        NetworkHandler.OnPlayerLeft -=   OnPlayerLeft;
    }

    public void OnValidate() {
        levelWidth = levelHeight = middleX = minX = minY = maxX = maxY = null;
    }

    public void Awake() {
        Instance = this;
        spectationManager = GetComponent<SpectationManager>();
        loopMusic = GetComponent<LoopingMusic>();
        particleManager = GetComponentInChildren<SingleParticleManager>();
        rpcs = GetComponent<GameEventRpcs>();

        //tiles
        originalTilesOrigin = new(levelMinTileX, levelMinTileY, 0, levelWidthTile, levelHeightTile, 1);
        originalTiles = tilemap.GetTilesBlock(originalTilesOrigin);

        //Make UI color translucent
        levelUIColor.a = .7f;
    }

    public void Start() {
        //spawning in editor
        if (!NetworkHandler.Runner.SessionInfo.IsValid) {
            //join a singleplayer room if we're not in one
            _ = NetworkHandler.CreateRoom(new() {
                Scene = SceneManager.GetActiveScene().buildIndex,
            }, GameMode.Single);
        }
    }

    public override void Spawned() {
        //by default, spectate. when we get assigned a player object, we disable it there.
        spectationManager.Spectating = true;

        //Load + enable player controls
        ControlSystem.controls.LoadBindingOverridesFromJson(GlobalController.Instance.controlsJson);
        Runner.ProvideInput = true;

        //Setup respawning tilemap
        tilemap.RefreshAllTiles();

        //Find objects in the scene
        starSpawns = GameObject.FindGameObjectsWithTag("StarSpawn");
        enemySpawns = FindObjectsOfType<EnemySpawnpoint>();

        if (Runner.IsServer && Runner.IsSinglePlayer) {
            //handle spawning in editor by spawning the room + player objects
            Runner.Spawn(PrefabList.Instance.SessionDataHolder);
            Runner.Spawn(PrefabList.Instance.PlayerDataHolder, inputAuthority: Runner.LocalPlayer);
        }

        //tell our host that we're done loading
        PlayerData localData = Runner.GetLocalPlayerData();
        localData.Rpc_FinishedLoading();
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (!runner.IsServer || !hasState)
            return;

        //remove all network objects.
        foreach (var obj in networkObjects)
            runner.Despawn(obj);
    }

    public void OnPlayerLoaded() {
        CheckIfAllPlayersLoaded();
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
        if (!Runner.IsServer)
            return;

        //send spectating player the current level
        if (Utils.GetTilemapChanges(originalTiles, originalTilesOrigin, tilemap, out TileChangeInfo[] tilePositions, out string[] tileNames)) {
            rpcs.Rpc_UpdateSpectatorTilemap(player, tilePositions, tileNames);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        //Kill player if they are still alive
        if (Object.HasStateAuthority) {
            foreach (PlayerController pl in AlivePlayers) {
                if (pl.Object.InputAuthority == player)
                    pl.Rpc_DisconnectDeath();
            }
        }

        CheckIfAllPlayersLoaded();
        CheckForWinner();
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {
        GlobalController.Instance.disconnectCause = shutdownReason;
        SceneManager.LoadScene(0);
    }

    private void CheckIfAllPlayersLoaded() {
        if (!Runner.IsServer || loaded)
            return;

        if (!Runner.IsSinglePlayer) {
            foreach (PlayerRef player in Runner.ActivePlayers) {
                PlayerData data = player.GetPlayerData(Runner);

                if (data == null || data.IsCurrentlySpectating)
                    continue;

                if (!data.IsLoaded)
                    return;
            }
        }

        loaded = true;
        SceneManager.SetActiveScene(gameObject.scene);
        GameStartTimer = TickTimer.CreateFromSeconds(Runner, Runner.IsSinglePlayer ? 0f : 5.7f);

        //create player instances
        foreach (PlayerRef player in Runner.ActivePlayers) {
            PlayerData data = player.GetPlayerData(Runner);
            if (!data)
                continue;

            data.IsLoaded = false;
            if (data.IsCurrentlySpectating)
                continue;

            Runner.Spawn(data.GetCharacterData().prefab, spawnpoint, inputAuthority: player);
            RealPlayerCount++;
        }

        //create pooled fireball instances (6 per player)
        for (int i = 0; i < RealPlayerCount * 6; i++)
            Runner.Spawn(PrefabList.Instance.Obj_Fireball);

        StartCoroutine(CallLoadingComplete(2));
    }

    private IEnumerator CallLoadingComplete(float seconds) {
        yield return new WaitForSeconds(seconds);
        rpcs.Rpc_LoadingComplete();
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
        int timer = SessionData.Instance.Timer;
        if (timer > 0)
            GameEndTimer = TickTimer.CreateFromSeconds(Runner, timer);

        //Start some things that should be booted
        foreach (var wfgs in FindObjectsOfType<WaitForGameStart>())
            wfgs.AttemptExecute();

        //Big star
        AttemptSpawnBigStar();

        GameStartTime = Runner.SimulationTime;

        gameStartTimestamp = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        gameEndTimestamp = (timer > 0) ? gameStartTimestamp + timer * 1000 : 0;

        GlobalController.Instance.DiscordController.UpdateActivity();

        StartMusicTimer = TickTimer.CreateFromSeconds(Runner, 1.3f);
    }

    internal IEnumerator EndGame(int winningTeam) {
        gameover = true;
        SessionData.Instance.SetGameStarted(false);

        music.Stop();
        GameObject text = GameObject.FindWithTag("wintext");
        TMP_Text tmpText = text.GetComponent<TMP_Text>();

        if (winningTeam == -1) {
            tmpText.text = "It's a draw...";
        } else {
            if (SessionData.Instance.Teams) {
                Team team = ScriptableManager.Instance.teams[winningTeam];
                tmpText.text = team.displayName + " Wins!";
            } else {
                tmpText.text = teamManager.GetTeamMembers(winningTeam).First().data.GetNickname() + " Wins!";
            }
        }

        yield return new WaitForSecondsRealtime(1);

        bool draw = winningTeam == -1;
        bool win = !draw && winningTeam == Runner.GetLocalPlayerData().Team;
        int secondsUntilMenu = draw ? 5 : 4;

        if (draw) {
            music.PlayOneShot(Enums.Sounds.UI_Match_Draw);
            text.GetComponent<Animator>().SetTrigger("startNegative");
        }
        else if (win) {
            music.PlayOneShot(Enums.Sounds.UI_Match_Win);
            text.GetComponent<Animator>().SetTrigger("start");
        }
        else {
            music.PlayOneShot(Enums.Sounds.UI_Match_Lose);
            text.GetComponent<Animator>().SetTrigger("startNegative");
        }

        //TODO: make a results screen?

        if (Runner.IsServer) {
            //handle player states
            foreach (PlayerRef player in Runner.ActivePlayers) {
                PlayerData data = player.GetPlayerData(Runner);

                //set loading state = false
                data.IsLoaded = false;

                //disable spectating
                data.IsCurrentlySpectating = false;

                //move non-teams into valid teams range
                if (SessionData.Instance.Teams)
                    data.Team = (sbyte) Mathf.Clamp(data.Team, 0, ScriptableManager.Instance.teams.Length);
            }
        }

        foreach (PlayerRef player in Runner.ActivePlayers)
            player.GetPlayerData(Runner).IsLoaded = false;

        yield return new WaitForSecondsRealtime(secondsUntilMenu);
        Runner.SetActiveScene(0);
    }

    public override void FixedUpdateNetwork() {

        if (gameover)
            return;

        Random = new(Runner.Simulation.Tick);

        if (IsMusicEnabled)
            HandleMusic();

        if (BigStarRespawnTimer.Expired(Runner)) {
            BigStarRespawnTimer = TickTimer.None;
            AttemptSpawnBigStar();
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

    private void AttemptSpawnBigStar() {

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

        //no star could spawn. wait a few and try again...
        BigStarRespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
    }

    public void CreateNametag(PlayerController controller) {
        GameObject nametag = Instantiate(nametagPrefab, nametagPrefab.transform.parent);
        nametag.GetComponent<UserNametag>().parent = controller;
        nametag.SetActive(true);
    }

    public void CheckForWinner() {
        if (gameover || !Object.HasStateAuthority)
            return;

        int requiredStars = SessionData.Instance.StarRequirement;
        bool starGame = requiredStars != -1;

        bool hasFirstPlace = teamManager.HasFirstPlaceTeam(out int firstPlaceTeam, out int firstPlaceStars);
        int aliveTeams = teamManager.GetAliveTeamCount();
        bool timeUp = GameEndTimer.Expired(Runner);

        if (aliveTeams == 0) {
            //all teams dead, draw?
            rpcs.Rpc_EndGame(PlayerRef.None);
            return;
        }

        if (aliveTeams == 1 && RealPlayerCount > 1) {
            //one team left alive (and it's not a solo game), they win.
            rpcs.Rpc_EndGame(firstPlaceTeam);
            return;
        }

        if (hasFirstPlace) {
            //we have a team that's clearly in first...
            if (starGame && firstPlaceStars >= requiredStars) {
                //and they have enough stars.
                rpcs.Rpc_EndGame(firstPlaceTeam);
                return;
            }
            //they don't have enough stars. wait 'till later
        }

        if (timeUp) {
            //ran out of time, instantly end if DrawOnTimeUp is set
            if (SessionData.Instance.DrawOnTimeUp) {
                //no one wins
                rpcs.Rpc_EndGame(PlayerRef.None);
                return;
            }
            //keep playing
        }
    }

    private void TryChangeSong(Enums.MusicState state, MusicData musicToPlay) {
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

        speedup |= teamManager.GetFirstPlaceStars() + 1 >= SessionData.Instance.StarRequirement;
        speedup |= AlivePlayers.Count <= 2 && AlivePlayers.All(pl => !pl || pl.Lives == 1 || pl.Lives == 0);

        if (mega) {
            TryChangeSong(Enums.MusicState.MegaMushroom, megaMushroomMusic);
        } else if (invincible) {
            TryChangeSong(Enums.MusicState.Starman, invincibleMusic);
        } else {
            TryChangeSong(Enums.MusicState.Normal, mainMusic);
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
        if (Runner.GetLocalPlayerData().IsRoomOwner) {
            //prompt for ending game or leaving
            sfx.PlayOneShot(Enums.Sounds.UI_Decide);
            pausePanel.SetActive(false);
            hostExitUI.SetActive(true);
            EventSystem.current.SetSelectedGameObject(hostExitButton);
            return;
        }

        Quit();
    }

    //---UI Callbacks
    public void HostEndMatch() {
        pauseUI.SetActive(false);
        sfx.PlayOneShot(Enums.Sounds.UI_Decide);
        rpcs.Rpc_EndGame(PlayerRef.None);
    }

    public void Quit() {
        sfx.PlayOneShot(Enums.Sounds.UI_Decide);
        Runner.Shutdown();
    }

    public void HostQuitCancel() {
        pausePanel.SetActive(true);
        hostExitUI.SetActive(false);
        sfx.PlayOneShot(Enums.Sounds.UI_Back);
        EventSystem.current.SetSelectedGameObject(pauseButton);
    }

    //---Helpers
    public float size = 1.39f, ySize = 0.8f;
    public Vector3 GetSpawnpoint(int playerIndex, int players = -1) {
        if (players <= -1)
            players = RealPlayerCount;
        if (players == 0)
            players = 1;

        float comp = (float) playerIndex / players * 2.5f * Mathf.PI + (Mathf.PI / (2 * players));
        float scale = (2 - (players + 1f) / players) * size;

        Vector3 spawn = spawnpoint + new Vector3(Mathf.Sin(comp) * scale, Mathf.Cos(comp) * (players > 2f ? scale * ySize : 0), 0);
        Utils.WrapWorldLocation(ref spawn);
        return spawn;
    }

    //---Debug
#if UNITY_EDITOR
    private static readonly int DebugSpawns = 10;
    private static readonly Color StarSpawnTint = new(1f, 1f, 1f, 0.5f), StarSpawnBox = new(1f, 0.9f, 0.2f, 0.2f);
    private static readonly Vector3 OneFourth = new(0.25f, 0.25f);
    public void OnDrawGizmos() {
        if (!tilemap)
            return;

        for (int i = 0; i < DebugSpawns; i++) {
            Gizmos.color = new Color((float) i / DebugSpawns, 0, 0, 0.75f);
            Gizmos.DrawCube(GetSpawnpoint(i, DebugSpawns) + Vector3.down * 0.25f, Vector2.one * 0.5f);
        }

        Vector3 size = new(LevelWidth, LevelHeight);
        Vector3 origin = new(LevelMinX + (LevelWidth * 0.5f), LevelMinY + (LevelHeight * 0.5f), 1);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(origin, size);

        size = new Vector3(LevelWidth, cameraHeightY);
        origin.y = cameraMinY + (cameraHeightY * 0.5f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(origin, size);

        for (int x = 0; x < levelWidthTile; x++) {
            for (int y = 0; y < levelHeightTile; y++) {
                Vector3Int loc = new(x+levelMinTileX, y+levelMinTileY, 0);
                TileBase tile = tilemap.GetTile(loc);
                if (tile is CoinTile)
                    Gizmos.DrawIcon(Utils.TilemapToWorldPosition(loc, this) + OneFourth, "coin");
                if (tile is PowerupTile)
                    Gizmos.DrawIcon(Utils.TilemapToWorldPosition(loc, this) + OneFourth, "powerup");
            }
        }

        Gizmos.color = StarSpawnBox;
        foreach (GameObject starSpawn in GameObject.FindGameObjectsWithTag("StarSpawn")) {
            Gizmos.DrawCube(starSpawn.transform.position, Vector3.one);
            Gizmos.DrawIcon(starSpawn.transform.position, "star", true, StarSpawnTint);
        }
    }
#endif
}
