using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.UI.MainMenu;
using NSMB.Utils;
using UnityEngine.SceneManagement;

public class SessionData : NetworkBehaviour {

    //---Static Variables
    public static SessionData Instance;
    public static HashSet<PlayerRef> PlayersNeedingJoinMessage = new();
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
    [Networked] public byte MaxPlayers { get; set; }
    [Networked] public NetworkBool PrivateRoom { get; set; }
    [Networked] public TickTimer GameStartTimer { get; set; }
    [Networked] public NetworkBool GameStarted { get; set; }
    [Networked] public byte Level { get; set; }
    [Networked(Default = nameof(defaultStarRequirement))] public sbyte StarRequirement { get; set; }
    [Networked(Default = nameof(defaultCoinRequirement))] public byte CoinRequirement { get; set; }
    [Networked(Default = nameof(defaultLives))] public sbyte Lives { get; set; }
    [Networked(Default = nameof(defaultTimer))] public sbyte Timer { get; set; }
    [Networked] public NetworkBool DrawOnTimeUp { get; set; }
    [Networked(Default = nameof(defaultCustomPowerups))]  public NetworkBool CustomPowerups { get; set; }
    [Networked] public NetworkBool Teams { get; set; }
    [Networked] public byte AlternatingMusicIndex { get; set; }

    //---Properties
    private MainMenuChat Chat {
        get {
            if (MainMenuManager.Instance)
                return MainMenuManager.Instance.chat;
            return null;
        }
    }

    //---Private Variables
    private readonly Dictionary<Guid, uint> wins = new();
    private HashSet<Guid> bannedIds;
    private HashSet<string> bannedIps;
    private Tick lastUpdatedTick;
    private float lastStartCancelTime = -10f;
    private bool playedStartSound;
    private Coroutine pingUpdaterCorotuine;
    private ChangeDetector changeDetector;
    private PropertyReader<byte> levelPropertyReader;

    public void Awake() {
        DontDestroyOnLoad(gameObject);
        Instance = this;
    }

    public override void Spawned() {
        Instance = this;

        if (!Runner.IsResume) {
            if (MainMenuManager.Instance)
                MainMenuManager.Instance.EnterRoom(false);
        }

        if (Runner.IsServer) {

            PrivateRoom = !Runner.SessionInfo.IsVisible;
            if (MaxPlayers == 0) {
                NetworkUtils.GetSessionProperty(Runner.SessionInfo, Enums.NetRoomProperties.MaxPlayers, out int players);
                MaxPlayers = (byte) players;
            }

            bannedIds = new();
            bannedIps = new();
            NetworkHandler.OnConnectRequest += OnConnectRequest;

            pingUpdaterCorotuine = StartCoroutine(UpdatePings());

            if (Runner.IsResume) {
                GameStartTimer = TickTimer.None;
                GameStarted = false;
            }
        }

        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        levelPropertyReader = GetPropertyReader<byte>(nameof(Level));
        gameObject.name = "SessionData (" + Runner.SessionInfo.Name + ")";

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.roomSettingsCallbacks.UpdateAllSettings(Instance, false);
    }

    public override void Render() {

        if (!GameStarted && GameStartTimer.IsActive(Runner) && MainMenuManager.Instance) {
            int ticksLeft = (GameStartTimer.RemainingTicks(Runner) ?? 0) + 1;
            if (ticksLeft % Runner.TickRate == 0) {
                // Send countdown
                int seconds = ticksLeft / Runner.TickRate;
                MainMenuManager.Instance.OnCountdownTick(seconds);
            }
        }

        foreach (var change in changeDetector.DetectChanges(this, out var previous, out _)) {
            switch (change) {
            case nameof(MaxPlayers):
            case nameof(PrivateRoom):
            case nameof(GameStarted):
            case nameof(Level):
            case nameof(StarRequirement):
            case nameof(CoinRequirement):
            case nameof(Lives):
            case nameof(Timer):
            case nameof(DrawOnTimeUp):
            case nameof(CustomPowerups):
            case nameof(Teams):
                OnSettingChanged(previous);
                break;
            case nameof(GameStartTimer):
                OnGameStartTimerChanged();
                break;
            }
        }
    }

    public override void FixedUpdateNetwork() {
        if (!GameStarted && GameStartTimer.IsRunning && MainMenuManager.Instance) {

            if (!MainMenuManager.Instance.IsRoomConfigurationValid()) {
                GameStartTimer = TickTimer.None;
                return;
            }

            if (GameStartTimer.Expired(Runner)) {
                // Start game
                Rpc_StartGame();
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

    //---RPCs
    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void Rpc_ChatIncomingMessage(string message, RpcInfo info = default) => ChatManager.Instance.IncomingPlayerMessage(message, info);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_ChatDisplayMessage(string message, PlayerRef player) => ChatManager.Instance.DisplayPlayerMessage(message, player);

    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void Rpc_UpdateTypingCounter(RpcInfo info = default) {
        if (Chat)
            Chat.SetTypingIndicator(info.Source);
    }

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
            data.IsReady = false;

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

        ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.started");
        SetGameStarted(true);

        // Load the correct scene
        if (Runner.IsServer) {
            Runner.LoadScene(MainMenuManager.Instance.GetCurrentSceneRef(), LoadSceneMode.Single);
        }
    }

    //---OnChangeds
    public void OnSettingChanged(NetworkBehaviourBuffer previous) {
        Tick currentTick = Object.Runner.Tick;
        if (currentTick <= lastUpdatedTick)
            return;

        //no "started" setting to update
        int oldLevel = previous.Read(levelPropertyReader);

        if (MainMenuManager.Instance && MainMenuManager.Instance.roomSettingsCallbacks)
            MainMenuManager.Instance.roomSettingsCallbacks.UpdateAllSettings(this, oldLevel != Level);

        lastUpdatedTick = currentTick;
    }

    public void OnGameStartTimerChanged() {
        if (!MainMenuManager.Instance)
            return;

        float time = Runner.SimulationTime;

        if (!GameStartTimer.IsRunning) {
            if (playedStartSound) {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.startcancelled");
            }
            lastStartCancelTime = Runner.SimulationTime;
            MainMenuManager.Instance.OnCountdownTick(-1);
            playedStartSound = false;
        } else {
            if (lastStartCancelTime + 3f < time || Runner.GetLocalPlayerData().IsRoomOwner) {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.starting", "countdown", Mathf.CeilToInt((GameStartTimer.RemainingTime(Runner) - 0.01f) ?? 0).ToString());
                MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_FileSelect);
                playedStartSound = true;
            }
            MainMenuManager.Instance.OnCountdownTick(3);
        }
    }
}
