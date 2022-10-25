using Fusion;

public class LobbyData : NetworkBehaviour {

    public static LobbyData Instance;

#pragma warning disable CS0414
    //--Default values
    private readonly byte defaultMaxPlayers = 10;
    private readonly sbyte defaultStarRequirement = 10;
    private readonly byte defaultCoinRequirement = 8;
    private readonly sbyte defaultLives = -1;
    private readonly int defaultTimer = -1;
    private readonly NetworkBool defaultCustomPowerups = true;
#pragma warning restore CS0414

    //---Networked Variables
    [Networked(OnChanged = nameof(SettingChanged), Default = nameof(defaultMaxPlayers))]        public byte MaxPlayers { get; set; }
    [Networked(OnChanged = nameof(SettingChanged))]                                             public NetworkBool PrivateRoom { get; set; }
    [Networked(OnChanged = nameof(SettingChanged))]                                             public NetworkBool GameStarted { get; set; }
    [Networked(OnChanged = nameof(SettingChanged))]                                             public byte Level { get; set; }
    [Networked(OnChanged = nameof(SettingChanged), Default = nameof(defaultStarRequirement))]   public sbyte StarRequirement { get; set; }
    [Networked(OnChanged = nameof(SettingChanged), Default = nameof(defaultCoinRequirement))]   public byte CoinRequirement { get; set; }
    [Networked(OnChanged = nameof(SettingChanged), Default = nameof(defaultLives))]             public sbyte Lives { get; set; }
    [Networked(OnChanged = nameof(SettingChanged), Default = nameof(defaultTimer))]             public int Timer { get; set; }
    [Networked(OnChanged = nameof(SettingChanged))]                                             public NetworkBool DrawOnTimeUp { get; set; }
    [Networked(OnChanged = nameof(SettingChanged), Default = nameof(defaultCustomPowerups))]    public NetworkBool CustomPowerups { get; set; }

    //---Private Variables
    private Tick lastUpdatedTick;

    //---Properties
    private ChatManager Chat => MainMenuManager.Instance.chat;

    public void Awake() {
        DontDestroyOnLoad(gameObject);
    }

    public override void Spawned() {
        Instance = this;
        if (MainMenuManager.Instance)
            MainMenuManager.Instance.EnterRoom();
    }

    public static void SettingChanged(Changed<LobbyData> data) {
        LobbyData lobby = data.Behaviour;
        Tick currentTick = lobby.Object.Runner.Tick;
        if (currentTick <= lobby.lastUpdatedTick)
            return;

        //no "started" setting to update
        byte newLevel = lobby.Level;
        data.LoadOld();
        byte oldLevel = lobby.Level;
        data.LoadNew();

        MainMenuManager.Instance.roomSettingsCallbacks?.UpdateAllSettings(lobby, oldLevel != newLevel);
        lobby.lastUpdatedTick = currentTick;
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

    public void SetTimer(int value) {
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

    #region RPCS

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void Rpc_ChatIncomingMessage(string message, RpcInfo info = default) => Chat.IncomingPlayerMessage(message, info);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_ChatDisplayMessage(string message, PlayerRef player) => Chat.DisplayPlayerMessage(message, player);

    #endregion
}
