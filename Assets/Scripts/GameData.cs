using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Entities;
using NSMB.Entities.Collectable;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Translation;
using NSMB.Utils;

namespace NSMB.Game {
    public class GameData : NetworkBehaviour, IBeforeTick {

        //---Static Variables
        public static event Action OnAllPlayersLoaded;
        private static readonly Vector3 OneFourth = new(0.25f, 0.25f, 0f);
        private static GameData _instance;
        public static GameData Instance {
            get {
                if (_instance)
                    return _instance;

                if (SceneManager.GetActiveScene().buildIndex != 0)
                    _instance = FindObjectOfType<GameData>();

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
        [Networked] public float GameStartTime { get; set; } = -1;
        [Networked] public byte RealPlayerCount { get; set; }
        [Networked] public NetworkBool IsMusicEnabled { get; set; }
        [Networked] public Enums.GameState GameState { get; set; }
        [Networked] public ref NetworkBitArray AvailableStarSpawns => ref MakeRef<NetworkBitArray>();
        [Networked] private byte PredictionCounter { get; set; }

        //---Public Variables
        public NetworkRNG random;
        public float gameEndTime;
        public bool dontPlaySounds;

        //---Properties
        public bool GameEnded => GameState == Enums.GameState.Ended;
        public bool PlaySounds => startMusicTimer.ExpiredOrNotRunning(Runner);

        //---Private Variables
        private readonly HashSet<NetworkObject> networkObjects = new();

        private GameManager gm;
        private AudioSource audioSfx;
        private AudioSource audioMusic;

        private TickTimer startMusicTimer;
        private bool hurryUpSoundPlayed, endSoundPlayed;
        private bool calledAllPlayersLoaded;

        //---Lifetime
        public void OnEnable() {
            NetworkHandler.OnShutdown += OnShutdown;
            NetworkHandler.OnPlayerLeft += OnPlayerLeft;
            OnAllPlayersLoaded += OurOnAllPlayersLoaded;
        }

        public void OnDisable() {
            NetworkHandler.OnShutdown -= OnShutdown;
            NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
            OnAllPlayersLoaded -= OurOnAllPlayersLoaded;
        }

        public void Awake() {
            gm = GameManager.Instance;
            audioSfx = gm.sfx;
            audioMusic = gm.music;
        }

        public override void Spawned() {
            Instance = this;

            // By default, spectate. when we get assigned a player object, we disable it there.
            gm.spectationManager.Spectating = true;

            if (Runner.IsServer && Runner.IsSinglePlayer && !Runner.IsResume) {
                // Handle spawning in editor by spawning the room + player data objects
                Runner.Spawn(PrefabList.Instance.SessionDataHolder);
                NetworkObject localData = Runner.Spawn(PrefabList.Instance.PlayerDataHolder, inputAuthority: Runner.LocalPlayer, onBeforeSpawned: (runner, obj) => obj.GetComponent<PlayerData>().OnBeforeSpawned());
                Runner.SetPlayerObject(Runner.LocalPlayer, localData);
            }

            if (GameStartTime <= 0 && !Runner.IsResume && !GameStartTimer.IsRunning) {
                // The game hasn't started.
                // Tell our host that we're done loading
                PlayerData localData = Runner.GetLocalPlayerData();
                localData.Rpc_FinishedLoading();
            } else {
                // The game HAS already started.
                SetGameTimestamps();
                StartCoroutine(CallAllPlayersLoaded());
                startMusicTimer = TickTimer.CreateFromSeconds(Runner, 1.8f);
            }

            // Set up alternating music for the default stages
            if (!gm.mainMusic) {
                byte musicIndex = SessionData.Instance.AlternatingMusicIndex;
                int songs = ScriptableManager.Instance.alternatingStageMusic.Length;
                gm.mainMusic = ScriptableManager.Instance.alternatingStageMusic[musicIndex % songs];
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState) {
            if (!runner.IsServer || !hasState)
                return;

            // Remove all networked objects. Fusion doesn't do this for us, unlike PUN.
            foreach (var obj in networkObjects) {
                if (obj)
                    runner.Despawn(obj);
            }

            networkObjects.Clear();
        }

        public void BeforeTick() {
            // Seed RNG
            random = new(Runner.Simulation.Tick);
        }

        public override void FixedUpdateNetwork() {
            if (GameEnded)
                return;

            if (Runner.IsServer && GameState == Enums.GameState.Loading && (Runner.Tick % Runner.Simulation.Config.TickRate) == 0) {
                CheckIfAllPlayersLoaded();
            }

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

            if (startMusicTimer.Expired(Runner)) {
                startMusicTimer = TickTimer.None;
                IsMusicEnabled = true;

                // Start timer
                int timer = SessionData.Instance.Timer;
                if (timer > 0)
                    GameEndTimer = TickTimer.CreateFromSeconds(Runner, timer * 60 + 1);
            }

            if (IsMusicEnabled && startMusicTimer.ExpiredOrNotRunning(Runner))
                HandleMusic();

            if (Runner.IsForward) {
                // Handle sound effects for the timer, if it's enabled
                if (GameEndTimer.IsRunning) {
                    if (GameEndTimer.Expired(Runner)) {
                        if (!endSoundPlayed)
                            audioSfx.PlayOneShot(Enums.Sounds.UI_Countdown_1);
                        endSoundPlayed = true;
                    } else {
                        int tickrate = Runner.Config.Simulation.TickRate;
                        int remainingTicks = GameEndTimer.RemainingTicks(Runner) ?? 0;

                        if (!hurryUpSoundPlayed && remainingTicks < 61 * tickrate) {
                            //60 second warning
                            hurryUpSoundPlayed = true;
                            audioSfx.PlayOneShot(Enums.Sounds.UI_HurryUp);
                        } else if (remainingTicks <= (10 * tickrate)) {
                            //10 second "dings"
                            if (remainingTicks % tickrate == 0)
                                audioSfx.PlayOneShot(Enums.Sounds.UI_Countdown_0);
                            //at 3 seconds, double speed
                            else if (remainingTicks < (3 * tickrate) && remainingTicks % (tickrate / 2) == 0)
                                audioSfx.PlayOneShot(Enums.Sounds.UI_Countdown_0);
                        }
                    }
                }
            }

            if (GameEndTimer.Expired(Runner)) {
                CheckForWinner();
                GameEndTimer = TickTimer.None;
            }
        }

        /// <summary>
        /// Checks if a team has won, and calls Rpc_EndGame if one has.
        /// </summary>
        public bool CheckForWinner() {
            if (GameState != Enums.GameState.Playing || !Runner.IsServer)
                return false;

            TeamManager teamManager = gm.teamManager;

            int requiredStars = SessionData.Instance.StarRequirement;
            bool starGame = requiredStars != -1;

            bool hasFirstPlace = teamManager.HasFirstPlaceTeam(out int firstPlaceTeam, out int firstPlaceStars);
            int aliveTeams = teamManager.GetAliveTeamCount();
            bool timeUp = SessionData.Instance.Timer > 0 && GameEndTimer.ExpiredOrNotRunning(Runner);

            if (aliveTeams == 0) {
                // All teams dead, draw?
                Rpc_EndGame(PlayerRef.None);
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
                    Rpc_EndGame(PlayerRef.None);
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

                Runner.Spawn(data.GetCharacterData().prefab, gm.spawnpoint, inputAuthority: player, onBeforeSpawned: (runner, obj) => {
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

        public Vector3 GetSpawnpoint(int playerIndex, int players = -1) {
            if (players <= -1)
                players = RealPlayerCount;
            if (players == 0)
                players = 1;

            return GameManager.Instance.GetSpawnpoint(playerIndex, players);
        }

        public void BumpBlock(short x, short y, TileBase oldTile, TileBase newTile, bool downwards, Vector2 offset, bool spawnCoin, NetworkPrefabRef spawnPrefab) {
            Vector2Int loc = new(x, y);

            Vector3 spawnLocation = Utils.Utils.TilemapToWorldPosition(loc) + OneFourth;

            NetworkObject bumper = Runner.Spawn(PrefabList.Instance.Obj_BlockBump, spawnLocation, onBeforeSpawned: (runner, obj) => {
                obj.GetComponentInChildren<BlockBump>().OnBeforeSpawned(loc, oldTile, newTile, spawnPrefab, downwards, spawnCoin, offset);
            }, predictionKey: new() { Byte1 = (byte) Runner.Tick, Byte0 = PredictionCounter++ });

            GameManager.Instance.TileManager.SetTile(loc, null);
        }


        //---Callbacks
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

        //---RPCs

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_EndGame(int team) {
            gameEndTime = Runner.SimulationTime;
            StartCoroutine(EndGame(team));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_LoadingComplete() {
            if (!calledAllPlayersLoaded)
                OnAllPlayersLoaded?.Invoke();
            calledAllPlayersLoaded = true;
        }

        //---Helpers
        private void StartGame() {
            GameState = Enums.GameState.Playing;

            // Respawn players
            foreach (PlayerController player in AlivePlayers)
                player.PreRespawn();

            // Play start jingle
            if (Runner.IsForward)
                audioSfx.PlayOneShot(Enums.Sounds.UI_StartGame);

            startMusicTimer = TickTimer.CreateFromSeconds(Runner, 1.3f);

            // Respawn enemies
            foreach (KillableEntity enemy in gm.enemies)
                enemy.RespawnEntity();

            // Start "WaitForGameStart" objects
            foreach (var wfgs in FindObjectsOfType<WaitForGameStart>())
                wfgs.AttemptExecute();

            // Spawn the initial Big Star
            AttemptSpawnBigStar();

            // Keep track of game timestamps
            GameStartTime = Runner.SimulationTime;
            SetGameTimestamps();

            // Update Discord RPC status
            if (Runner.IsForward)
                GlobalController.Instance.discordController.UpdateActivity();
        }

        private IEnumerator EndGame(int winningTeam) {
            //TODO: Clean this up, massively.

            GameState = Enums.GameState.Ended;
            IsMusicEnabled = false;

            gm.Pause(false);
            gm.musicManager.Stop();

            yield return new WaitForSecondsRealtime(1);

            dontPlaySounds = true;

            TeamManager teamManager = gm.teamManager;
            TranslationManager tm = GlobalController.Instance.translationManager;
            bool draw = winningTeam == -1;
            string resultText;
            if (draw) {
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
            gm.winText.text = resultText;

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

            audioMusic.PlayOneShot(resultSound);
            gm.winTextAnimator.SetTrigger(resultTrigger);

            // Return back to the main menu
            yield return new WaitForSecondsRealtime(secondsUntilMenu);

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

                SessionData.Instance.AlternatingMusicIndex++;
            }

            SessionData.Instance.SetGameStarted(false);
            SessionData.Instance.GameStartTimer = TickTimer.None;
            Runner.SetActiveScene(0);
        }

        private void HandleMusic() {
            bool invincible = false;
            bool mega = false;
            bool speedup = false;

            foreach (var player in AlivePlayers) {
                if (!player)
                    continue;

                mega |= player.State == Enums.PowerupState.MegaMushroom && player.MegaStartTimer.ExpiredOrNotRunning(Runner);
                invincible |= player.IsStarmanInvincible;
            }

            speedup |= SessionData.Instance.Timer > 0 && ((GameEndTimer.RemainingTime(Runner) ?? 0f) < 60f);
            speedup |= gm.teamManager.GetFirstPlaceStars() + 1 >= SessionData.Instance.StarRequirement;

            if (!speedup) {
                // Also speed up the music if:
                // A: two players left, at least one has one life
                // B: three+ players left, all have one life
                int playersWithOneLife = 0;
                int playerCount = 0;
                foreach (var player in AlivePlayers) {
                    if (!player) continue;
                    if (player.Lives == 1 || player.Lives == 0) playersWithOneLife++;

                    playerCount++;
                }
                speedup |= (playersWithOneLife <= 2 && playersWithOneLife != 0) || playersWithOneLife >= playerCount;
            }

            LoopingMusicPlayer musicManager = gm.musicManager;

            if (mega) {
                musicManager.Play(gm.megaMushroomMusic);
            } else if (invincible) {
                musicManager.Play(gm.invincibleMusic);
            } else {
                musicManager.Play(gm.mainMusic);
            }

            musicManager.FastMusic = speedup;
        }

        /// <summary>
        /// Spawns a Big Star, if we can find a valid spawnpoint.
        /// </summary>
        /// <returns>If the star successfully spawned</returns>
        private bool AttemptSpawnBigStar() {

            GameObject[] starSpawns = gm.starSpawns;

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

                    if (Runner.GetPhysicsScene2D().OverlapCircle(spawnPos, 3, Layers.MaskOnlyPlayers)) {
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
            AvailableStarSpawns.RawSet(unchecked((ulong) ~0L));
        }

        /// <summary>
        /// Sets the game timestamps for Discord RPC
        /// </summary>
        private void SetGameTimestamps() {
            double now = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
            float secondsSinceStart = Runner.SimulationTime - GameStartTime;
            gm.gameStartTimestamp = now - secondsSinceStart;

            int timer = SessionData.Instance.Timer;
            if (timer > 0)
                gm.gameEndTimestamp = gm.gameStartTimestamp + (timer * 60);
        }

        private IEnumerator CallLoadingComplete(float seconds) {
            yield return new WaitForSeconds(seconds);
            Rpc_LoadingComplete();
        }

        private IEnumerator CallAllPlayersLoaded() {
            yield return new WaitForSeconds(1f);
            if (!calledAllPlayersLoaded)
                OnAllPlayersLoaded?.Invoke();
            calledAllPlayersLoaded = true;
        }

        private void OurOnAllPlayersLoaded() {
            foreach (var player in AlivePlayers)
                GameManager.Instance.teamManager.AddPlayer(player);
        }
    }
}
