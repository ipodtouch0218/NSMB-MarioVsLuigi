using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;

public class GameManager : MonoBehaviour, IOnEventCallback {
    public static GameManager Instance { get; private set; }

    public Sprite destroyedPipeSprite;
    public AudioClip intro, loop, invincibleIntro, invincibleLoop, megaMushroomLoop;

    public int levelMinTileX, levelMinTileY, levelWidthTile, levelHeightTile;
    public float cameraMinY, cameraHeightY, cameraMinX = -1000, cameraMaxX = 1000;
    public bool loopingLevel = true;
    public Vector3 spawnpoint;
    public Tilemap tilemap;
    public bool canSpawnMegaMushroom = true;
    TileBase[] originalTiles;
    BoundsInt origin;
    GameObject currentStar = null;
    GameObject[] starSpawns;
    readonly List<GameObject> remainingSpawns = new();
    float spawnStarCount;

    //Audio
    public AudioSource music, sfx;
    private Enums.MusicState? musicState = null;

    public GameObject localPlayer;
    public bool paused, loaded, starting;
    public GameObject pauseUI, pauseButton;
    public bool gameover = false, musicEnabled = false;
    public readonly List<string> loadedPlayers = new();
    public int starRequirement;

    public PlayerController[] allPlayers;
    public EnemySpawnpoint[] enemySpawnpoints;

    // EVENT CALLBACK
    public void SendAndExecuteEvent(Enums.NetEventIds eventId, object parameters, ExitGames.Client.Photon.SendOptions sendOption, RaiseEventOptions eventOptions = null) {
        if (eventOptions == null)
            eventOptions = Utils.EVENT_OTHERS;
        //TODO event caching for rejoining?
        PhotonNetwork.RaiseEvent((byte) eventId, parameters, eventOptions, sendOption);
        OnEvent((byte) eventId, parameters);
    }
    public void OnEvent(EventData e) {
        OnEvent(e.Code, e.CustomData);
    }
    public void OnEvent(byte eventId, object customData) {
        switch (eventId) {
        case (byte) Enums.NetEventIds.SetGameStartTimestamp: {
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
            object[] data = (object[]) customData;
            int x = (int) data[0];
            int y = (int) data[1];
            string tilename = (string) data[2];
            Vector3Int loc = new(x,y,0);
            Tile tile = tilename != null ? (Tile) Resources.Load("Tilemaps/Tiles/" + tilename) : null;
            tilemap.SetTile(loc, tile);
            break;
        }
        case (byte) Enums.NetEventIds.SetTileBatch: {
            object[] data = (object[]) customData;
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
        case (byte) Enums.NetEventIds.PlayerFinishedLoading: {
            Player player = (Player) customData;
            loadedPlayers.Add(player.NickName);
            break;
        }
        case (byte) Enums.NetEventIds.BumpTile: {
            object[] data = (object[]) customData;
            int x = (int) data[0];
            int y = (int) data[1];
            bool downwards = (bool) data[2];
            string newTile = (string) data[3];
            BlockBump.SpawnResult spawnResult = (BlockBump.SpawnResult) data[4]; 
            
            Vector3Int loc = new(x,y,0);

            GameObject bump = (GameObject) Instantiate(Resources.Load("Prefabs/Bump/BlockBump"), Utils.TilemapToWorldPosition(loc) + new Vector3(0.25f, 0.25f), Quaternion.identity);
            BlockBump bb = bump.GetComponentInChildren<BlockBump>();

            bb.fromAbove = downwards;
            bb.resultTile = newTile;
            bb.sprite = tilemap.GetSprite(loc);
            bb.spawn = spawnResult;

            tilemap.SetTile(loc, null);
            break;
        }
        case (byte) Enums.NetEventIds.SpawnParticle: {
            object[] data = (object[]) customData;
            int x = (int) data[0];
            int y = (int) data[1];
            string particleName = (string) data[2];
            Vector3 color = (data.Length > 3 ? (Vector3) data[3] : new Vector3(1,1,1));

            Vector3Int loc = new(x,y,0);

            GameObject particle = (GameObject) Instantiate(Resources.Load("Prefabs/Particle/" + particleName), Utils.TilemapToWorldPosition(loc) + new Vector3(0.25f, 0.25f), Quaternion.identity);
            ParticleSystem system = particle.GetComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.startColor = new Color(color.x, color.y, color.z, 1);
            break;
        }
        case (byte) Enums.NetEventIds.SpawnDestructablePipe: {
            object[] data = (object[]) customData;
            float x = (float) data[0];
            float y = (float) data[1];
            bool right = (bool) data[2];
            bool upsideDown = (bool) data[3];
            int tiles = (int) data[4];
            bool alreadyDestroyed = (bool) data[5];
            GameObject particle = (GameObject) Instantiate(Resources.Load("Prefabs/Particle/DestructablePipe"), new Vector2(x + (right ? 0.5f : 0f), y + (tiles/4f * (upsideDown ? -1 : 1))), Quaternion.Euler(0, 0, upsideDown ? 180 : 0));
            Rigidbody2D body = particle.GetComponent<Rigidbody2D>();
            body.velocity = new Vector2(right ? 9 : -9, 6);
            body.angularVelocity = (right ^ upsideDown ? -300 : 300); 
            SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();
            sr.size = new Vector2(2, tiles);
            if (alreadyDestroyed)
                sr.sprite = destroyedPipeSprite;
            //TODO: find the right sound
            sfx.PlayOneShot((AudioClip) Resources.Load("Sound/player/brick_break"));
            break;
        }
        }
    }

    void OnEnable() {
        PhotonNetwork.AddCallbackTarget(this);
    }
    void OnDisable() {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    void Start() {
        Instance = this;
        
        if (!PhotonNetwork.IsConnectedAndReady) {
            // offline mode, spawning in editor?
            PhotonNetwork.OfflineMode = true;
            PhotonNetwork.CreateRoom("Debug");
            ExitGames.Client.Photon.Hashtable properties = new() {
                { Enums.NetRoomProperties.StarRequirement, 10 }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
            Instantiate(Resources.Load("Prefabs/Static/GlobalController"), Vector3.zero, Quaternion.identity);
        }

        origin = new BoundsInt(levelMinTileX, levelMinTileY, 0, levelWidthTile, levelHeightTile, 1);
        starSpawns = GameObject.FindGameObjectsWithTag("StarSpawn");
        starRequirement = (int) PhotonNetwork.CurrentRoom.CustomProperties[Enums.NetRoomProperties.StarRequirement];

        TileBase[] map = tilemap.GetTilesBlock(origin);
        originalTiles = new TileBase[map.Length];
        for (int i = 0; i < map.Length; i++) {
            if (map[i] == null) 
                continue;
            originalTiles[i] = map[i];
        }

        
        SceneManager.SetActiveScene(gameObject.scene);
        localPlayer = PhotonNetwork.Instantiate("Prefabs/" + Utils.GetCharacterData().prefab, spawnpoint, Quaternion.identity, 0);
        localPlayer.GetComponent<Rigidbody2D>().isKinematic = true;

        PhotonNetwork.IsMessageQueueRunning = true;
        
        RaiseEventOptions options = new() { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCache };
        SendAndExecuteEvent(Enums.NetEventIds.PlayerFinishedLoading, PhotonNetwork.LocalPlayer, SendOptions.SendReliable, options);
    }
    IEnumerator LoadingComplete(long startTimestamp) {
        starting = true;
        loaded = true;
        loadedPlayers.Clear();
        enemySpawnpoints = FindObjectsOfType<EnemySpawnpoint>();
        if (PhotonNetwork.IsMasterClient && !PhotonNetwork.OfflineMode) {
            //clear buffered loading complete events. 
            RaiseEventOptions options = new() { Receivers = ReceiverGroup.MasterClient, CachingOption = EventCaching.RemoveFromRoomCache };
            PhotonNetwork.RaiseEvent((byte) Enums.NetEventIds.PlayerFinishedLoading, null, options, SendOptions.SendReliable);
        }
        
        yield return new WaitForSecondsRealtime((startTimestamp - PhotonNetwork.ServerTimestamp) / 1000f);

        GameObject canvas = GameObject.FindGameObjectWithTag("LoadingCanvas");
        if (canvas) {
            canvas.GetComponent<Animator>().SetTrigger("loaded");
            //please just dont beep at me :(
            AudioSource source = canvas.GetComponent<AudioSource>();
            source.Stop();
            source.volume = 0;
            source.enabled = false;
            Destroy(source);
        }

        yield return new WaitForSeconds(3.5f);

        sfx.PlayOneShot((AudioClip) Resources.Load("Sound/startgame")); 

        foreach (var wfgs in FindObjectsOfType<WaitForGameStart>())
            wfgs.AttemptExecute();

        if (PhotonNetwork.IsMasterClient)
            foreach (EnemySpawnpoint point in FindObjectsOfType<EnemySpawnpoint>())
                point.AttemptSpawning();

        localPlayer.GetComponent<Rigidbody2D>().isKinematic = false;
        localPlayer.GetComponent<PlayerController>().enabled = true;
        localPlayer.GetPhotonView().RPC("PreRespawn", RpcTarget.All);

        yield return new WaitForSeconds(1f);
        
        musicEnabled = true;
        
        if (canvas)
            SceneManager.UnloadSceneAsync("Loading");
    }

    IEnumerator EndGame(Photon.Realtime.Player winner) {
        gameover = true;
        music.Stop();
        GameObject text = GameObject.FindWithTag("wintext");
        text.GetComponent<TMP_Text>().text = winner.NickName + " Wins!";
        yield return new WaitForSecondsRealtime(1);
        text.GetComponent<Animator>().SetTrigger("start");

        AudioMixer mixer = music.outputAudioMixerGroup.audioMixer;
        mixer.SetFloat("MusicSpeed", 1f);
        mixer.SetFloat("MusicPitch", 1f);
        music.PlayOneShot((AudioClip)Resources.Load("Sound/match-" + (winner.IsLocal ? "win" : "lose")));
        //TOOD: make a results screen?

        yield return new WaitForSecondsRealtime(4);
        SceneManager.LoadScene("MainMenu");
    }

    void Update() {
        if (gameover) 
            return;

        if (musicEnabled)
            HandleMusic();

        if (allPlayers.Length != PhotonNetwork.CurrentRoom.PlayerCount) {
            allPlayers = FindObjectsOfType<PlayerController>();
            if (allPlayers.Length == PhotonNetwork.CurrentRoom.PlayerCount)
                UIUpdater.Instance.GivePlayersIcons();
        }

        if (PhotonNetwork.IsMasterClient) {
            int players = PhotonNetwork.CurrentRoom.PlayerCount;
            if (!loaded && loadedPlayers.Count >= players) {
                SendAndExecuteEvent(Enums.NetEventIds.SetGameStartTimestamp, PhotonNetwork.ServerTimestamp + ((players-1) * 250) + 1000, SendOptions.SendReliable);
                loaded = true;
            }
            foreach (var player in allPlayers) {
                if (player.stars < starRequirement)
                    continue;

                gameover = true;
                PhotonNetwork.RaiseEvent((byte)Enums.NetEventIds.EndGame, player.photonView.Owner, Utils.EVENT_ALL, SendOptions.SendReliable);
            }

        }

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
                    //TODO: star appear sound
                    spawnStarCount = 15f;
                }
            } else {
                currentStar = GameObject.FindGameObjectWithTag("bigstar");
            }
        }
    }

    void PlaySong(Enums.MusicState state, AudioClip loop, AudioClip intro = null) {
        if (musicState == state) 
            return;
        musicState = state;
        music.Stop();
        music.clip = loop;
        music.loop = true;
        if (intro) {
            music.PlayOneShot(intro);
            music.PlayScheduled(AudioSettings.dspTime + intro.length - (Time.fixedDeltaTime * 2));
        } else {
            music.Play();
        }
    }

    void HandleMusic() {
        bool invincible = false;
        bool mega = false;
        bool speedup = false;
        
        foreach (var player in allPlayers) {
            if (player == null) 
                return;
            if (player.state == Enums.PowerupState.Giant && player.giantTimer != 15)
                mega = true;
            if (player.invincible > 0)
                invincible = true;
            if ((player.stars + 1f) / starRequirement >= 0.95f)
                speedup = true;
        }

        if (mega) {
            PlaySong(Enums.MusicState.MegaMushroom, megaMushroomLoop);
        } else if (invincible) {
            PlaySong(Enums.MusicState.Starman, invincibleLoop, invincibleIntro);
        } else {
            PlaySong(Enums.MusicState.Normal, loop, intro);
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

    public void Pause() {
        paused = !paused;
        Instance.pauseUI.SetActive(paused);
        EventSystem.current.SetSelectedGameObject(pauseButton);
    }

    public void Quit() {
        PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene("MainMenu");
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
    void OnDrawGizmos() {
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
                    Gizmos.DrawIcon(Utils.TilemapToWorldPosition(loc, this) + new Vector3(0.25f, 0.25f), "coin");
                if (tile is PowerupTile)
                    Gizmos.DrawIcon(Utils.TilemapToWorldPosition(loc, this) + new Vector3(0.25f, 0.25f), "powerup");
            }
        }
        
        Gizmos.color = new Color(1, 0.9f, 0.2f, 0.2f);
        foreach (GameObject starSpawn in GameObject.FindGameObjectsWithTag("StarSpawn")) {
            Gizmos.DrawCube(starSpawn.transform.position, Vector3.one);
            Gizmos.DrawIcon(starSpawn.transform.position, "star", true, new Color(1, 1, 1, 0.5f));
        }
    }
}
