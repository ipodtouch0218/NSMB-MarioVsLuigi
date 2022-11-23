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

    //---Public Variables
    public readonly HashSet<NetworkObject> networkObjects = new();
    public EnemySpawnpoint[] enemySpawns;
    public SingleParticleManager particleManager;
    public TeamManager teamManager = new();
    public GameEventRpcs rpcs;
    public Canvas nametagCanvas;
    public GameObject nametagPrefab;
    public PlayerController localPlayer;
    public long gameStartTimestamp, gameEndTimestamp;
    public bool paused, loaded, gameover;
    public BoundsInt originalTilesOrigin;
    public TileBase[] originalTiles;

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

    //TODO: convert to RPC...?
    public void SpawnResizableParticle(Vector2 pos, bool right, bool flip, Vector2 size, GameObject prefab) {
        GameObject particle = Instantiate(prefab, pos, Quaternion.Euler(0, 0, flip ? 180 : 0));

        SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();
        sr.size = size;

        Rigidbody2D body = particle.GetComponent<Rigidbody2D>();
        body.velocity = new Vector2(right ? 7 : -7, 6);
        body.angularVelocity = right ^ flip ? -300 : 300;

        particle.transform.position += new Vector3(sr.size.x / 4f, size.y / 4f * (flip ? -1 : 1));
    }

    //Register pause event
    public void OnEnable() {
        InputSystem.controls.UI.Pause.performed += OnPause;
        NetworkHandler.OnShutdown +=               OnShutdown;
        NetworkHandler.OnPlayerJoined +=           OnPlayerJoined;
        NetworkHandler.OnPlayerLeft +=             OnPlayerLeft;
    }

    public void OnDisable() {
        InputSystem.controls.UI.Pause.performed -= OnPause;
        NetworkHandler.OnShutdown -=               OnShutdown;
        NetworkHandler.OnPlayerJoined -=           OnPlayerJoined;
        NetworkHandler.OnPlayerLeft -=             OnPlayerLeft;
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
        tilemap.RefreshAllTiles();

        //Find objects in the scene
        starSpawns = GameObject.FindGameObjectsWithTag("StarSpawn");
        enemySpawns = FindObjectsOfType<EnemySpawnpoint>();

        if (Runner.IsServer) {
            //create player instances
            foreach (PlayerRef player in Runner.ActivePlayers) {
                PlayerData data = player.GetPlayerData(Runner);
                if (data.IsCurrentlySpectating)
                    continue;

                Runner.Spawn(data.GetCharacterData().prefab, spawnpoint, inputAuthority: player);
                RealPlayerCount++;
            }

            //create pooled fireball instances (6 per player)
            for (int i = 0; i < RealPlayerCount * 6; i++)
                Runner.Spawn(PrefabList.Instance.Obj_Fireball);
        }

        //finished loading
        if (Runner.IsSinglePlayer) {
            CheckIfAllPlayersLoaded();
        } else {
            //tell our host that we're done loading
            PlayerData data = NetworkHandler.Runner.GetLocalPlayerData();
            if (data)
                data.Rpc_FinishedLoading();
        }

        Camera.main.transform.position = spawnpoint;
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (runner.IsServer || !hasState)
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

            Debug.Log(string.Join(',', tileNames));
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
        GameStartTimer = TickTimer.CreateFromSeconds(NetworkHandler.Runner, 5.7f);

        if (Runner.IsServer)
            StartCoroutine(CallLoadingComplete(2));

        foreach (PlayerRef player in NetworkHandler.Runner.ActivePlayers)
            player.GetPlayerData(NetworkHandler.Runner).IsLoaded = false;
    }

    private IEnumerator CallLoadingComplete(float seconds) {
        yield return new WaitForSeconds(seconds);
        Rpc_LoadingComplete();
    }

    //TODO: invokeresim?
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_EndGame(int team) {
        if (gameover)
            return;

        //TODO: don't use a coroutine?
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
        AttemptSpawnBigStar();

        GameStartTime = Runner.SimulationTime;

        gameStartTimestamp = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        gameEndTimestamp = (timer > 0) ? gameStartTimestamp + timer * 1000 : 0;

        GlobalController.Instance.DiscordController.UpdateActivity();

        StartMusicTimer = TickTimer.CreateFromSeconds(Runner, 1.3f);
    }

    private IEnumerator EndGame(int winningTeam) {
        //TODO:
        //PhotonNetwork.CurrentRoom.SetCustomProperties(new() { [Enums.NetRoomProperties.GameStarted] = false });
        gameover = true;
        LobbyData.Instance.SetGameStarted(false);

        music.Stop();
        GameObject text = GameObject.FindWithTag("wintext");
        TMP_Text tmpText = text.GetComponent<TMP_Text>();

        if (winningTeam == -1) {
            tmpText.text = "It's a draw";
        } else {
            if (LobbyData.Instance.Teams) {
                Team team = ScriptableManager.Instance.teams[winningTeam];
                tmpText.text = team.displayName + " Wins!";
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
                if (LobbyData.Instance.Teams)
                    data.Team = (sbyte) Mathf.Clamp(data.Team, 0, ScriptableManager.Instance.teams.Length);
            }
        }

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

        speedup |= teamManager.GetFirstPlaceStars() + 1 >= LobbyData.Instance.StarRequirement;
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
        Rpc_EndGame(PlayerRef.None);
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
    //lazy loading
    private float? middleX, minX, minY, maxX, maxY, levelWidth, levelHeight;
    public float GetLevelMiddleX() => middleX ??= (GetLevelMaxX() + GetLevelMinX()) * 0.5f;
    public float GetLevelMinX() => minX ??= (levelMinTileX * tilemap.transform.localScale.x) + tilemap.transform.position.x;
    public float GetLevelMinY() => minY ??= (levelMinTileY * tilemap.transform.localScale.y) + tilemap.transform.position.y;
    public float GetLevelMaxX() => maxX ??= ((levelMinTileX + levelWidthTile) * tilemap.transform.localScale.x) + tilemap.transform.position.x;
    public float GetLevelMaxY() => maxY ??= ((levelMinTileY + levelHeightTile) * tilemap.transform.localScale.y) + tilemap.transform.position.y;
    public float GetLevelWidth() => levelWidth ??= levelWidthTile * 0.5f;
    public float GetLevelHeight() => levelHeight ??= levelHeightTile * 0.5f;


    public float size = 1.39f, ySize = 0.8f;
    public Vector3 GetSpawnpoint(int playerIndex, int players = -1) {
        if (players <= -1)
            players = RealPlayerCount;
        if (players == 0)
            players = 1;

        float comp = (float) playerIndex / players * 2.5f * Mathf.PI + (Mathf.PI/(2*players));
        float scale = (2 - (players + 1f) / players) * size;

        Vector3 spawn = spawnpoint + new Vector3(Mathf.Sin(comp) * scale, Mathf.Cos(comp) * (players > 2 ? scale * ySize : 0), 0);
        Utils.WrapWorldLocation(ref spawn);

        return spawn;
    }

    //---Debug
    private static readonly int DebugSpawns = 10;
    public void OnDrawGizmos() {

        if (!tilemap)
            return;

        for (int i = 0; i < DebugSpawns; i++) {
            Gizmos.color = new Color((float) i / DebugSpawns, 0, 0, 0.75f);
            Gizmos.DrawCube(GetSpawnpoint(i, DebugSpawns) + Vector3.down * 0.25f, Vector2.one * 0.5f);
        }

        Vector3 size = new(GetLevelWidth(), GetLevelHeight());
        Vector3 origin = new(GetLevelMinX() + (GetLevelWidth() * 0.5f), GetLevelMinY() + (GetLevelHeight() * 0.5f), 1);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(origin, size);

        size = new Vector3(GetLevelWidth(), cameraHeightY);
        origin.y = cameraMinY + (cameraHeightY * 0.5f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(origin, size);

        Vector3 oneFourth = Vector3.one * 0.25f;
        for (int x = 0; x < levelWidthTile; x++) {
            for (int y = 0; y < levelHeightTile; y++) {
                Vector3Int loc = new(x+levelMinTileX, y+levelMinTileY, 0);
                TileBase tile = tilemap.GetTile(loc);
                if (tile is CoinTile)
                    Gizmos.DrawIcon(Utils.TilemapToWorldPosition(loc, this) + oneFourth, "coin");
                if (tile is PowerupTile)
                    Gizmos.DrawIcon(Utils.TilemapToWorldPosition(loc, this) + oneFourth, "powerup");
            }
        }

        Gizmos.color = new(1, 0.9f, 0.2f, 0.2f);
        Color starBoxColor = new(1, 1, 1, 0.5f);
        foreach (GameObject starSpawn in GameObject.FindGameObjectsWithTag("StarSpawn")) {
            Gizmos.DrawCube(starSpawn.transform.position, Vector3.one);
            Gizmos.DrawIcon(starSpawn.transform.position, "star", true, starBoxColor);
        }
    }
}
