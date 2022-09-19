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

using NSMB.Utils;
using NSMB.Extensions;
using Fusion;

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
    [Networked] public NetworkRNG Random { get; set; }
    [Networked, Capacity(10)] private NetworkLinkedList<PlayerController> Players => default;
    [Networked, Capacity(10)] private NetworkLinkedList<PlayerRef> LoadedPlayers => default;
    [Networked] public NetworkBool GameStarted { get; set; }

    //---Serialized Variables
    [SerializeField] private MusicData mainMusic, invincibleMusic, megaMushroomMusic;

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
    public Enums.MusicState? musicState = null;

    public PlayerController localPlayer;

    public bool paused, loaded;
    public GameObject pauseUI, pausePanel, pauseButton, hostExitUI, hostExitButton;
    public bool gameover = false, musicEnabled = false;
    public int starRequirement, timedGameDuration = -1, coinRequirement;
    public bool hurryUp = false;

    public int playerCount = 1;
    public List<PlayerController> players = new();

    //---Private Variables
    private EnemySpawnpoint[] enemySpawns;
    private FloatingCoin[] coins;

    //---Components
    public SpectationManager spectationManager;
    private LoopingMusic loopMusic;


    private ParticleSystem brickBreak;

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
            Utils.ApplyTilemapChanges(originalTiles, origin, tilemap, changes);
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
    }
    public void OnDisable() {
        InputSystem.controls.UI.Pause.performed -= OnPause;
    }

    public void Awake() {
        Instance = this;
        spectationManager = GetComponent<SpectationManager>();
        loopMusic = GetComponent<LoopingMusic>();

        //Make UI color translucent
        levelUIColor.a = .7f;

        //Check if we spawned inside the editor?
        if (!NetworkHandler.Instance.runner.IsConnectedToServer) {
            //uhhh
            NetworkRunner runner = NetworkHandler.Instance.runner;
            runner.StartGame(new() {
                GameMode = GameMode.Single,
                SessionName = "debug",
                SessionProperties = NetworkUtils.DefaultRoomProperties,
            });
        }

        SceneManager.SetActiveScene(gameObject.scene);
    }

    public override void Spawned() {
        //by default, spectate. when we get assigned a player object, we disable it there.
        spectationManager.Spectating = true;

        //start RNG
        if (Runner.IsServer)
            Random = new(UnityEngine.Random.Range(int.MinValue, int.MaxValue));

        //Load + enable player controls
        InputSystem.controls.LoadBindingOverridesFromJson(GlobalController.Instance.controlsJson);
        Runner.ProvideInput = true;

        //Cache game settings
        Utils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.StarRequirement, out starRequirement);
        Utils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.CoinRequirement, out coinRequirement);

        //Setup respawning tilemap
        origin = new(levelMinTileX, levelMinTileY, 0, levelWidthTile, levelHeightTile, 1);
        originalTiles = tilemap.GetTilesBlock(origin);

        //Find objects in the scene
        starSpawns = GameObject.FindGameObjectsWithTag("StarSpawn");
        coins = FindObjectsOfType<FloatingCoin>();
        enemySpawns = FindObjectsOfType<EnemySpawnpoint>();

        //create player instances
        if (Runner.IsServer) {
            foreach (PlayerRef player in Runner.ActivePlayers) {
                CharacterData character = player.GetCharacterData(Runner);
                NetworkObject obj = Runner.Spawn((GameObject) Resources.Load("Prefabs/" + character.prefab), spawnpoint, inputAuthority: player);
                Players.Add(obj.GetComponent<PlayerController>());

                playerCount++;
            }
        }

        brickBreak = ((GameObject) Instantiate(Resources.Load("Prefabs/Particle/BrickBreak"))).GetComponent<ParticleSystem>();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeResim = true)]
    public void Rpc_ResetTilemap() {
        tilemap.SetTilesBlock(origin, originalTiles);

        foreach (FloatingCoin coin in coins)
            coin.IsCollected = false;

        foreach (EnemySpawnpoint point in enemySpawns)
            point.AttemptSpawning();

        BigStarRespawnTimer = TickTimer.CreateFromSeconds(Runner, 10.4f - playerCount / 5f);
    }

    public void OnPlayerLoaded() {
        CheckIfAllPlayersLoaded();
    }


    private void CheckIfAllPlayersLoaded() {
        if (loaded)
            return;

        foreach (PlayerRef player in Runner.ActivePlayers) {
            PlayerData data = player.GetPlayerData(Runner);

            if (data == null || data.IsCurrentlySpectating)
                continue;

            if (!data.IsLoaded)
                return;
        }

        loaded = true;
        GameStartTimer = TickTimer.CreateFromSeconds(Runner, 1.5f);
    }

    //TODO: invokeresim?
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_EndGame(PlayerRef winner) {

        //TODO: don't use a coroutine.
        StartCoroutine(EndGame(winner));
    }

    private void FinishLoading() {

        //Populate scoreboard
        ScoreboardUpdater.instance.Populate(players);
        if (Settings.Instance.scoreboardAlways)
            ScoreboardUpdater.instance.SetEnabled();

        //Finalize loading screen
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

        GameStarted = true;
    }

    private void StartGame() {
        started = true;

        //Respawn players
        foreach (PlayerController player in players) {
            player.PreRespawn();
        }

        //Respawn enemies
        if (Runner.IsServer) {
            foreach (EnemySpawnpoint point in FindObjectsOfType<EnemySpawnpoint>())
                point.AttemptSpawning();
        }

        //Start timer
        Utils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.Time, out int timedGameDuration);
        if (timedGameDuration > 0)
            GameEndTimer = TickTimer.CreateFromSeconds(Runner, timedGameDuration);

        //Play start sfx
        sfx.PlayOneShot(Enums.Sounds.UI_StartGame.GetClip());



        foreach (PlayerController controllers in players)
            if (controllers) {
                if (spectating && controllers.sfx) {
                    controllers.sfxBrick.enabled = true;
                    controllers.sfx.enabled = true;
                }
                controllers.gameObject.SetActive(spectating);
            }


        startServerTime = startTimestamp + 3500;
        foreach (var wfgs in FindObjectsOfType<WaitForGameStart>())
            wfgs.AttemptExecute();

        yield return new WaitForSeconds(1f);

        musicEnabled = true;

        startRealTime = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (timedGameDuration > 0) {
            endServerTime = startTimestamp + 4500 + timedGameDuration * 1000;
            endRealTime = startRealTime + 4500 + timedGameDuration * 1000;
        }

        GlobalController.Instance.DiscordController.UpdateActivity();

        if (canvas)
            SceneManager.UnloadSceneAsync("Loading");
    }

    private IEnumerator FinalizeGameStart() {
        yield return new WaitForSeconds(3f);
        musicEnabled = true;
        SceneManager.UnloadSceneAsync("Loading");
    }

    private IEnumerator EndGame(PlayerRef winner) {
        //TODO:
        //PhotonNetwork.CurrentRoom.SetCustomProperties(new() { [Enums.NetRoomProperties.GameStarted] = false });
        gameover = true;
        music.Stop();
        GameObject text = GameObject.FindWithTag("wintext");
        text.GetComponent<TMP_Text>().text = winner != null ? winner.GetPlayerData(Runner).GetNickname() + " Wins!" : "It's a draw...";

        yield return new WaitForSecondsRealtime(1);
        text.GetComponent<Animator>().SetTrigger("start");

        AudioMixer mixer = music.outputAudioMixerGroup.audioMixer;
        mixer.SetFloat("MusicSpeed", 1f);
        mixer.SetFloat("MusicPitch", 1f);

        bool win = winner != null && winner == Runner.LocalPlayer;
        bool draw = winner == PlayerRef.None;
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

        if (Runner.IsServer)
            Runner.SetActiveScene(0);
    }

    private void SpawnBigStar() {

        for (int i = 0; i < starSpawns.Length; i++) {
            if (remainingSpawns.Count <= 0)
                remainingSpawns.AddRange(starSpawns);

            int index = Random.RangeExclusive(0, remainingSpawns.Count);
            Vector3 spawnPos = remainingSpawns[index].transform.position;

            if (Runner.GetPhysicsScene2D().OverlapCircle(spawnPos, 4, Layers.MaskOnlyPlayers)) {
                //a player is too close to the spawn
                remainingSpawns.RemoveAt(index);
                continue;
            }

            Runner.Spawn(PrefabList.BigStar, spawnPos, onBeforeSpawned: (runner, obj) => {
                obj.GetComponent<StarBouncer>().OnBeforeSpawned(0, true, false);
            });
            remainingSpawns.RemoveAt(index);
            break;
        }
    }

    public override void FixedUpdateNetwork() {

        if (BigStarRespawnTimer.Expired(Runner)) {
            SpawnBigStar();
            BigStarRespawnTimer = TickTimer.None;
        }

        if (GameStartTimer.Expired(Runner)) {
            LoadingComplete();
            GameStartTimer = TickTimer.None;
        }

        if (GameEndTimer.IsRunning) {
            if (GameEndTimer.Expired(Runner)) {
                CheckForWinner();

                sfx.PlayOneShot(Enums.Sounds.UI_Countdown_1.GetClip());
                GameEndTimer = TickTimer.None;
                return;
            }

            int tickrate = Runner.Config.Simulation.TickRate;
            int remainingTicks = GameEndTimer.RemainingTicks(Runner) ?? 0;
            if (!hurryUp && remainingTicks < 60 * tickrate) {
                hurryUp = true;
                sfx.PlayOneShot(Enums.Sounds.UI_HurryUp.GetClip());
            } else if (remainingTicks < (10 * tickrate)) {
                //10 second "dings"
                if (remainingTicks % tickrate == 0)
                    sfx.PlayOneShot(Enums.Sounds.UI_Countdown_0.GetClip());

                //at 3 seconds, double speed
                if (remainingTicks < (3 * tickrate) && remainingTicks % (tickrate / 2) == 0)
                    sfx.PlayOneShot(Enums.Sounds.UI_Countdown_0.GetClip());
            }
        }
    }





    public void Update() {
        if (gameover)
            return;

        if (GameStarted && musicEnabled) {
            bool allNull = true;
            foreach (PlayerController controller in players) {
                if (controller) {
                    allNull = false;
                    break;
                }
            }
            if (spectationManager.Spectating && allNull) {
                StartCoroutine(EndGame(PlayerRef.None));
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
        if (gameover || !Runner.IsServer)
            return;

        bool starGame = starRequirement != -1;
        bool timeUp = GameEndTimer.Expired(Runner);
        int winningStars = -1;
        List<PlayerController> winningPlayers = new();
        List<PlayerController> alivePlayers = new();
        foreach (var player in players) {
            if (player == null || player.Lives == 0)
                continue;

            alivePlayers.Add(player);

            if ((starGame && player.Stars >= starRequirement) || timeUp) {
                //we're in a state where this player would win.
                //check if someone has more stars
                if (player.Stars > winningStars) {
                    winningPlayers.Clear();
                    winningStars = player.Stars;
                    winningPlayers.Add(player);
                } else if (player.Stars == winningStars) {
                    winningPlayers.Add(player);
                }
            }
        }
        //LIVES CHECKS
        if (alivePlayers.Count == 0) {
            //everyone's dead...? ok then, draw?
            Rpc_EndGame(PlayerRef.None);
            return;
        } else if (alivePlayers.Count == 1 && playerCount >= 2) {
            //one player left alive (and not in a solo game). winner!
            Rpc_EndGame(alivePlayers[0].Object.InputAuthority);
            return;
        }

        //TIMED CHECKS
        if (timeUp) {
            Utils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.DrawTime, out bool draw);

            //time up! check who has most stars, if a tie keep playing, if draw is on end game in a draw
            if (draw) {
                // it's a draw! Thanks for playing the demo!
                Rpc_EndGame(PlayerRef.None);
            } else if (winningPlayers.Count == 1) {
                Rpc_EndGame(winningPlayers[0].Object.InputAuthority);
            }
            //keep plaing
            return;
        }

        if (starGame && winningStars >= starRequirement) {
            if (winningPlayers.Count == 1)
                Rpc_EndGame(winningPlayers[0].Object.InputAuthority);

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

            if (player.State == Enums.PowerupState.MegaMushroom && player.giantTimer != 15)
                mega = true;
            if (player.IsStarmanInvincible)
                invincible = true;
            if ((player.Stars + 1f) / starRequirement >= 0.95f || hurryUp != false)
                speedup = true;
            if (player.Lives == 1 && players.Count <= 2)
                speedup = true;
        }

        speedup |= players.All(pl => !pl || pl.Lives == 1 || pl.Lives == 0);

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

        if (Runner.IsServer) {
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
        Rpc_EndGame(PlayerRef.None);
    }

    public void Quit() {
        sfx.PlayOneShot(Enums.Sounds.UI_Decide.GetClip());
        Runner.Shutdown();
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
