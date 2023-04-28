using System;
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
using NSMB.Tiles;
using NSMB.Translation;
using NSMB.Utils;

public class GameManager : NetworkBehaviour {

    private static GameManager _instance;
    public static GameManager Instance {
        get {
            if (_instance)
                return _instance;

            if (SceneManager.GetActiveScene().buildIndex != 0)
                _instance = FindObjectOfType<GameManager>();

            return _instance;
        }
        private set => _instance = value;
    }

    //---Properties
    public NetworkRNG Random { get; set; }

    private float? levelWidth, levelHeight, middleX, minX, minY, maxX, maxY;
    public float LevelWidth   => levelWidth ??= levelWidthTile * tilemap.transform.localScale.x;
    public float LevelHeight  => levelHeight ??= levelHeightTile * tilemap.transform.localScale.x;
    public float LevelMinX    => minX ??= (levelMinTileX * tilemap.transform.localScale.x) + tilemap.transform.position.x;
    public float LevelMaxX    => maxX ??= ((levelMinTileX + levelWidthTile) * tilemap.transform.localScale.x) + tilemap.transform.position.x;
    public float LevelMiddleX => middleX ??= LevelMinX + (LevelWidth * 0.5f);
    public float LevelMinY    => minY ??= (levelMinTileY * tilemap.transform.localScale.y) + tilemap.transform.position.y;
    public float LevelMaxY    => maxY ??= ((levelMinTileY + levelHeightTile) * tilemap.transform.localScale.y) + tilemap.transform.position.y;

    public bool GameEnded => GameState == Enums.GameState.Ended;

    //---Networked Variables
    [Networked] public TickTimer BigStarRespawnTimer { get; set; }
    [Networked] public TickTimer GameStartTimer { get; set; }
    [Networked] public TickTimer GameEndTimer { get; set; }
    [Networked, Capacity(10)] public NetworkLinkedList<PlayerController> AlivePlayers => default;
    [Networked, Capacity(60)] public NetworkLinkedList<FireballMover> PooledFireballs => default;
    [Networked] public float GameStartTime { get; set; } = -1;
    [Networked] public int RealPlayerCount { get; set; }
    [Networked] public NetworkBool IsMusicEnabled { get; set; }
    [Networked] public Enums.GameState GameState { get; set; }

    //---Serialized Variables
    [Header("Music")]
    [SerializeField] private LoopingMusicData mainMusic;
    [SerializeField] private LoopingMusicData invincibleMusic;
    [SerializeField] private LoopingMusicData megaMushroomMusic;

    [Header("Level Configuration")]
    public int levelMinTileX;
    public int levelMinTileY;
    public int levelWidthTile;
    public int levelHeightTile;
    public bool loopingLevel = true, spawnBigPowerups = true, spawnVerticalPowerups = true;
    public string levelDesigner = "", richPresenceId = "", levelName = "Unknown";
    public Vector3 spawnpoint;
    [ColorUsage(false)] public Color levelUIColor = new(24, 178, 170);

    [Header("Camera")]
    public float cameraMinY;
    public float cameraHeightY;
    public float cameraMinX = -1000;
    public float cameraMaxX = 1000;

    [Header("Misc")]
    [SerializeField] private GameObject hud;
    [SerializeField] private GameObject pauseUI, pausePanel, pauseButton, hostExitUI, hostExitButton, nametagPrefab;
    [SerializeField] public Tilemap tilemap;
    [SerializeField] public GameObject objectPoolParent;
    [SerializeField] private TMP_Text winText;
    [SerializeField] private Animator winTextAnimator;

    //---Public Variables
    public readonly HashSet<NetworkObject> networkObjects = new();
    public SingleParticleManager particleManager;
    public TeamManager teamManager = new();
    public GameEventRpcs rpcs;
    public Canvas nametagCanvas;
    public PlayerController localPlayer;
    public double gameStartTimestamp, gameEndTimestamp;
    public bool paused;

    [NonSerialized] public KillableEntity[] enemies;
    [NonSerialized] public FloatingCoin[] coins;

    //---Private Variables
    private TickTimer StartMusicTimer { get; set; }
    private readonly List<GameObject> activeStarSpawns = new();
    private GameObject[] starSpawns;
    private bool hurryUpSoundPlayed;
    private bool pauseStateLastFrame, optionsWereOpenLastFrame;

    //---Components
    [SerializeField] public TileManager tileManager;
    [SerializeField] public SpectationManager spectationManager;
    [SerializeField] public LoopingMusicPlayer musicManager;
    [SerializeField] public AudioSource music, sfx;

    // TODO: convert to RPC...?
    public void SpawnResizableParticle(Vector2 pos, bool right, bool flip, Vector2 size, GameObject prefab) {
        GameObject particle = Instantiate(prefab, pos, Quaternion.Euler(0, 0, flip ? 180 : 0));

        SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();
        sr.size = size;

        Rigidbody2D body = particle.GetComponent<Rigidbody2D>();
        body.velocity = new Vector2(right ? 7 : -7, 6);
        body.angularVelocity = right ^ flip ? -300 : 300;

        particle.transform.position += new Vector3(sr.size.x * 0.25f, size.y * 0.25f * (flip ? -1 : 1));
    }

    // Register pause & networking events
    public void OnEnable() {
        ControlSystem.controls.UI.Pause.performed += OnPause;
        ControlSystem.controls.Debug.ToggleHUD.performed += OnToggleHud;
        NetworkHandler.OnShutdown += OnShutdown;
        NetworkHandler.OnPlayerLeft += OnPlayerLeft;
    }

    public void OnDisable() {
        ControlSystem.controls.UI.Pause.performed -= OnPause;
        ControlSystem.controls.Debug.ToggleHUD.performed -= OnToggleHud;
        NetworkHandler.OnShutdown -= OnShutdown;
        NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
    }

    public void OnValidate() {
        // Remove our cached values if we change something in editor.
        // We shouldn't have to worry about values changing mid-game ever.
        levelWidth = levelHeight = middleX = minX = minY = maxX = maxY = null;
    }

    public void Awake() {
        Instance = this;
        particleManager = GetComponentInChildren<SingleParticleManager>();

        //Make UI color translucent
        levelUIColor.a = .7f;
    }

    public void Start() {
        // Handles spawning in editor
        if (!NetworkHandler.Runner.SessionInfo.IsValid) {
            // Join a singleplayer room if we're not in one
            _ = NetworkHandler.CreateRoom(new() {
                Scene = SceneManager.GetActiveScene().buildIndex,
            }, GameMode.Single);
        }

        nametagCanvas.gameObject.SetActive(Settings.Instance.GraphicsPlayerNametags);
    }

    public override void Spawned() {
        // By default, spectate. when we get assigned a player object, we disable it there.
        spectationManager.Spectating = true;

        // Enable player controls
        Runner.ProvideInput = true;

        // Find objects in the scene
        starSpawns = GameObject.FindGameObjectsWithTag("StarSpawn");
        enemies = FindObjectsOfType<KillableEntity>().Where(ke => ke is not BulletBillMover).ToArray();
        coins = FindObjectsOfType<FloatingCoin>();

        if (Runner.IsServer && Runner.IsSinglePlayer) {
            // Handle spawning in editor by spawning the room + player data objects
            Runner.Spawn(PrefabList.Instance.SessionDataHolder);
            NetworkObject localData = Runner.Spawn(PrefabList.Instance.PlayerDataHolder, inputAuthority: Runner.LocalPlayer);
            Runner.SetPlayerObject(Runner.LocalPlayer, localData);
        }

        if (GameStartTime <= 0) {
            // The game hasn't started.
            // Tell our host that we're done loading
            PlayerData localData = Runner.GetLocalPlayerData();
            localData.Rpc_FinishedLoading();
        } else {
            // The game HAS already started.
            SetGameTimestamps();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (!runner.IsServer || !hasState)
            return;

        // Remove all networked objects. Fusion doesn't do this for us, unlike PUN.
        foreach (var obj in networkObjects)
            runner.Despawn(obj);
    }

    public override void Render() {
        // Handle sound effects for the timer, if it's enabled
        if (GameEndTimer.IsRunning) {
            if (GameEndTimer.Expired(Runner)) {
                sfx.PlayOneShot(Enums.Sounds.UI_Countdown_1);
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

    public override void FixedUpdateNetwork() {
        if (GameEnded)
            return;

        // Seed RNG for this tick
        Random = new(Runner.Simulation.Tick);

        if (BigStarRespawnTimer.Expired(Runner)) {
            if (AttemptSpawnBigStar())
                BigStarRespawnTimer = TickTimer.None;
            else
                BigStarRespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
        }

        if (GameStartTimer.Expired(Runner)) {
            GameStartTimer = TickTimer.None;
            StartGame();
        }

        if (StartMusicTimer.Expired(Runner)) {
            StartMusicTimer = TickTimer.None;
            IsMusicEnabled = true;
        }

        if (IsMusicEnabled)
            HandleMusic();

        if (GameEndTimer.Expired(Runner)) {
            CheckForWinner();
            GameEndTimer = TickTimer.None;
        }
    }

    public void Update() {
        pauseStateLastFrame = paused;
        optionsWereOpenLastFrame = GlobalController.Instance.optionsManager.gameObject.activeSelf;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        // Kill player if they are still alive
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

    /// <summary>
    /// Officially starts the game if all clients say that they're loaded.
    /// </summary>
    public void CheckIfAllPlayersLoaded() {
        // If we aren't the server, don't bother checking. We can't start the game regardless.
        if (!Runner || !Runner.IsServer || GameState != Enums.GameState.Loading)
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

        // Everyone is loaded, officially start the game.
        GameState = Enums.GameState.Starting;
        SceneManager.SetActiveScene(gameObject.scene);
        GameStartTimer = TickTimer.CreateFromSeconds(Runner, Runner.IsSinglePlayer ? 0.2f : 5.7f);

        // Find out how many players we have
        foreach (PlayerRef client in Runner.ActivePlayers) {
            PlayerData data = client.GetPlayerData(Runner);
            if (!data || data.IsCurrentlySpectating)
                continue;

            RealPlayerCount++;
        }

        List<int> spawnpoints = Enumerable.Range(0, RealPlayerCount).ToList();

        // Create player instances
        foreach (PlayerRef player in Runner.ActivePlayers) {
            PlayerData data = player.GetPlayerData(Runner);
            if (!data)
                continue;

            data.IsLoaded = false;
            if (data.IsCurrentlySpectating)
                continue;

            Runner.Spawn(data.GetCharacterData().prefab, spawnpoint, inputAuthority: player, onBeforeSpawned: (runner, obj) => {
                // Set the spawnpoint that they should spawn at
                int index = UnityEngine.Random.Range(0, spawnpoints.Count);
                int spawnpoint = spawnpoints[index];
                spawnpoints.RemoveAt(index);

                obj.GetComponent<PlayerController>().OnBeforeSpawned(spawnpoint);
            });
        }

        // Create pooled Fireball instances (max of 6 per player)
        for (int i = 0; i < RealPlayerCount * 6; i++)
            Runner.Spawn(PrefabList.Instance.Obj_Fireball);

        // Tell everyone else to start the game
        StartCoroutine(CallLoadingComplete(2));
    }

    private IEnumerator CallLoadingComplete(float seconds) {
        yield return new WaitForSeconds(seconds);
        rpcs.Rpc_LoadingComplete();
    }

    private void StartGame() {
        GameState = Enums.GameState.Playing;

        // Respawn players
        foreach (PlayerController player in AlivePlayers)
            player.PreRespawn();

        // Play start jingle
        if (Runner.IsForward)
            sfx.PlayOneShot(Enums.Sounds.UI_StartGame);

        StartMusicTimer = TickTimer.CreateFromSeconds(Runner, 1.3f);

        // Respawn enemies
        foreach (KillableEntity enemy in enemies)
            enemy.RespawnEntity();

        // Start timer
        int timer = SessionData.Instance.Timer;
        if (timer > 0)
            GameEndTimer = TickTimer.CreateFromSeconds(Runner, timer);

        // Start "WaitForGameStart" objects
        foreach (var wfgs in FindObjectsOfType<WaitForGameStart>())
            wfgs.AttemptExecute();

        // Spawn the initial Big Star
        AttemptSpawnBigStar();

        // Keep track of game timestamps
        GameStartTime = Runner.SimulationTime;
        SetGameTimestamps();

        // Update Discord RPC status
        GlobalController.Instance.discordController.UpdateActivity();
    }

    /// <summary>
    /// Sets the game timestamps for Discord RPC
    /// </summary>
    private void SetGameTimestamps() {
        double now = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
        float secondsSinceStart = Runner.SimulationTime - GameStartTime;
        gameStartTimestamp = now - secondsSinceStart;

        int timer = SessionData.Instance.Timer;
        if (timer > 0)
            gameEndTimestamp = gameStartTimestamp + timer;
    }

    internal IEnumerator EndGame(int winningTeam) {
        //TODO: Clean this up, massively.

        GameState = Enums.GameState.Ended;
        SessionData.Instance.SetGameStarted(false);
        SessionData.Instance.GameStartTimer = TickTimer.None;

        music.Stop();

        TranslationManager tm = GlobalController.Instance.translationManager;
        string resultText;
        if (winningTeam == -1) {
            resultText = tm.GetTranslation("ui.result.draw");
        } else {
            if (SessionData.Instance.Teams) {
                Team team = ScriptableManager.Instance.teams[winningTeam];
                resultText = tm.GetTranslationWithReplacements("ui.result.teamwin", "team", team.displayName);
            } else {
                string username = teamManager.GetTeamMembers(winningTeam).First().data.GetNickname();
                resultText = tm.GetTranslationWithReplacements("ui.result.playerwin", "playername", username);
            }

            if (Runner.IsServer) {
                foreach (PlayerController player in teamManager.GetTeamMembers(winningTeam)) {
                    player.data.Wins++;
                }
            }
        }
        winText.text = resultText;

        yield return new WaitForSecondsRealtime(1);

        bool draw = winningTeam == -1;
        PlayerData local = Runner.GetLocalPlayerData();
        bool win = !draw && (winningTeam == local.Team || local.IsCurrentlySpectating);
        int secondsUntilMenu = draw ? 5 : 4;

        if (draw) {
            music.PlayOneShot(Enums.Sounds.UI_Match_Draw);
            winTextAnimator.SetTrigger("startNegative");
        } else if (win) {
            music.PlayOneShot(Enums.Sounds.UI_Match_Win);
            winTextAnimator.SetTrigger("start");
        } else {
            music.PlayOneShot(Enums.Sounds.UI_Match_Lose);
            winTextAnimator.SetTrigger("startNegative");
        }

        //TODO: make a results screen?

        if (Runner.IsServer) {
            // Handle resetting player states for the next game
            foreach (PlayerRef player in Runner.ActivePlayers) {
                PlayerData data = player.GetPlayerData(Runner);

                // Set IsLoaded to false
                data.IsLoaded = false;

                // Set spectating state to false
                data.IsCurrentlySpectating = false;

                // Move people without teams into a valid teams range
                if (SessionData.Instance.Teams)
                    data.Team = (sbyte) Mathf.Clamp(data.Team, 0, ScriptableManager.Instance.teams.Length);
            }
        }

        // Return back to the main menu
        yield return new WaitForSecondsRealtime(secondsUntilMenu);
        Runner.SetActiveScene(0);
    }

    /// <summary>
    /// Spawns a Big Star, if we can find a valid spawnpoint.
    /// </summary>
    /// <returns>If the start is successfully spawned</returns>
    private bool AttemptSpawnBigStar() {

        for (int i = 0; i < starSpawns.Length; i++) {
            if (activeStarSpawns.Count <= 0)
                activeStarSpawns.AddRange(starSpawns);

            int index = Random.RangeExclusive(0, activeStarSpawns.Count);
            Vector3 spawnPos = activeStarSpawns[index].transform.position;

            if (Runner.GetPhysicsScene2D().OverlapCircle(spawnPos, 4, Layers.MaskOnlyPlayers)) {
                // A player is too close to this spawn. Discard
                activeStarSpawns.RemoveAt(index);
                continue;
            }

            // Found a valid spawn
            Runner.Spawn(PrefabList.Instance.Obj_BigStar, spawnPos, onBeforeSpawned: (runner, obj) => {
                obj.GetComponent<StarBouncer>().OnBeforeSpawned(0, true, false);
            });
            activeStarSpawns.RemoveAt(index);
            return true;
        }

        // No star could spawn.
        return false;
    }

    public void CreateNametag(PlayerController controller) {
        GameObject nametag = Instantiate(nametagPrefab, nametagPrefab.transform.parent);
        nametag.GetComponent<UserNametag>().parent = controller;
        nametag.SetActive(true);
    }

    /// <summary>
    /// Checks if a team has won, and calls Rpc_EndGame if one has.
    /// </summary>
    public void CheckForWinner() {
        if (GameState != Enums.GameState.Playing || !Object.HasStateAuthority)
            return;

        int requiredStars = SessionData.Instance.StarRequirement;
        bool starGame = requiredStars != -1;

        bool hasFirstPlace = teamManager.HasFirstPlaceTeam(out int firstPlaceTeam, out int firstPlaceStars);
        int aliveTeams = teamManager.GetAliveTeamCount();
        bool timeUp = GameEndTimer.Expired(Runner);

        if (aliveTeams == 0) {
            // All teams dead, draw?
            rpcs.Rpc_EndGame(PlayerRef.None);
            return;
        }

        if (aliveTeams == 1 && RealPlayerCount > 1) {
            // One team left alive (and it's not a solo game), they win immediately.
            rpcs.Rpc_EndGame(firstPlaceTeam);
            return;
        }

        if (hasFirstPlace) {
            // We have a team that's clearly in first...
            if (starGame && firstPlaceStars >= requiredStars) {
                // And they have enough stars.
                rpcs.Rpc_EndGame(firstPlaceTeam);
                return;
            }
            // They don't have enough stars. wait 'till later
        }

        if (timeUp) {
            // Ran out of time, instantly end if DrawOnTimeUp is set
            if (SessionData.Instance.DrawOnTimeUp) {
                // No one wins
                rpcs.Rpc_EndGame(PlayerRef.None);
                return;
            }
            // Keep playing into overtime.
        }

        // No winner, Keep playing
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
            musicManager.Play(megaMushroomMusic);
        } else if (invincible) {
            musicManager.Play(invincibleMusic);
        } else {
            musicManager.Play(mainMusic);
        }

        musicManager.FastMusic = speedup;
    }

    public void OnToggleHud(InputAction.CallbackContext context) {
        hud.SetActive(!hud.activeSelf);
    }

    public void OnPause(InputAction.CallbackContext context) {
        if (optionsWereOpenLastFrame)
            return;

        Pause(!pauseStateLastFrame);
    }

    public void Pause(bool newState) {
        if (paused == newState || GameState != Enums.GameState.Playing)
            return;

        paused = newState;
        sfx.PlayOneShot(Enums.Sounds.UI_Pause);
        pauseUI.SetActive(paused);
        pausePanel.SetActive(paused);
    }

    //---UI Callbacks
    public void AttemptQuit() {
        if (!Runner.GetLocalPlayerData().IsRoomOwner) {
            QuitGame();
            return;
        }

        // Prompt for ending game or leaving
        sfx.PlayOneShot(Enums.Sounds.UI_Decide);
        pausePanel.SetActive(false);
        hostExitUI.SetActive(true);
        EventSystem.current.SetSelectedGameObject(hostExitButton);
    }

    public void HostEndMatch() {
        pauseUI.SetActive(false);
        sfx.PlayOneShot(Enums.Sounds.UI_Decide);
        rpcs.Rpc_EndGame(PlayerRef.None);
    }

    public void QuitGame() {
        sfx.PlayOneShot(Enums.Sounds.UI_Decide);
        Runner.Shutdown();
    }

    public void HostQuitCancel() {
        pausePanel.SetActive(true);
        hostExitUI.SetActive(false);
        sfx.PlayOneShot(Enums.Sounds.UI_Back);
        EventSystem.current.SetSelectedGameObject(pauseButton);
    }

    public void OpenOptions() {
        GlobalController.Instance.optionsManager.OpenMenu();
        sfx.PlayOneShot(Enums.Sounds.UI_Decide);
    }

    //---Helpers
    public float size = 1.39f, ySize = 0.8f;
    public Vector3 GetSpawnpoint(int playerIndex, int players = -1) {
        if (players <= -1)
            players = RealPlayerCount;
        if (players == 0)
            players = 1;

        float comp = (float) playerIndex / players * 2.5f * Mathf.PI + (Mathf.PI / (2 * players));
        float scale = (2f - (players + 1f) / players) * size;

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
                Vector2Int loc = new(x + levelMinTileX, y + levelMinTileY);
                TileBase tile = tilemap.GetTile((Vector3Int) loc);

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
