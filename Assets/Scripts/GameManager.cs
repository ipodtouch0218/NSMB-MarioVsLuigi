using System;
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

namespace NSMB.Game {
    public class GameManager : MonoBehaviour {

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
        public GameData GameData => GameData.Instance;

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
        [SerializeField] public LoopingMusicData mainMusic;
        [SerializeField] public LoopingMusicData invincibleMusic;
        [SerializeField] public LoopingMusicData megaMushroomMusic;

        [Header("Level Configuration")]
        public int levelMinTileX;
        public int levelMinTileY;
        public int levelWidthTile;
        public int levelHeightTile;
        public bool loopingLevel = true, spawnBigPowerups = true, spawnVerticalPowerups = true;
        public string levelDesigner = "", richPresenceId = "", levelTranslationKey = "";
        public Vector3 spawnpoint;
        [FormerlySerializedAs("size")] public float spawnCircleWidth = 1.39f;
        [FormerlySerializedAs("ySize")] public float spawnCircleHeight = 0.8f;
        [ColorUsage(false)] public Color levelUIColor = new(24, 178, 170);

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
        public TileManager tileManager;
        public SingleParticleManager particleManager;
        public TeamManager teamManager = new();
        public Canvas nametagCanvas;
        public PlayerController localPlayer;
        public double gameStartTimestamp, gameEndTimestamp;
        public bool paused;

        [NonSerialized] public KillableEntity[] enemies;
        [NonSerialized] public FloatingCoin[] coins;
        [HideInInspector] public TileBase[] sceneTiles;
        [HideInInspector] public ushort[] originalTiles;

        //---Private Variables
        private bool pauseStateLastFrame, optionsWereOpenLastFrame;

        //---Components
        [SerializeField] public SpectationManager spectationManager;
        [SerializeField] public LoopingMusicPlayer musicManager;
        [SerializeField] public AudioSource music, sfx;

        // TODO: convert to RPC...?
        public void SpawnResizableParticle(Vector2 pos, bool right, bool flip, Vector2 size, GameObject prefab) {
            GameObject particle = Instantiate(prefab, pos, Quaternion.Euler(0, 0, flip ? 180 : 0));

            SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();
            sr.size = size;

            SimplePhysicsMover body = particle.GetComponent<SimplePhysicsMover>();
            body.velocity = new Vector2(right ? 7 : -7, 6);
            body.angularVelocity = right ^ flip ? -300 : 300;

            particle.transform.position += new Vector3(sr.size.x * 0.25f, size.y * 0.25f * (flip ? -1 : 1));
        }

        public void OnEnable() {
            ControlSystem.controls.UI.Pause.performed += OnPause;
            ControlSystem.controls.Debug.ToggleHUD.performed += OnToggleHud;
        }

        public void OnDisable() {
            ControlSystem.controls.UI.Pause.performed -= OnPause;
            ControlSystem.controls.Debug.ToggleHUD.performed -= OnToggleHud;
        }

        public void OnValidate() {
            // Remove our cached values if we change something in editor.
            // We shouldn't have to worry about values changing mid-game ever.
            levelWidth = levelHeight = middleX = minX = minY = maxX = maxY = null;
        }

        public void Awake() {
            Instance = this;

            //Make UI color translucent
            levelUIColor.a = .7f;
        }

        public async void Start() {
            // Handles spawning in editor
            if (!NetworkHandler.Runner.SessionInfo.IsValid) {
                // Join a singleplayer room if we're not in one
                _ = await NetworkHandler.CreateRoom(new() {
                    Scene = SceneManager.GetActiveScene().buildIndex,
                }, GameMode.Single);
            }

            // Find objects in the scene
            starSpawns = GameObject.FindGameObjectsWithTag("StarSpawn");
            enemies = FindObjectsOfType<KillableEntity>().Where(ke => ke is not BulletBillMover).ToArray();
            coins = FindObjectsOfType<FloatingCoin>();

            nametagCanvas.gameObject.SetActive(Settings.Instance.GraphicsPlayerNametags);

            // Spawn a GameDataHolder, if one doesn't already exist.
            if (!GameData.Instance) {
                NetworkHandler.Instance.runner.Spawn(PrefabList.Instance.GameDataHolder);
                NetworkHandler.Instance.runner.Spawn(PrefabList.Instance.TileManager);
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
            if (!tile)
                return 0;

            int index = Array.IndexOf(sceneTiles, tile);
            if (index == -1)
                return 0;

            return (ushort) index;
        }

        public TileBase GetTileInstanceFromTileId(ushort id) {
            return sceneTiles[id];
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
            if (paused == newState || GameData.GameState != Enums.GameState.Playing)
                return;

            paused = newState;
            sfx.PlayOneShot(Enums.Sounds.UI_Pause);
            pauseUI.SetActive(paused);
        }

        //---UI Callbacks
        public void PauseEndMatch() {
            if (!NetworkHandler.Runner.IsServer)
                return;

            pauseUI.SetActive(false);
            sfx.PlayOneShot(Enums.Sounds.UI_Decide);
            GameData.Instance.Rpc_EndGame(PlayerRef.None);
        }

        public void PauseQuitGame() {
            sfx.PlayOneShot(Enums.Sounds.UI_Decide);
            _ = NetworkHandler.ConnectToRegion();
        }

        public void PauseOpenOptions() {
            GlobalController.Instance.optionsManager.OpenMenu();
            sfx.PlayOneShot(Enums.Sounds.UI_Decide);
        }

        //---Debug
#if UNITY_EDITOR
        private static int DebugSpawns = 10;
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
                        Gizmos.DrawIcon(Utils.Utils.TilemapToWorldPosition(loc, this) + OneFourth, "coin");

                    if (tile is PowerupTile)
                        Gizmos.DrawIcon(Utils.Utils.TilemapToWorldPosition(loc, this) + OneFourth, "powerup");
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

        private Vector3 GetSpawnpoint(int playerIndex, int players) {

            float comp = (float) playerIndex / players * 2.5f * Mathf.PI + (Mathf.PI / (2 * players));
            float scale = (2f - (players + 1f) / players) * spawnCircleWidth;

            Vector3 spawn = spawnpoint + new Vector3(Mathf.Sin(comp) * scale, Mathf.Cos(comp) * (players > 2f ? scale * spawnCircleHeight : 0), 0);
            Utils.Utils.WrapWorldLocation(ref spawn);
            return spawn;
        }
#endif
    }
}
