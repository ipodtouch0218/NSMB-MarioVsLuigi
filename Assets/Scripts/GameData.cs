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
    public class GameData : NetworkBehaviour {

        //---Static Variables
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
        private static GameManager GameManager => GameManager.Instance;
        public static event Action OnAllPlayersLoaded;

        //---Networked Variables
        [Networked] public TickTimer BigStarRespawnTimer { get; set; }
        [Networked(OnChanged = nameof(OnGameStartTimerChanged))] public TickTimer GameStartTimer { get; set; }
        [Networked] public TickTimer GameEndTimer { get; set; }
        [Networked, Capacity(10)] public NetworkLinkedList<PlayerController> AlivePlayers => default;
        [Networked, Capacity(60)] public NetworkLinkedList<FireballMover> PooledFireballs => default;
        [Networked] public float GameStartTime { get; set; } = -1;
        [Networked] public byte RealPlayerCount { get; set; }
        [Networked] public NetworkBool IsMusicEnabled { get; set; }
        [Networked] public Enums.GameState GameState { get; set; }
        [Networked] public ref NetworkBitArray AvailableStarSpawns => ref MakeRef<NetworkBitArray>();
        [Networked] private byte PredictionCounter { get; set; }

        //---Public Variables
        public NetworkRNG Random;
        public float gameEndTime;

        //---Private Variables
        private readonly HashSet<NetworkObject> networkObjects = new();
        private TickTimer StartMusicTimer;
        private bool hurryUpSoundPlayed, endSoundPlayed;

        //---Properties
        public bool GameEnded => GameState == Enums.GameState.Ended;
        public AudioSource AudioSfx => GameManager.sfx;
        public AudioSource AudioMusic => GameManager.music;

        //---Lifetime
        public void OnEnable() {
            NetworkHandler.OnShutdown += OnShutdown;
            NetworkHandler.OnPlayerLeft += OnPlayerLeft;
        }

        public void OnDisable() {
            NetworkHandler.OnShutdown -= OnShutdown;
            NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
        }

        public override void Spawned() {
            Instance = this;

            // By default, spectate. when we get assigned a player object, we disable it there.
            GameManager.spectationManager.Spectating = true;

            // Enable player controls
            Runner.ProvideInput = true;

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

            // Set up alternating music for the default stages
            if (!GameManager.mainMusic) {
                byte musicIndex = SessionData.Instance.AlternatingMusicIndex;
                int songs = ScriptableManager.Instance.alternatingStageMusic.Length;
                GameManager.mainMusic = ScriptableManager.Instance.alternatingStageMusic[musicIndex % songs];
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState) {
            if (!runner.IsServer || !hasState)
                return;

            // Remove all networked objects. Fusion doesn't do this for us, unlike PUN.
            foreach (var obj in networkObjects)
                if (obj)
                    runner.Despawn(obj);

            networkObjects.Clear();
        }

        public override void FixedUpdateNetwork() {
            // Seed RNG for this tick
            Random = new(Runner.Simulation.Tick);

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

            if (StartMusicTimer.Expired(Runner)) {
                StartMusicTimer = TickTimer.None;
                IsMusicEnabled = true;

                // Start timer
                int timer = SessionData.Instance.Timer;
                if (timer > 0)
                    GameEndTimer = TickTimer.CreateFromSeconds(Runner, timer * 60 + 1);
            }

            if (IsMusicEnabled)
                HandleMusic();

            if (Runner.IsForward) {
                // Handle sound effects for the timer, if it's enabled
                if (GameEndTimer.IsRunning) {
                    if (GameEndTimer.Expired(Runner)) {
                        if (!endSoundPlayed)
                            AudioSfx.PlayOneShot(Enums.Sounds.UI_Countdown_1);
                        endSoundPlayed = true;
                    } else {
                        int tickrate = Runner.Config.Simulation.TickRate;
                        int remainingTicks = GameEndTimer.RemainingTicks(Runner) ?? 0;

                        if (!hurryUpSoundPlayed && remainingTicks < 61 * tickrate) {
                            //60 second warning
                            hurryUpSoundPlayed = true;
                            AudioSfx.PlayOneShot(Enums.Sounds.UI_HurryUp);
                        } else if (remainingTicks <= (10 * tickrate)) {
                            //10 second "dings"
                            if (remainingTicks % tickrate == 0)
                                AudioSfx.PlayOneShot(Enums.Sounds.UI_Countdown_0);
                            //at 3 seconds, double speed
                            else if (remainingTicks < (3 * tickrate) && remainingTicks % (tickrate / 2) == 0)
                                AudioSfx.PlayOneShot(Enums.Sounds.UI_Countdown_0);
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
        public void CheckForWinner() {
            if (GameState != Enums.GameState.Playing || !Runner.IsServer)
                return;

            TeamManager teamManager = GameManager.teamManager;

            int requiredStars = SessionData.Instance.StarRequirement;
            bool starGame = requiredStars != -1;

            bool hasFirstPlace = teamManager.HasFirstPlaceTeam(out int firstPlaceTeam, out int firstPlaceStars);
            int aliveTeams = teamManager.GetAliveTeamCount();
            bool timeUp = SessionData.Instance.Timer > 0 && GameEndTimer.ExpiredOrNotRunning(Runner);

            if (aliveTeams == 0) {
                // All teams dead, draw?
                Rpc_EndGame(PlayerRef.None);
                return;
            }

            if (aliveTeams == 1 && RealPlayerCount > 1) {
                // One team left alive (and it's not a solo game), they win immediately.
                Rpc_EndGame(firstPlaceTeam);
                return;
            }

            if (hasFirstPlace) {
                // We have a team that's clearly in first...
                if (starGame && (firstPlaceStars >= requiredStars || timeUp)) {
                    // And they have enough stars.
                    Rpc_EndGame(firstPlaceTeam);
                    return;
                }
                // They don't have enough stars. wait 'till later
            }

            if (timeUp) {
                // Ran out of time, instantly end if DrawOnTimeUp is set
                if (SessionData.Instance.DrawOnTimeUp) {
                    // No one wins
                    Rpc_EndGame(PlayerRef.None);
                    return;
                }

                if (RealPlayerCount <= 1) {
                    // One player, no overtime.
                    Rpc_EndGame(firstPlaceTeam);
                    return;
                }

                // Keep playing into overtime.
            }

            // No winner, Keep playing
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

                Runner.Spawn(data.GetCharacterData().prefab, GameManager.spawnpoint, inputAuthority: player, onBeforeSpawned: (runner, obj) => {
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

            float comp = (float) playerIndex / players * 2.5f * Mathf.PI + (Mathf.PI / (2 * players));
            float scale = (2f - (players + 1f) / players) * GameManager.spawnCircleWidth;

            Vector3 spawn = GameManager.spawnpoint + new Vector3(Mathf.Sin(comp) * scale, Mathf.Cos(comp) * (players > 2f ? scale * GameManager.spawnCircleHeight : 0), 0);
            Utils.Utils.WrapWorldLocation(ref spawn);
            return spawn;
        }

        public void BumpBlock(short x, short y, TileBase oldTile, TileBase newTile, bool downwards, Vector2 offset, bool spawnCoin, NetworkPrefabRef spawnPrefab) {
            Vector2Int loc = new(x, y);

            Vector3 spawnLocation = Utils.Utils.TilemapToWorldPosition(loc) + OneFourth;

            NetworkObject bumper = Runner.Spawn(PrefabList.Instance.Obj_BlockBump, spawnLocation, onBeforeSpawned: (runner, obj) => {
                obj.GetComponentInChildren<BlockBump>().OnBeforeSpawned(loc, oldTile, newTile, spawnPrefab, downwards, spawnCoin, offset);
            }, predictionKey: new() { Byte1 = (byte) Runner.Tick, Byte0 = PredictionCounter++ });

            GameManager.Instance.tileManager.SetTile(loc, null);
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
            //if (gm.GameEnded)
            //    return;

            // TODO: don't use a coroutine?
            // eh, it should be alrite, since it's an RPC and isn't predictive.
            gameEndTime = Runner.SimulationTime;
            StartCoroutine(EndGame(team));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_LoadingComplete() {
            // Populate scoreboard
            GlobalController.Instance.loadingCanvas.EndLoading();
            OnAllPlayersLoaded?.Invoke();
        }

        //---Helpers
        private void StartGame() {
            GameState = Enums.GameState.Playing;

            // Respawn players
            foreach (PlayerController player in AlivePlayers)
                player.PreRespawn();

            // Play start jingle
            if (Runner.IsForward)
                AudioSfx.PlayOneShot(Enums.Sounds.UI_StartGame);

            StartMusicTimer = TickTimer.CreateFromSeconds(Runner, 1.3f);

            // Respawn enemies
            foreach (KillableEntity enemy in GameManager.enemies)
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

            GameManager.Pause(false);
            GameManager.musicManager.Stop();

            yield return new WaitForSecondsRealtime(1);

            TeamManager teamManager = GameManager.teamManager;
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
            GameManager.winText.text = resultText;

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

            AudioMusic.PlayOneShot(resultSound);
            GameManager.winTextAnimator.SetTrigger(resultTrigger);

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

            // Return back to the main menu
            yield return new WaitForSecondsRealtime(secondsUntilMenu);
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

                mega |= player.State == Enums.PowerupState.MegaMushroom && player.GiantStartTimer.ExpiredOrNotRunning(Runner);
                invincible |= player.IsStarmanInvincible;
            }

            speedup |= SessionData.Instance.Timer > 0 && ((GameEndTimer.RemainingTime(Runner) ?? 0f) < 60f);
            speedup |= GameManager.teamManager.GetFirstPlaceStars() + 1 >= SessionData.Instance.StarRequirement;
            speedup |= AlivePlayers.Count <= 2 && AlivePlayers.All(pl => !pl || pl.Lives == 1 || pl.Lives == 0);

            LoopingMusicPlayer musicManager = GameManager.musicManager;

            if (mega) {
                musicManager.Play(GameManager.megaMushroomMusic);
            } else if (invincible) {
                musicManager.Play(GameManager.invincibleMusic);
            } else {
                musicManager.Play(GameManager.mainMusic);
            }

            musicManager.FastMusic = speedup;
        }

        /// <summary>
        /// Spawns a Big Star, if we can find a valid spawnpoint.
        /// </summary>
        /// <returns>If the start is successfully spawned</returns>
        private bool AttemptSpawnBigStar() {

            GameObject[] starSpawns = GameManager.starSpawns;
            int validSpawns = starSpawns.Length - AvailableStarSpawns.UnsetBitCount();

            if (validSpawns <= 0) {
                ResetAvailableStarSpawns();
                validSpawns = starSpawns.Length;
            }

            int nthSpawn = Random.RangeExclusive(0, validSpawns);
            AvailableStarSpawns.GetNthSetBitIndex(nthSpawn, out int im);
            if (AvailableStarSpawns.GetNthSetBitIndex(nthSpawn, out int index)) {

                Vector3 spawnPos = starSpawns[index].transform.position;
                AvailableStarSpawns[index] = false;

                if (Runner.GetPhysicsScene2D().OverlapCircle(spawnPos, 3, Layers.MaskOnlyPlayers)) {
                    // A player is too close to this spawn. Don't spawn.
                    return false;
                }

                // Valid spawn
                Runner.Spawn(PrefabList.Instance.Obj_BigStar, spawnPos, onBeforeSpawned: (runner, obj) => {
                    obj.GetComponent<StarBouncer>().OnBeforeSpawned(0, true, false);
                });
                return true;
            }

            // This should never happen...
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
            GameManager.gameStartTimestamp = now - secondsSinceStart;

            int timer = SessionData.Instance.Timer;
            if (timer > 0)
                GameManager.gameEndTimestamp = GameManager.gameStartTimestamp + (timer * 60);
        }

        private IEnumerator CallLoadingComplete(float seconds) {
            yield return new WaitForSeconds(seconds);
            Rpc_LoadingComplete();
        }

        //---OnChangeds
        public static void OnGameStartTimerChanged(Changed<GameData> changed) {
            GameManager.teamScoreboardElement.OnTeamsFinalized(GameManager.teamManager);
        }
    }
}
