using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class SessionData : NetworkBehaviour {

    //---Static Variables
    public static SessionData Instance;
    private static readonly WaitForSeconds WaitTwoSeconds = new(2);

#pragma warning disable CS0414
    //---Default values
    private readonly sbyte defaultStarRequirement = 10;
    private readonly byte defaultCoinRequirement = 8;
    private readonly sbyte defaultLives = -1;
    private readonly sbyte defaultTimer = -1;
    private readonly NetworkBool defaultCustomPowerups = true;
#pragma warning restore CS0414

    //---Networked Variables
    [Networked(OnChanged = nameof(SettingChanged))]                                           public byte MaxPlayers { get; set; }
    [Networked(OnChanged = nameof(SettingChanged))]                                           public NetworkBool PrivateRoom { get; set; }
    [Networked(OnChanged = nameof(GameStartTimerChanged))]                                    public TickTimer GameStartTimer { get; set; }
    [Networked(OnChanged = nameof(StartChanged))]                                             public NetworkBool GameStarted { get; set; }
    [Networked(OnChanged = nameof(SettingChanged))]                                           public byte Level { get; set; }
    [Networked(OnChanged = nameof(SettingChanged), Default = nameof(defaultStarRequirement))] public sbyte StarRequirement { get; set; }
    [Networked(OnChanged = nameof(SettingChanged), Default = nameof(defaultCoinRequirement))] public byte CoinRequirement { get; set; }
    [Networked(OnChanged = nameof(SettingChanged), Default = nameof(defaultLives))]           public sbyte Lives { get; set; }
    [Networked(OnChanged = nameof(SettingChanged), Default = nameof(defaultTimer))]           public sbyte Timer { get; set; }
    [Networked(OnChanged = nameof(SettingChanged))]                                           public NetworkBool DrawOnTimeUp { get; set; }
    [Networked(OnChanged = nameof(SettingChanged), Default = nameof(defaultCustomPowerups))]  public NetworkBool CustomPowerups { get; set; }
    [Networked(OnChanged = nameof(SettingChanged))]                                           public NetworkBool Teams { get; set; }
    [Networked]                                                                               public byte AlternatingMusicIndex { get; set; }

    //---Private Variables
    private readonly Dictionary<Guid, uint> wins = new();
    private HashSet<Guid> bannedIds;
    private HashSet<string> bannedIps;
    private Tick lastUpdatedTick;
    private float lastStartCancelTime = -10f;
    private bool playedStartSound;
    private Coroutine pingUpdaterCorotuine;

    //---Properties
    private ChatManager Chat => MainMenuManager.Instance.chat;

    public void Awake() {
        DontDestroyOnLoad(gameObject);
    }

    public override void Spawned() {
        if (Instance != this) {
            Instance = this;
            if (MainMenuManager.Instance)
                MainMenuManager.Instance.EnterRoom(false);
        }

        PrivateRoom = !Runner.SessionInfo.IsVisible;
        if (MaxPlayers == 0) {
            NetworkUtils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.MaxPlayers, out int players);
            MaxPlayers = (byte) players;
        }

        if (Runner.IsServer) {
            bannedIds = new();
            bannedIps = new();
            NetworkHandler.OnConnectRequest += OnConnectRequest;

            pingUpdaterCorotuine = StartCoroutine(UpdatePings());
        }

        gameObject.name = "SessionData (" + Runner.SessionInfo.Name + ")";
    }

    public override void FixedUpdateNetwork() {
        if (!GameStarted && GameStartTimer.IsRunning && MainMenuManager.Instance) {

            if (!MainMenuManager.Instance.IsRoomConfigurationValid()) {
                GameStartTimer = TickTimer.None;
                return;
            }

            if (GameStartTimer.Expired(Runner)) {
                // Start game
                if (Runner.IsServer)
                    Rpc_StartGame();

            } else {
                int ticksLeft = (GameStartTimer.RemainingTicks(Runner) ?? 0) + 1;
                if (ticksLeft % Runner.Config.Simulation.TickRate == 0) {
                    // Send countdown
                    int seconds = ticksLeft / Runner.Config.Simulation.TickRate;
                    MainMenuManager.Instance.OnCountdownTick(seconds);
                }
            }
        }
    }

    private IEnumerator UpdatePings() {
        while (true) {
            yield return WaitTwoSeconds;
            foreach (PlayerRef player in Runner.ActivePlayers) {
                if (player.GetPlayerData(Runner) is not PlayerData pd || !pd)
                    continue;
                pd.Ping = (int) (Runner.GetPlayerRtt(player) * 1000);
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        NetworkHandler.OnConnectRequest -= OnConnectRequest;

        if (pingUpdaterCorotuine != null)
            StopCoroutine(pingUpdaterCorotuine);
    }

    private bool OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
        try {
            ConnectionToken connectionToken = ConnectionToken.Deserialize(token);

            // Connection token not signed by the auth server
            if (!connectionToken.HasValidSignature()) {
                Debug.Log($"[Network] Received an unsigned connection token from an incoming connection: {request.RemoteAddress}");
                return false;
            }

            if (bannedIds.Contains(connectionToken.signedData.UserId)) {
                Debug.Log($"[Network] Rejecting join from a banned userid ({connectionToken.signedData.UserId})");
                return false;
            }

            return true;
        } catch {
            // Malformed connection token.
            Debug.Log($"[Network] Received a malformed connection token from an incoming connection: {request.RemoteAddress}");
            return false;
        }
    }

    public void SaveWins(PlayerData data) {
        wins[data.UserId] = data.Wins;
    }

    public void LoadWins(PlayerData data) {
        if (wins.ContainsKey(data.UserId))
            data.Wins = wins[data.UserId];
    }

    public void AddBan(PlayerData data) {
        bannedIds.Add(data.UserId);
    }

    public void SetMaxPlayers(byte value) {
        MaxPlayers = value;
        UpdateProperty(Enums.NetRoomProperties.MaxPlayers, value);
    }

    public void SetGameStarted(bool value) {
        GameStarted = value;
        UpdateProperty(Enums.NetRoomProperties.GameStarted, value ? 1 : 0);
    }

    public void SetLevel(byte value) {
        Level = value;
        UpdateProperty(Enums.NetRoomProperties.Level, value);
    }

    public void SetStarRequirement(sbyte value) {
        StarRequirement = value;
        UpdateProperty(Enums.NetRoomProperties.StarRequirement, value);
    }

    public void SetCoinRequirement(byte value) {
        CoinRequirement = value;
        UpdateProperty(Enums.NetRoomProperties.CoinRequirement, value);
    }
    public void SetLives(sbyte value) {
        Lives = value;
        UpdateProperty(Enums.NetRoomProperties.Lives, value);
    }

    public void SetTimer(sbyte value) {
        Timer = value;
        UpdateProperty(Enums.NetRoomProperties.Time, value);
    }

    public void SetDrawOnTimeUp(bool value) {
        DrawOnTimeUp = value;
        //no session property here.
    }

    public void SetCustomPowerups(bool value) {
        CustomPowerups = value;
        UpdateProperty(Enums.NetRoomProperties.CustomPowerups, value ? 1 : 0);
    }

    public void SetTeams(bool value) {
        Teams = value;
        UpdateProperty(Enums.NetRoomProperties.Teams, value ? 1 : 0);
    }

    public void SetPrivateRoom(bool value) {
        PrivateRoom = value;
        Runner.SessionInfo.IsVisible = !value;
        //no session property here.
    }

    private void UpdateProperty(string property, SessionProperty value) {
        Runner.SessionInfo.UpdateCustomProperties(new() {
            [property] = value
        });
    }


    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void Rpc_ChatIncomingMessage(string message, RpcInfo info = default) => Chat.IncomingPlayerMessage(message, info);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_ChatDisplayMessage(string message, PlayerRef player) => Chat.DisplayPlayerMessage(message, player);
    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void Rpc_UpdateTypingCounter(RpcInfo info = default) => Chat.SetTypingIndicator(info.Source);


    [Rpc(RpcSources.StateAuthority, RpcTargets.All, TickAligned = false)]
    public void Rpc_StartGame() {

        // Set PlayerIDs and spectator values for players
        sbyte count = 0;
        foreach (PlayerRef player in Runner.ActivePlayers) {
            PlayerData data = player.GetPlayerData(Runner);
            if (!data)
                continue;

            data.IsCurrentlySpectating = data.IsManualSpectator;
            data.IsLoaded = false;

            if (data.IsCurrentlySpectating) {
                data.PlayerId = -1;
                data.Team = -1;
                continue;
            }

            data.PlayerId = count;
            if (!Teams) {
                data.Team = count;
            } else if (data.Team == -1) {
                data.Team = 0;
            }

            count++;
        }

        SetGameStarted(true);

        // Load the correct scene
        Runner.SetActiveScene(MainMenuManager.Instance.GetCurrentSceneRef());
    }

    //---OnChangeds
    public static void StartChanged(Changed<SessionData> data) {
        if (MainMenuManager.Instance)
            MainMenuManager.Instance.OnGameStartChanged();

        SettingChanged(data);
    }

    public static void SettingChanged(Changed<SessionData> data) {
        SessionData lobby = data.Behaviour;
        Tick currentTick = lobby.Object.Runner.Tick;
        if (currentTick <= lobby.lastUpdatedTick)
            return;

        //no "started" setting to update
        byte newLevel = lobby.Level;
        data.LoadOld();
        byte oldLevel = lobby.Level;
        data.LoadNew();

        if (MainMenuManager.Instance && MainMenuManager.Instance.roomSettingsCallbacks)
            MainMenuManager.Instance.roomSettingsCallbacks.UpdateAllSettings(lobby, oldLevel != newLevel);

        lobby.lastUpdatedTick = currentTick;
    }

    public static void GameStartTimerChanged(Changed<SessionData> data) {
        if (!MainMenuManager.Instance)
            return;

        SessionData sd = data.Behaviour;
        float time = sd.Runner.SimulationTime;

        if (!sd.GameStartTimer.IsRunning) {
            if (sd.playedStartSound) {
                MainMenuManager.Instance.chat.AddSystemMessage("ui.inroom.chat.server.startcancelled");
            }
            sd.lastStartCancelTime = sd.Runner.SimulationTime;
            MainMenuManager.Instance.OnCountdownTick(-1);
            sd.playedStartSound = false;
        } else {
            if (sd.lastStartCancelTime + 3f < time || sd.Runner.GetLocalPlayerData().IsRoomOwner) {
                MainMenuManager.Instance.chat.AddSystemMessage("ui.inroom.chat.server.starting", "countdown", Mathf.CeilToInt(sd.GameStartTimer.RemainingTime(sd.Runner) ?? 0).ToString());
                MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_FileSelect);
                sd.playedStartSound = true;
            }
            MainMenuManager.Instance.OnCountdownTick(3);
        }
    }
}
