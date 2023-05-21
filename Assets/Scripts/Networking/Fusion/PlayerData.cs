using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

using Fusion;
using Fusion.Sockets;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Utils;

public class PlayerData : NetworkBehaviour {

    //---Static stuffs
    public static bool Locked => SessionData.Instance && SessionData.Instance.GameStarted;

    //---Networked Variables
    [Networked(OnChanged = nameof(OnNameChanged)), Capacity(20)] public string Nickname { get; set; } = "noname";
    [Networked, Capacity(28), SerializeField]                    private string DisplayNickname { get; set; } = "noname";
    [Networked]                                                  public Guid UserId { get; set; }
    [Networked]                                                  public sbyte PlayerId { get; set; }
    [Networked]                                                  public uint Wins { get; set; }
    [Networked(OnChanged = nameof(OnStartSettingChanged))]       public sbyte Team { get; set; }
    [Networked(OnChanged = nameof(OnStartSettingChanged))]       public NetworkBool IsManualSpectator { get; set; }
    [Networked]                                                  public NetworkBool IsCurrentlySpectating { get; set; }
    [Networked]                                                  public NetworkBool IsRoomOwner { get; set; }
    [Networked(OnChanged = nameof(OnLoadStateChanged))]          public NetworkBool IsLoaded { get; set; }
    [Networked]                                                  public NetworkBool IsMuted { get; set; }
    [Networked]                                                  public TickTimer MessageCooldownTimer { get; set; }
    [Networked(OnChanged = nameof(OnCharacterChanged))]          public byte CharacterIndex { get; set; }
    [Networked(OnChanged = nameof(OnSkinChanged))]               public byte SkinIndex { get; set; }
    [Networked(OnChanged = nameof(OnSettingChanged))]            public int Ping { get; set; }
    [Networked]                                                  public NetworkBool Initialized { get; set; }

    //---Private Variables
    private Tick lastUpdatedTick;
    private NetAddress address;
    private string filteredNickname;

    public void Awake() {
        DontDestroyOnLoad(gameObject);
    }

    public override void Spawned() {
        // Keep track of our data, pls kthx
        Runner.SetPlayerObject(Object.InputAuthority, Object);

        PlayerId = -1;
        if (Object.InputAuthority == Runner.SessionInfo.MaxPlayers - 1)
            Team = 0;
        else
            Team = (sbyte) ((Object.InputAuthority + 1) % 5);

        if (SessionData.Instance)
            SessionData.Instance.LoadWins(this);

        if (Object.HasInputAuthority) {
            // We're the client. update with our data.
            Rpc_SetCharacterIndex((byte) Settings.Instance.genericCharacter);
            Rpc_SetSkinIndex((byte) Settings.Instance.genericSkin);

            if (Runner.IsServer)
                IsRoomOwner = true;
        }

        if (Runner.IsServer) {
            if (!Initialized) {
                string nickname = Encoding.UTF8.GetString(Runner.GetPlayerConnectionToken(Object.InputAuthority) ?? Encoding.UTF8.GetBytes("noname"));
                SetNickname(nickname);

                // Expose their userid
                Guid.TryParse(Runner.GetPlayerUserId(Object.InputAuthority), out Guid id);
                UserId = id;

                IsCurrentlySpectating = SessionData.Instance ? SessionData.Instance.GameStarted : false;

                Initialized = true;
            } else {
                SetNickname(Nickname);
            }
        }

        if (MainMenuManager.Instance)
            StartCoroutine(MainMenuManager.Instance.OnPlayerDataValidated(Object.InputAuthority));
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (hasState)
            SessionData.Instance.SaveWins(this);
    }

    public string GetNickname(bool filter = true) {
        return filter ? DisplayNickname : (filteredNickname ??= DisplayNickname.Filter());
    }

    public string GetUserIdString() {
        return UserId.ToString();
    }

    public void SetNickname(string name) {
        // Limit nickname to valid characters only.
        name = Regex.Replace(name, @"[^\p{L}\d]", "");

        // Enforce character limits
        name = name[..Mathf.Min(name.Length, MainMenuManager.NicknameMax)];

        // If this new nickname is invalid, default back to "noname"
        if (name.Length < MainMenuManager.NicknameMin)
            name = "noname";

        Nickname = name;

        // Check for players with duplicate names, and add (1), (2), etc
        int count = Runner.ActivePlayers
            .Select(pr => pr.GetPlayerData(Runner))
            .Where(pd => pd && pd.Object)
            .Where(pd => pd.Nickname.ToString().Filter() == name)
            .Count() - 1;

        if (count > 0)
            name += " (" + count + ")";

        DisplayNickname = name;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_FinishedLoading() {
        IsLoaded = true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetPermanentSpectator(bool value) {
        //not accepting changes at this time
        if (Locked)
            return;

        IsManualSpectator = value;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetCharacterIndex(byte index) {
        //not accepting changes at this time
        if (Locked)
            return;

        //invalid character...
        if (index >= ScriptableManager.Instance.characters.Length)
            return;

        CharacterIndex = index;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetSkinIndex(byte index) {
        //not accepting changes at this time
        if (Locked)
            return;

        //invalid skin...
        if (index >= ScriptableManager.Instance.skins.Length)
            return;

        SkinIndex = index;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetTeamNumber(sbyte team) {
        //not accepting changes at this time
        if (Locked)
            return;

        if (team < 0 || team > 4)
            return;

        Team = team;
    }

    public static void OnLoadStateChanged(Changed<PlayerData> changed) {
        if (changed.Behaviour.IsLoaded && GameData.Instance)
            GameData.Instance.CheckIfAllPlayersLoaded();
    }

    public static void OnSettingChanged(Changed<PlayerData> changed) {
        if (!MainMenuManager.Instance || changed.Behaviour.lastUpdatedTick >= changed.Behaviour.Runner.Tick)
            return;

        changed.Behaviour.lastUpdatedTick = changed.Behaviour.Runner.Tick;
        MainMenuManager.Instance.playerList.UpdateAllPlayerEntries();
    }

    public static void OnStartSettingChanged(Changed<PlayerData> changed) {
        if (!MainMenuManager.Instance)
            return;

        MainMenuManager.Instance.UpdateStartGameButton();
        OnSettingChanged(changed);
    }

    public static void OnNameChanged(Changed<PlayerData> changed) {
        changed.Behaviour.gameObject.name = "PlayerData (" + changed.Behaviour.Nickname + ", " + changed.Behaviour.UserId.ToString() + ")";
    }

    public static void OnCharacterChanged(Changed<PlayerData> changed) {
        if (!MainMenuManager.Instance || !changed.Behaviour.Object.HasInputAuthority)
            return;

        MainMenuManager.Instance.SwapCharacter(changed.Behaviour.CharacterIndex, false);
        OnSettingChanged(changed);
    }

    public static void OnSkinChanged(Changed<PlayerData> changed) {
        if (!MainMenuManager.Instance || !changed.Behaviour.Object.HasInputAuthority)
            return;

        MainMenuManager.Instance.SwapPlayerSkin(changed.Behaviour.SkinIndex, false);
    }
}
