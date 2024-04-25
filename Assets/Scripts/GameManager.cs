using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using TMPro;

using Fusion;
using NSMB.Entities;
using NSMB.Entities.Enemies;
using NSMB.Entities.Collectable;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Tiles;
using NSMB.Translation;
using NSMB.Utils;

namespace NSMB.Game {
    public class GameManager : NetworkBehaviour {

        //---Static Variables
        public static event Action OnAllPlayersLoaded;
        private static readonly Vector3 OneFourth = new(0.25f, 0.25f, 0f);
        private static GameManager _instance;
        public static GameManager Instance {
            get {
                if (_instance) {
                    return _instance;
                }

                if (SceneManager.GetActiveScene().buildIndex != 0) {
                    _instance = FindFirstObjectByType<GameManager>();
                }

                return _instance;
            }
            private set => _instance = value;
        }

        //---Networked Variables
        [Networked] public TickTimer BigStarRespawnTimer { get; set; }
        [Networked] public TickTimer GameStartTimer { get; set; }
        [Networked] public TickTimer GameEndTimer { get; set; }
        [Networked, Capacity(10)] public NetworkLinkedList<PlayerController> AlivePlayers => default;
        [Networked, Capacity(60)] public NetworkLinkedList<Fireball> PooledFireballs => default;
        [Networked] public float PlayerLoadingTimeoutTime { get; set; }
        [Networked] public float GameStartTime { get; set; } = -1;
        [Networked] public byte RealPlayerCount { get; set; }
        [Networked] public NetworkBool IsMusicEnabled { get; set; }
        [Networked] public Enums.GameState GameState { get; set; }
        [Networked] public ref NetworkBitArray AvailableStarSpawns => ref MakeRef<NetworkBitArray>();

        //---Properties
        public bool GameEnded => GameState == Enums.GameState.Ended;
        public bool PlaySounds { get; private set; } = false;

        private float? levelWidth, levelHeight, middleX, minX, minY, maxX, maxY;
        public float LevelWidth => levelWidth ??= levelWidthTile * tilemap.transform.localScale.x * tilemap.cellSize.x;
        public float LevelHeight => levelHeight ??= levelHeightTile * tilemap.transform.localScale.y * tilemap.cellSize.y;
        public float LevelMinX => minX ??= (levelMinTileX * tilemap.transform.localScale.x * tilemap.cellSize.x) + tilemap.transform.position.x;
        public float LevelMaxX => maxX ??= ((levelMinTileX + levelWidthTile) * tilemap.transform.localScale.x * tilemap.cellSize.x) + tilemap.transform.position.x;
        public float LevelMiddleX => middleX ??= LevelMinX + (LevelWidth * 0.5f);
        public float LevelMinY => minY ??= (levelMinTileY * tilemap.transform.localScale.y * tilemap.cellSize.y) + tilemap.transform.position.y;
        public float LevelMaxY => maxY ??= ((levelMinTileY + levelHeightTile) * tilemap.transform.localScale.y * tilemap.cellSize.y) + tilemap.transform.position.y;

        //---Serialized Variables
        [Header("Music")]
        public LoopingMusicData mainMusic;
        public LoopingMusicData invincibleMusic;
        public LoopingMusicData megaMushroomMusic;

        [Header("Level Configuration")]
        public int levelMinTileX;
        public int levelMinTileY;
        public int levelWidthTile;
        public int levelHeightTile;
        public bool loopingLevel = true, spawnBigPowerups = true, spawnVerticalPowerups = true;
        public string levelDesigner = "", composer = "", richPresenceId = "", levelTranslationKey = "";
        public Vector3 spawnpoint;
        [FormerlySerializedAs("size")] public float spawnCircleWidth = 1.39f;
        [FormerlySerializedAs("ySize")] public float spawnCircleHeight = 0.8f;
        [ColorUsage(false)] public Color levelUIColor = new(24, 178, 170);
        public bool hidePlayersOnMinimap = false;

        [Header("Camera")]
        public float cameraMinY;
        public float cameraHeightY;
        public float cameraMinX = -1000;
        public float cameraMaxX = 1000;

        [Header("Misc")]
        [SerializeField] private GameObject hud;
        [SerializeField] internal TeamScoreboard teamScoreboardElement;
        [SerializeField] private GameObject pauseUI;
        [SerializeField] private GameObject nametagPrefab;
        [SerializeField] public Tilemap tilemap, semisolidTilemap;
        [SerializeField] public GameObject objectPoolParent;
        [SerializeField] public TMP_Text winText;
        [SerializeField] public Animator winTextAnimator;

        //---Public Variables
        public readonly HashSet<NetworkObject> networkObjects = new();
        public GameObject[] starSpawns;
        public SingleParticleManager particleManager;
        public TeamManager teamManager = new();
        public Canvas nametagCanvas;
        public PlayerController localPlayer;
        public double gameStartTimestamp, gameEndTimestamp;
        public bool paused;

        public NetworkRNG random;
        public float gameEndTime;

        [NonSerialized] public KillableEntity[] enemies;
        [NonSerialized] public FloatingCoin[] coins;
        [HideInInspector] public TileBase[] sceneTiles;
        [HideInInspector] public ushort[] originalTiles;

        //---Private Variables
        private bool pauseStateLastFrame, optionsWereOpenLastFrame;
        private bool hurryUpSoundPlayed, endSoundPlayed;
        private bool calledAllPlayersLoaded;
        private float previousTimerRenderTime;
        private bool calledLoadingComplete;

        //---Components
        [Header("Misc Components")]
        [SerializeField] public TileManager tileManager;
        [SerializeField] public FadeOutManager fadeManager;
        [SerializeField] public SpectationManager spectationManager;
        [SerializeField] public LoopingMusicPlayer musicManager;
        [SerializeField] public AudioSource music, sfx;

        public void OnEnable() {
            ControlSystem.controls.UI.Pause.performed += OnPause;
            ControlSystem.controls.Debug.ToggleHUD.performed += OnToggleHud;

            NetworkHandler.OnPlayerLeft += OnPlayerLeft;
            OnAllPlayersLoaded += OurOnAllPlayersLoaded;
        }

        public void OnDisable() {
            ControlSystem.controls.UI.Pause.performed -= OnPause;
            ControlSystem.controls.Debug.ToggleHUD.performed -= OnToggleHud;

            NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
            OnAllPlayersLoaded -= OurOnAllPlayersLoaded;
        }

        public void OnValidate() {
            // Remove our cached values if we change something in editor.
            // We shouldn't have to worry about values changing mid-game ever.
            levelWidth = levelHeight = middleX = minX = minY = maxX = maxY = null;

            if (!tileManager) {
                tileManager = GetComponentInChildren<TileManager>();
            }
        }

        public void Awake() {
            Instance = this;

            //Make UI color translucent
            levelUIColor.a = .7f;
        }

        public async void Start() {
            // Handles spawning in editor
            if (string.IsNullOrEmpty(NetworkHandler.Runner.SessionInfo.Name)) {
                // Join a singleplayer room if we're not in one
                await NetworkHandler.CreateRoom(new() {
                    Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                }, GameMode.Single);
            }

            // Find objects in the scene
            starSpawns = GameObject.FindGameObjectsWithTag("StarSpawn");
            enemies = FindObjectsByType<KillableEntity>(FindObjectsSortMode.None).Where(ke => ke is not BulletBill).ToArray();
            coins = FindObjectsByType<FloatingCoin>(FindObjectsSortMode.None);

            nametagCanvas.gameObject.SetActive(Settings.Instance.GraphicsPlayerNametags);
        }

        public override void Spawned() {
            Instance = this;
            Runner.SetIsSimulated(Object, true);

            // By default, spectate. when we get assigned a player object, we disable it there.
            spectationManager.Spectating = true;

            if (Runner.IsSinglePlayer) {
                // Handle spawning in editor by spawning the room + player data objects
                Runner.Spawn(
                    PrefabList.Instance.PlayerDataHolder,
                    inputAuthority: Runner.LocalPlayer,
                    onBeforeSpawned: (runner, obj) => obj.GetComponent<PlayerData>().OnBeforeSpawned(Runner.LocalPlayer)
                );
            }

            if (GameStartTime <= 0 && !GameStartTimer.IsRunning) {
                // The game hasn't started.
                if (Runner.Topology == Topologies.Shared) {
                    // Create a local player
                    PlayerData data = Runner.GetLocalPlayerData();
                    if (!data.IsCurrentlySpectating) {
                        Runner.Spawn(data.GetCharacterData().prefab, spawnpoint, inputAuthority: data.Owner);
                    }
                }
            } else {
                // The game HAS already started.
                SetGameTimestamps();
                StartCoroutine(CallAllPlayersLoaded());
            }

            // Set up alternating music for the default stages
            if (!mainMusic) {
                byte musicIndex = (SessionData.Instance && SessionData.Instance.Object) ? SessionData.Instance.AlternatingMusicIndex : (byte) 0;
                int songs = ScriptableManager.Instance.alternatingStageMusic.Length;
                mainMusic = ScriptableManager.Instance.alternatingStageMusic[musicIndex % songs];
            }

            // Default loading timeout of 30 seconds
            if (!calledLoadingComplete && Runner.TryGetLocalPlayerData(out PlayerData data2)) {
                data2.Rpc_FinishedLoading();
                calledLoadingComplete = true;
            }

            PlayerLoadingTimeoutTime = Runner.SimulationTime + 30f;
        }

        public override void Render() {
            base.Render();

            if (!SessionData.Instance) {
                return;
            }

            if (GameState == Enums.GameState.Playing) {
                HandleMusic();
            }

            // Handle sound effects for the timer, if it's enabled
            if (SessionData.Instance.Timer > 0 && GameState >= Enums.GameState.Playing) {
                if (GameEndTimer.ExpiredOrNotRunning(Runner)) {
                    if (!endSoundPlayed) {
                        sfx.PlayOneShot(Enums.Sounds.UI_Countdown_1);
                    }
                    endSoundPlayed = true;
                } else if (PlaySounds) {
                    float timer = GameEndTimer.RemainingRenderTime(Runner) ?? 0f;

                    if ((int) (previousTimerRenderTime * 2) != (int) (timer * 2)) {
                        int second = (int) timer;

                        if (!hurryUpSoundPlayed && second < 60) {
                            // 60 second warning
                            hurryUpSoundPlayed = true;
                            sfx.PlayOneShot(Enums.Sounds.UI_HurryUp);
                        } else if (second < 3) {
                            // At 3 seconds, double speed
                            sfx.PlayOneShot(Enums.Sounds.UI_Countdown_0);
                        } else if (second < 10 && second != (int) previousTimerRenderTime) {
                            // 10 second "dings"
                            sfx.PlayOneShot(Enums.Sounds.UI_Countdown_0);
                        }
                    }
                    previousTimerRenderTime = timer;
                }
            }
        }

        public override void FixedUpdateNetwork() {
            if (!calledLoadingComplete && Runner.TryGetLocalPlayerData(out PlayerData data)) {
                data.Rpc_FinishedLoading();
                calledLoadingComplete = true;
            }

            if (!HasStateAuthority || GameEnded) {
                return;
            }

            switch (GameState) {
            case Enums.GameState.Loading: {
                if ((Runner.Tick % Runner.TickRate) == 0) {
                    CheckIfAllPlayersLoaded();
                }
                break;
            }
            case Enums.GameState.Starting: {
                if (GameStartTimer.Expired(Runner)) {
                    GameStartTimer = TickTimer.None;
                    Host_StartGame();
                }
                break;
            }
            }

            if (BigStarRespawnTimer.Expired(Runner)) {
                if (AttemptSpawnBigStar()) {
                    BigStarRespawnTimer = TickTimer.None;
                } else {
                    BigStarRespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
                }
            }

            if (previousTimerRenderTime > 0 && GameEndTimer.Expired(Runner)) {
                CheckForWinner();
                GameEndTimer = TickTimer.None;
                endSoundPlayed = false;
            }
        }

        public void Update() {
            pauseStateLastFrame = paused;
            optionsWereOpenLastFrame = GlobalController.Instance.optionsManager.gameObject.activeSelf;
        }

        public void CreateNametag(PlayerController controller) {
            GameObject nametag = Instantiate(nametagPrefab, nametagPrefab.transform.parent);
            nametag.GetComponent<UserNametag>().parent = controller;
            nametag.SetActive(true);
        }

        public ushort GetTileIdFromTileInstance(TileBase tile) {
            if (!tile) {
                return 0;
            }

            int index = Array.IndexOf(sceneTiles, tile);
            if (index == -1) {
                return 0;
            }

            return (ushort) index;
        }

        public TileBase GetTileInstanceFromTileId(ushort id) {
            return sceneTiles[id];
        }

        public void OnToggleHud(InputAction.CallbackContext context) {
            hud.SetActive(!hud.activeSelf);
        }

        public void OnPause(InputAction.CallbackContext context) {
            if (optionsWereOpenLastFrame) {
                return;
            }

            Pause(!pauseStateLastFrame);
        }

        public void Pause(bool newState) {
            if (paused == newState || GameState != Enums.GameState.Playing) {
                return;
            }

            paused = newState;
            pauseUI.SetActive(paused);
            sfx.PlayOneShot(Enums.Sounds.UI_Pause);
        }

        public void ForceUnpause() {
            paused = false;
            pauseUI.SetActive(false);
        }

        //---UI Callbacks
        public void PauseEndMatch() {
            if (!HasStateAuthority) {
                return;
            }

            pauseUI.SetActive(false);
            sfx.PlayOneShot(Enums.Sounds.UI_Decide);
            Rpc_EndGame(-1);
        }

        public void PauseQuitGame() {
            sfx.PlayOneShot(Enums.Sounds.UI_Decide);
            NetworkHandler.Instance.runner.Shutdown(false, ShutdownReason.Ok);
        }

        public void PauseOpenOptions() {
            GlobalController.Instance.optionsManager.OpenMenu();
            sfx.PlayOneShot(Enums.Sounds.UI_Decide);
        }

        public Vector3 GetSpawnpoint(int playerIndex, int players = -1) {
            if (players <= -1) {
                players = RealPlayerCount;
            }

            if (players == 0) {
                players = 1;
            }

            float comp = (float) playerIndex / players * 2 * Mathf.PI + (Mathf.PI / 2f) + (Mathf.PI / (2 * players));
            float scale = (2 - (players + 1f) / players) * spawnCircleWidth;

            Vector3 spawn = spawnpoint + new Vector3(Mathf.Sin(comp) * scale, Mathf.Cos(comp) * (players > 2f ? scale * spawnCircleHeight : 0), 0);
            Utils.Utils.WrapWorldLocation(ref spawn);
            return spawn;
        }

        public override void Despawned(NetworkRunner runner, bool hasState) {
            if (!HasStateAuthority || !hasState) {
                return;
            }

            DestroyNetworkObjects(runner);
        }

        public void BeforeTick() {
            // Seed RNG
            random = new(Runner.Tick);
        }

        /// <summary>
        /// Checks if a team has won, and calls Rpc_EndGame if one has.
        /// </summary>
        public bool CheckForWinner() {
            if (!HasStateAuthority || GameState != Enums.GameState.Playing) {
                return false;
            }

            int requiredStars = SessionData.Instance.StarRequirement;
            bool starGame = requiredStars != -1;

            bool hasFirstPlace = teamManager.HasFirstPlaceTeam(out int firstPlaceTeam, out int firstPlaceStars);
            int aliveTeams = teamManager.GetAliveTeamCount();
            bool timeUp = SessionData.Instance.Timer > 0 && GameEndTimer.ExpiredOrNotRunning(Runner);

            if (aliveTeams == 0) {
                // All teams dead, draw?
                Rpc_EndGame(-1);
                return true;
            }

            if (aliveTeams == 1 && RealPlayerCount > 1) {
                // One team left alive (and it's not a solo game), they win immediately.
                Rpc_EndGame(firstPlaceTeam);
                return true;
            }

            if (hasFirstPlace) {
                // We have a team that's clearly in first...
                if (starGame && (firstPlaceStars >= requiredStars || timeUp)) {
                    // And they have enough stars.
                    Rpc_EndGame(firstPlaceTeam);
                    return true;
                }
                // They don't have enough stars. wait 'till later
            }

            if (timeUp) {
                // Ran out of time, instantly end if DrawOnTimeUp is set
                if (SessionData.Instance.DrawOnTimeUp) {
                    // No one wins
                    Rpc_EndGame(-1);
                    return true;
                }

                if (RealPlayerCount <= 1) {
                    // One player, no overtime.
                    Rpc_EndGame(firstPlaceTeam);
                    return true;
                }

                // Keep playing into overtime.
            }

            // No winner, Keep playing
            return false;
        }

        /// <summary>
        /// Officially starts the game if all clients say that they're loaded.
        /// </summary>
        public void CheckIfAllPlayersLoaded() {
            // If we aren't the server, don't bother checking. We can't start the game regardless.
            if (!Runner || !HasStateAuthority || GameState != Enums.GameState.Loading) {
                return;
            }

            if (Runner.IsSinglePlayer) {
                // Waiting for our PlayerData to be valid...
                if (SessionData.Instance.PlayerDatas.Count == 0) {
                    return;
                }
            } else {
                if (PlayerLoadingTimeoutTime < Runner.SimulationTime) {
                    // https://youtu.be/XX5eMgeA_R4 kick any players that didn't load in time...
                    foreach ((PlayerRef player, PlayerData pd) in SessionData.Instance.PlayerDatas) {
                        if (!pd.IsCurrentlySpectating && !pd.IsLoaded) {
                            pd.IsCurrentlySpectating = true;
                            SessionData.Instance.Disconnect(player);
                        }
                    }
                } else {
                    // Check if any player is still loading
                    if (SessionData.Instance.PlayerDatas.Any(kvp => !kvp.Value.IsCurrentlySpectating && !kvp.Value.IsLoaded)) {
                        return;
                    }
                }
            }

            // Everyone is loaded, officially start the game.
            GameState = Enums.GameState.Starting;
            SceneManager.SetActiveScene(gameObject.scene);
            GameStartTimer = TickTimer.CreateFromSeconds(Runner, Runner.IsSinglePlayer ? 0.2f : 5.7f);

            // Find out how many players we have
            RealPlayerCount = (byte)
                SessionData.Instance.PlayerDatas
                    .Select(kvp => kvp.Value)
                    .Where(pd => !pd.IsCurrentlySpectating)
                    .Count();

            // Assign spawnpoints (SpawnpointIds)
            List<int> playerIds = Enumerable.Range(0, RealPlayerCount).ToList();
            foreach ((_, PlayerData data) in SessionData.Instance.PlayerDatas) {
                data.IsLoaded = false;

                if (data.IsCurrentlySpectating) {
                    data.SpawnpointId = -1;
                } else {
                    int index = UnityEngine.Random.Range(0, playerIds.Count);
                    int newPlayerId = playerIds[index];
                    playerIds.RemoveAt(index);
                    data.SpawnpointId = (sbyte) newPlayerId;
                }
            }

            if (Runner.IsSinglePlayer || Runner.Topology == Topologies.ClientServer) {
                foreach ((PlayerRef player, PlayerData data) in SessionData.Instance.PlayerDatas) {
                    if (data.IsCurrentlySpectating) {
                        continue;
                    }

                    Runner.Spawn(data.GetCharacterData().prefab, spawnpoint, inputAuthority: player);
                }
            }

            // Create pooled Fireball instances (max of 6 per player)
            for (int i = 0; i < RealPlayerCount * 6; i++) {
                Runner.Spawn(PrefabList.Instance.Obj_Fireball);
            }

            // Tell everyone else to start the game
            StartCoroutine(CallLoadingComplete(2));
        }

        public void BumpBlock(short x, short y, TileBase oldTile, TileBase newTile, bool downwards, Vector2 offset, bool spawnCoin, NetworkPrefabRef spawnPrefab, int? tick = null, byte? counter = null) {
            Vector2Int loc = new(x, y);
            Vector3 spawnLocation = Utils.Utils.TilemapToWorldPosition(loc) + OneFourth;

            // TODO: find a way to predict these.
            if (HasStateAuthority) {
                Runner.Spawn(PrefabList.Instance.Obj_BlockBump, spawnLocation, onBeforeSpawned: (runner, obj) => {
                    obj.GetComponentInChildren<BlockBump>().OnBeforeSpawned(loc, oldTile, newTile, spawnPrefab, downwards, spawnCoin, tick ?? Runner.Tick, offset);
                });
            }

            tileManager.SetTile(loc, null);
        }

        //---Callbacks
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
            // Take over the player if they are still alive
            if (Object.HasStateAuthority) {
                foreach (PlayerController pl in AlivePlayers) {
                    if (pl.Data.Owner == player) {
                        pl.Object.RequestStateAuthority();
                    }
                }
            }

            CheckIfAllPlayersLoaded();
            // CheckForWinner();
        }

        //---RPCs
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_EndGame(int team) {
            gameEndTime = Runner.SimulationTime;
            StartCoroutine(EndGame(team));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_LoadingComplete() {
            if (!calledAllPlayersLoaded) {
                OnAllPlayersLoaded?.Invoke();
            }

            calledAllPlayersLoaded = true;
        }

        //---Helpers
        private void Host_StartGame() {
            // Respawn enemies
            foreach (KillableEntity enemy in enemies) {
                enemy.RespawnEntity();
            }

            // Start "WaitForGameStart" objects
            foreach (var wfgs in FindObjectsByType<NetworkBehaviour>(FindObjectsSortMode.None).Where(nb => nb is IWaitForGameStart)) {
                ((IWaitForGameStart) wfgs).AttemptExecute(wfgs.Object);
            }

            // Spawn the initial Big Star
            AttemptSpawnBigStar();

            Rpc_StartGame();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_StartGame() {
            // Play start jingle
            sfx.PlayOneShot(Enums.Sounds.UI_StartGame);

            // Respawn our player, if we have one.
            foreach (PlayerController player in AlivePlayers) {
                if (player.HasStateAuthority) {
                    player.PreRespawn();
                }
            }

            StartCoroutine(WaitToStartGame());
        }

        private IEnumerator WaitToStartGame() {
            GameStartTime = Runner.SimulationTime + 1.3f;

            yield return new WaitForSecondsRealtime(1.3f);

            // Keep track of game timestamps
            GameState = Enums.GameState.Playing;
            GameStartTime = Runner.SimulationTime;
            IsMusicEnabled = true;
            musicManager.Play(mainMusic);
            PlaySounds = true;

            // Start timer
            int timer = SessionData.Instance.Timer;
            if (timer > 0) {
                GameEndTimer = TickTimer.CreateFromSeconds(Runner, timer * 60);
            }

            // Update Discord RPC status
            SetGameTimestamps();
            GlobalController.Instance.discordController.UpdateActivity();
        }

        private IEnumerator EndGame(int winningTeam) {
            // TODO: Clean this up, massively.

            GameState = Enums.GameState.Ended;
            IsMusicEnabled = false;
            endSoundPlayed = true;

            // End "WaitForGameEnd" objects
            foreach (var wfge in FindObjectsByType<NetworkBehaviour>(FindObjectsSortMode.None).Where(nb => nb is IWaitForGameEnd)) {
                ((IWaitForGameEnd) wfge).AttemptExecute(wfge.Object);
            }

            ForceUnpause();
            musicManager.Stop();

            yield return new WaitForSecondsRealtime(1);

            PlaySounds = false;

            TranslationManager tm = GlobalController.Instance.translationManager;
            bool draw = winningTeam == -1;
            string resultText;
            string winner = null;
            if (draw) {
                resultText = tm.GetTranslation("ui.result.draw");
            } else {
                if (SessionData.Instance.Teams) {
                    Team team = ScriptableManager.Instance.teams[winningTeam];
                    winner = team.displayName;
                    resultText = tm.GetTranslationWithReplacements("ui.result.teamwin", "team", winner);
                } else {
                    string username = teamManager.GetTeamMembers(winningTeam).First().Data.GetNickname();
                    winner = username;
                    resultText = tm.GetTranslationWithReplacements("ui.result.playerwin", "playername", winner);
                }

                if (HasStateAuthority) {
                    foreach (PlayerController player in teamManager.GetTeamMembers(winningTeam)) {
                        player.Data.Wins++;
                    }
                }
            }
            winText.text = resultText;

            PlayerData local = Runner.GetLocalPlayerData();
            bool win = !draw && (winningTeam == local.Team || local.IsCurrentlySpectating);
            int secondsUntilMenu = draw ? 5 : 4;

            Enums.Sounds resultSound;
            string resultTrigger;

            if (draw) {
                resultSound = Enums.Sounds.UI_Match_Draw;
                resultTrigger = "startNegative";
            } else if (win) {
                resultSound = Enums.Sounds.UI_Match_Win;
                resultTrigger = "start";
            } else {
                resultSound = Enums.Sounds.UI_Match_Lose;
                resultTrigger = "startNegative";
            }

            music.PlayOneShot(resultSound);
            winTextAnimator.SetTrigger(resultTrigger);

            if (draw) {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.draw", color: ChatManager.Red);
            } else if (SessionData.Instance.Teams) {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.team", color: ChatManager.Red, "team", winner);
            } else {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.ended.player", color: ChatManager.Red, "playername", winner);
            }

            // Return back to the main menu
            yield return new WaitForSecondsRealtime(secondsUntilMenu);

            DestroyNetworkObjects(Runner);

            if (HasStateAuthority) {
                // Handle resetting player states for the next game
                foreach (PlayerRef player in Runner.ActivePlayers) {
                    PlayerData data = player.GetPlayerData();

                    // Set IsLoaded to false
                    data.IsLoaded = false;

                    // Set spectating state to false
                    data.IsCurrentlySpectating = false;

                    // Move people without teams into a valid teams range
                    if (SessionData.Instance.Teams) {
                        data.Team = (sbyte) Mathf.Clamp(data.Team, 0, ScriptableManager.Instance.teams.Length);
                    }
                }

                SessionData.Instance.AlternatingMusicIndex++;
            }

            SessionData.Instance.SetGameStarted(false);
            SessionData.Instance.GameStartTimer = TickTimer.None;

            yield return new WaitForSecondsRealtime(0.25f);

            if (HasStateAuthority) {
                Runner.LoadScene(SceneRef.FromIndex(0), LoadSceneMode.Single);
            }
        }

        private void HandleMusic() {
            if (!SessionData.Instance) {
                return;
            }

            if (!musicManager.IsPlaying) {
                return;
            }

            bool invincible = false;
            bool mega = false;
            bool speedup = false;

            foreach (var player in AlivePlayers) {
                if (!player || !player.cameraController.IsControllingCamera) {
                    continue;
                }

                mega |= Settings.Instance.audioSpecialPowerupMusic.HasFlag(Enums.SpecialPowerupMusic.MegaMushroom) && player.State == Enums.PowerupState.MegaMushroom && player.MegaStartTimer.ExpiredOrNotRunning(Runner);
                invincible |= Settings.Instance.audioSpecialPowerupMusic.HasFlag(Enums.SpecialPowerupMusic.Starman) && player.IsStarmanInvincible;
            }

            speedup |= SessionData.Instance.Timer > 0 && ((GameEndTimer.RemainingTime(Runner) ?? 0f) < 60f);
            speedup |= teamManager.GetFirstPlaceStars() + 1 >= SessionData.Instance.StarRequirement;

            if (!speedup && SessionData.Instance.Lives > 0) {
                int playersWithOneLife = 0;
                int playerCount = 0;
                foreach (var player in AlivePlayers) {
                    if (!player || player.OutOfLives) {
                        continue;
                    }

                    if (player.Lives == 1) {
                        playersWithOneLife++;
                    }

                    playerCount++;
                }

                // Also speed up the music if:
                // A: two players left, at least one has one life
                // B: three+ players left, all have one life
                speedup |= (playerCount <= 2 && playersWithOneLife > 0) || (playersWithOneLife >= playerCount);
            }

            if (mega) {
                musicManager.Play(megaMushroomMusic);
            } else if (invincible) {
                musicManager.Play(invincibleMusic);
            } else {
                musicManager.Play(mainMusic);
            }

            musicManager.FastMusic = speedup;
        }

        /// <summary>
        /// Spawns a Big Star, if we can find a valid spawnpoint.
        /// </summary>
        /// <returns>If the star successfully spawned</returns>
        private bool AttemptSpawnBigStar() {
            if (!HasStateAuthority) {
                return true;
            }

            for (int attempt = 0; attempt < starSpawns.Length; attempt++) {
                int validSpawns = starSpawns.Length - AvailableStarSpawns.UnsetBitCount();

                if (validSpawns <= 0) {
                    ResetAvailableStarSpawns();
                    validSpawns = starSpawns.Length;
                }

                int nthSpawn = random.RangeExclusive(0, validSpawns);
                AvailableStarSpawns.GetNthSetBitIndex(nthSpawn, out int im);
                if (AvailableStarSpawns.GetNthSetBitIndex(nthSpawn, out int index)) {

                    Vector3 spawnPos = starSpawns[index].transform.position;
                    AvailableStarSpawns[index] = false;

                    if (Runner.GetPhysicsScene2D().OverlapCircle(spawnPos, 2.5f, Layers.MaskOnlyPlayers)) {
                        // A player is too close to this spawn. Don't spawn.
                        continue;
                    }

                    // Valid spawn
                    Runner.Spawn(PrefabList.Instance.Obj_BigStar, spawnPos, onBeforeSpawned: (runner, obj) => {
                        obj.GetComponent<BigStar>().OnBeforeSpawned(0, true, false);
                    });
                    return true;
                }
            }

            // This should hopefully never happen...
            return false;
        }

        private void ResetAvailableStarSpawns() {
            AvailableStarSpawns.RawSet(unchecked(~0UL));
        }

        /// <summary>
        /// Sets the game timestamps for Discord RPC
        /// </summary>
        public void SetGameTimestamps() {
            if (!SessionData.Instance || !Object) {
                return;
            }

            double now = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
            float secondsSinceStart = Runner.SimulationTime - GameStartTime;
            gameStartTimestamp = now - secondsSinceStart;

            int timer = SessionData.Instance.Timer;
            if (timer > 0) {
                gameEndTimestamp = gameStartTimestamp + (timer * 60);
            }
        }

        private IEnumerator CallLoadingComplete(float seconds) {
            yield return new WaitForSeconds(seconds);
            Rpc_LoadingComplete();
        }

        private IEnumerator CallAllPlayersLoaded() {
            yield return new WaitForSeconds(2f);
            if (!calledAllPlayersLoaded) {
                OnAllPlayersLoaded?.Invoke();
            }

            calledAllPlayersLoaded = true;
        }

        private void DestroyNetworkObjects(NetworkRunner runner) {
            // Remove all networked objects. Fusion doesn't do this for us, unlike PUN.
            foreach (var obj in networkObjects) {
                if (obj) {
                    runner.Despawn(obj);
                }
            }
        }

        private void OurOnAllPlayersLoaded() {
            foreach (var player in AlivePlayers) {
                teamManager.AddPlayer(player);
            }

            teamScoreboardElement.OnAllPlayersLoaded();
        }

#if UNITY_EDITOR
        //---Debug
        [SerializeField] private int DebugSpawns = 10;
        private static readonly Color StarSpawnTint = new(1f, 1f, 1f, 0.5f), StarSpawnBox = new(1f, 0.9f, 0.2f, 0.2f);
        public void OnDrawGizmos() {
            if (!tilemap) {
                return;
            }

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

                    if (tile is CoinTile) {
                        Gizmos.DrawIcon(Utils.Utils.TilemapToWorldPosition(loc, this) + OneFourth, "coin");
                    } else if (tile is PowerupTile) {
                        Gizmos.DrawIcon(Utils.Utils.TilemapToWorldPosition(loc, this) + OneFourth, "powerup");
                    }
                }
            }

            Gizmos.color = StarSpawnBox;
            foreach (GameObject starSpawn in GameObject.FindGameObjectsWithTag("StarSpawn")) {
                Gizmos.DrawCube(starSpawn.transform.position, Vector3.one);
                Gizmos.DrawIcon(starSpawn.transform.position, "star", true, StarSpawnTint);
            }

            Gizmos.color = Color.black;
            for (int x = 0; x < Mathf.CeilToInt(levelWidthTile / 16f); x++) {
                for (int y = 0; y < Mathf.CeilToInt(levelHeightTile / 16f); y++) {
                    Gizmos.DrawWireCube(new(LevelMinX + (x * 8f) + 4f, LevelMinY + (y * 8f) + 4f), new(8, 8, 0));
                }
            }
        }
#endif
    }
}
