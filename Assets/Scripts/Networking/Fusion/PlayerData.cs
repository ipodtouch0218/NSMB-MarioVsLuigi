using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Utils;
using NSMB.UI.MainMenu;
using NSMB.UI.Pause.Options;

public class PlayerData : NetworkBehaviour {

    //---Static stuffs
    public bool Locked => SessionData.Instance && SessionData.Instance.GameStarted && !IsCurrentlySpectating;

    //---Events
    public event Action<bool> OnInOptionsChangedEvent;

    //---Networked Variables
    [Networked, Capacity(20)]                                    public string RawNickname { get; set; } = "noname";
    [Networked(OnChanged = nameof(OnNameChanged)), Capacity(28)] private string DisplayNickname { get; set; } = "noname";
    [Networked]                                                  public ConnectionToken ConnectionToken { get; set; }
    [Networked]                                                  public sbyte PlayerId { get; set; }
    [Networked]                                                  public uint Wins { get; set; }
    [Networked(OnChanged = nameof(OnStartSettingChanged))]       public sbyte Team { get; set; }
    [Networked(OnChanged = nameof(OnStartSettingChanged))]       public NetworkBool IsManualSpectator { get; set; }
    [Networked]                                                  public NetworkBool IsCurrentlySpectating { get; set; }
    [Networked]                                                  public NetworkBool IsRoomOwner { get; set; }
    [Networked(OnChanged = nameof(OnLoadStateChanged))]          public NetworkBool IsLoaded { get; set; }
    [Networked]                                                  public NetworkBool IsMuted { get; set; }
    [Networked(OnChanged = nameof(OnInOptionsChanged))]          public NetworkBool IsInOptions { get; set; }
    [Networked]                                                  public TickTimer MessageCooldownTimer { get; set; }
    [Networked(OnChanged = nameof(OnCharacterChanged))]          public byte CharacterIndex { get; set; }
    [Networked(OnChanged = nameof(OnSkinChanged))]               public byte SkinIndex { get; set; }
    [Networked(OnChanged = nameof(OnSettingChanged))]            public int Ping { get; set; }
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

        JoinTick = Runner.Tick;

        if (Object.InputAuthority == Runner.SessionInfo.MaxPlayers - 1) {
            Team = 0;
            IsRoomOwner = true;
        } else {
            Team = (sbyte) ((Object.InputAuthority + 1) % 5);
        }
    }

    public void ReassignPlayerData(PlayerRef player) {
        Object.AssignInputAuthority(player);
        Runner.SetPlayerObject(player, Object);

        if (player == Runner.LocalPlayer) {
            JoinTick = -1;
            IsRoomOwner = true;
        }
    }

    public override void Spawned() {
        // Keep track of our data, pls kthx
        Runner.SetPlayerObject(Object.InputAuthority, Object);

        if (Runner.IsResume) {
            SetNickname(ConnectionToken.nickname.Value);
            Ping = 0;
        }

        IsCurrentlySpectating = SessionData.Instance ? SessionData.Instance.GameStarted : false;
        nicknameColor = NicknameColor.FromConnectionToken(ConnectionToken);

        if (SessionData.Instance)
            SessionData.Instance.LoadWins(this);

        if (HasInputAuthority) {
            // We're the client. update with our data.
            Rpc_SetCharacterIndex((byte) Settings.Instance.generalCharacter);
            Rpc_SetSkinIndex((byte) Settings.Instance.generalSkin);

            PauseOptionMenuManager.OnOptionsOpenedToggled += OnOptionsOpenToggled;
        }

        // Check if we need to play sfx / send a chat message
        if (SessionData.PlayersNeedingJoinMessage.Remove(Object.InputAuthority)) {
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.joined", "playername", GetNickname());

            if (MainMenuManager.Instance)
                MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_PlayerConnect);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (hasState)
            SessionData.Instance.SaveWins(this);

        if (HasInputAuthority) {
            PauseOptionMenuManager.OnOptionsOpenedToggled -= OnOptionsOpenToggled;
        }

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

        RawNickname = name;

        if (Object.InputAuthority != Runner.SessionInfo.MaxPlayers - 1) {
            // Check for players with duplicate names, and add (1), (2), etc
            int count = Runner.ActivePlayers
                .Where(pr => pr < Object.InputAuthority)
                .Select(pr => pr.GetPlayerData(Runner))
                .Where(pd => pd && pd.Object)
                .Where(pd => pd.RawNickname.ToString().Filter() == name)
                .Count();

            if (count > 0)
                name += " (" + count + ")";
        }

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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetOptionsOpen(bool open) {
        IsInOptions = open;
    }

    private void OnOptionsOpenToggled(bool isOpen) {
        Rpc_SetOptionsOpen(isOpen);
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
        changed.Behaviour.gameObject.name = "PlayerData (" + changed.Behaviour.DisplayNickname + ", " + changed.Behaviour.UserId.ToString() + ")";
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

    public static void OnInOptionsChanged(Changed<PlayerData> changed) {
        changed.Behaviour.OnInOptionsChangedEvent?.Invoke(changed.Behaviour.IsInOptions);
    }
}
