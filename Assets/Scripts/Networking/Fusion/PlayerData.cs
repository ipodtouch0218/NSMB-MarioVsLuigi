using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Utils;

public class PlayerData : NetworkBehaviour {

    //---Static stuffs
    public bool Locked => SessionData.Instance && SessionData.Instance.GameStarted && !IsCurrentlySpectating;

    //---Networked Variables
    [Networked(OnChanged = nameof(OnNameChanged)), Capacity(20)] public string Nickname { get; set; } = "noname";
    [Networked, Capacity(28)]                                    private string DisplayNickname { get; set; } = "noname";
    [Networked]                                                  public ConnectionToken ConnectionToken { get; set; }
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
    [Networked]                                                  public int JoinTick { get; set; }

    public Guid UserId => ConnectionToken.signedData.UserId;
    public NicknameColor NicknameColor => nicknameColor;

    //---Private Variables
    private NicknameColor nicknameColor;
    private Tick lastUpdatedTick;
    private string filteredNickname;

    public void Awake() {
        DontDestroyOnLoad(gameObject);
    }

    public void OnBeforeSpawned() {
        if (!Initialized) {
            // Expose their connection token :flushed:
            byte[] token = Runner.GetPlayerConnectionToken(Object.InputAuthority);
            try {
                ConnectionToken = ConnectionToken.Deserialize(token);
                if (!ConnectionToken.HasValidSignature()) {
                    // Invalid signature, nice try guy
                    throw new Exception();
                }
                if (ConnectionToken.signedData.UserId != Guid.Parse(Runner.GetPlayerUserId(Object.InputAuthority))) {
                    // Attempted to steal from another user???
                    throw new Exception();
                }
                // Successful :D
                SetNickname(ConnectionToken.nickname.Value);
            } catch {
                Debug.LogWarning($"No/malformed/invalid connection token from player with id '{Runner.GetPlayerUserId(Object.InputAuthority)}'. If you're directly booting the game within a level in the Unity Editor, this is not a bug.");
                SetNickname(ConnectionToken.nickname.Value);
                ConnectionToken = new();
            }

            IsCurrentlySpectating = SessionData.Instance ? SessionData.Instance.GameStarted : false;
            JoinTick = Runner.Tick;

            Initialized = true;
        } else {
            SetNickname(Nickname);
        }
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

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.OnPlayerDataValidated(this);

        nicknameColor = NicknameColor.FromConnectionToken(ConnectionToken);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (hasState)
            SessionData.Instance.SaveWins(this);

        runner.SetPlayerObject(Object.InputAuthority, null);
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
        // Not accepting changes at this time
        if (Locked)
            return;

        // Invalid skin...
        if (index >= ScriptableManager.Instance.skins.Length)
            return;

        SkinIndex = index;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetTeamNumber(sbyte team) {
        // Not accepting changes at this time
        if (Locked)
            return;

        // Invalid team...
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
