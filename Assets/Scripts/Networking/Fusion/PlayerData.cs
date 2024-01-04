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
    public event Action<bool> OnIsReadyChangedEvent;

    //---Networked Variables
    [Networked, Capacity(20)] public string RawNickname { get; set; } = "noname";
    [Networked, Capacity(28)] private string DisplayNickname { get; set; } = "noname";
    [Networked] public ConnectionToken ConnectionToken { get; set; }
    [Networked] public sbyte PlayerId { get; set; }
    [Networked] public uint Wins { get; set; }
    [Networked] public sbyte Team { get; set; }
    [Networked] public NetworkBool IsManualSpectator { get; set; }
    [Networked] public NetworkBool IsCurrentlySpectating { get; set; }
    [Networked] public NetworkBool IsRoomOwner { get; set; }
    [Networked] public NetworkBool IsLoaded { get; set; }
    [Networked] public NetworkBool IsMuted { get; set; }
    [Networked] public NetworkBool IsInOptions { get; set; }
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public TickTimer MessageCooldownTimer { get; set; }
    [Networked] public byte CharacterIndex { get; set; }
    [Networked] public byte SkinIndex { get; set; }
    [Networked] public int Ping { get; set; }
    [Networked] public int JoinTick { get; set; }

    public Guid UserId => ConnectionToken.signedData.UserId;
    public NicknameColor NicknameColor => nicknameColor;

    //---Private Variables
    private NicknameColor nicknameColor;
    private Tick lastUpdatedTick;
    private string filteredNickname;
    private ChangeDetector changeDetector;

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
            if (!Runner.IsSinglePlayer) {
                Debug.LogWarning($"No/malformed/invalid connection token from player with id '{Runner.GetPlayerUserId(Object.InputAuthority)}'.");
            }

            SetNickname(ConnectionToken.nickname.Value);
            ConnectionToken = new();
        }

        // Find the least populated team and automatically join that one.
        var playerDatas =
            Runner.ActivePlayers
                .Select(pr => pr.GetPlayerData(Runner))
                .Where(pd => pd != null);

        int[] teamCounts = new int[5];
        foreach (PlayerData data in playerDatas) {
            teamCounts[data.Team]++;
        }

        int minIndex = 0;
        for (int i = 1; i < teamCounts.Length; i++) {
            if (teamCounts[i] < teamCounts[minIndex]) {
                minIndex = i;
            }
        }

        Team = (sbyte) minIndex;
        IsRoomOwner = (Object.InputAuthority == Runner.LocalPlayer);
        JoinTick = IsRoomOwner ? -1 : Runner.Tick;
    }

    public void ReassignPlayerData(PlayerRef player) {
        Object.AssignInputAuthority(player);
        Runner.SetPlayerObject(player, Object);

        IsRoomOwner = (Object.InputAuthority == Runner.LocalPlayer);
        if (IsRoomOwner) {
            JoinTick = -1;
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

        if (SessionData.Instance) {
            SessionData.Instance.LoadWins(this);
        }

        if (HasInputAuthority) {
            // We're the client. update with our data.
            Rpc_SetCharacterIndex((byte) Settings.Instance.generalCharacter);
            Rpc_SetSkinIndex((byte) Settings.Instance.generalSkin);

            PauseOptionMenuManager.OnOptionsOpenedToggled += OnOptionsOpenToggled;
        }


        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // Check if we need to play sfx / send a chat message
        SendJoinMessageIfNeeded();

        UpdateObjectName();
        NetworkHandler.Instance.playerDatas.Add(this);
    }

    public override void Render() {
        base.Render();

        foreach (var change in changeDetector.DetectChanges(this)) {
            switch (change) {
            case nameof(Team):
            case nameof(IsManualSpectator):
                OnStartSettingChanged();
                break;
            case nameof(Ping): OnSettingChanged(); break;
            case nameof(IsLoaded): OnLoadStateChanged(); break;
            case nameof(IsInOptions): OnInOptionsChanged(); break;
            case nameof(IsReady): OnIsReadyChanged(); break;
            case nameof(CharacterIndex): OnCharacterChanged(); break;
            case nameof(SkinIndex): OnSkinChanged(); break;
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (hasState) {
            SessionData.Instance.SaveWins(this);
        }

        if (HasInputAuthority) {
            PauseOptionMenuManager.OnOptionsOpenedToggled -= OnOptionsOpenToggled;
        }

        runner.SetPlayerObject(Object.InputAuthority, null);
    }

    public void SendJoinMessageIfNeeded() {
        if (!SessionData.PlayersNeedingJoinMessage.Remove(Object.InputAuthority)) {
            return;
        }

        ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.joined", "playername", GetNickname());

        if (MainMenuManager.Instance) {
            MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_PlayerConnect);
        }
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
        if (name.Length < MainMenuManager.NicknameMin) {
            name = "noname";
        }

        RawNickname = name;

        if (Object.InputAuthority.AsIndex != Runner.SessionInfo.MaxPlayers - 1) {
            // Check for players with duplicate names, and add (1), (2), etc
            int count = Runner.ActivePlayers
                .Select(pr => pr.GetPlayerData(Runner))
                .Where(pd => pd && pd.Object)
                .Where(pd => pd.JoinTick < JoinTick)
                .Where(pd => pd.RawNickname.ToString().Filter() == name)
                .Count();

            if (count > 0) {
                name += " (" + count + ")";
            }
        }

        DisplayNickname = name;
        UpdateObjectName();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_FinishedLoading() {
        IsLoaded = true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetPermanentSpectator(bool value) {
        //not accepting changes at this time
        if (Locked) {
            return;
        }

        IsManualSpectator = value;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetCharacterIndex(byte index) {
        //not accepting changes at this time
        if (Locked) {
            return;
        }

        //invalid character...
        if (index >= ScriptableManager.Instance.characters.Length) {
            return;
        }

        CharacterIndex = index;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetSkinIndex(byte index) {
        // Not accepting changes at this time
        if (Locked) {
            return;
        }

        // Invalid skin...
        if (index >= ScriptableManager.Instance.skins.Length) {
            return;
        }

        SkinIndex = index;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetTeamNumber(sbyte team) {
        // Not accepting changes at this time
        if (Locked) {
            return;
        }

        // Invalid team...
        if (team < 0 || team > 4) {
            return;
        }

        Team = team;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority | RpcTargets.StateAuthority)]
    public void Rpc_SetOptionsOpen(bool open) {
        if (HasStateAuthority) {
            IsInOptions = open;
        } else if (HasInputAuthority) {
            // Bodge for the lack of "InvokeResim" in Fusion 2
            // Makes it so the icon appears.. "predictively"? Is that a word?
            OnInOptionsChanged();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority | RpcTargets.StateAuthority)]
    public void Rpc_SetIsReady(bool ready) {
        if (HasStateAuthority) {
            IsReady = ready;
        } else if (HasInputAuthority) {
            // Bodge for the lack of "InvokeResim" in Fusion 2
            // Makes it so the icon appears.. "predictively"? Is that a word?
            OnIsReadyChanged();
        }
    }

    public void UpdateObjectName() {
        gameObject.name = "PlayerData (" + DisplayNickname + ", " + UserId.ToString() + ")";
    }

    private void OnOptionsOpenToggled(bool isOpen) {
        Rpc_SetOptionsOpen(isOpen);
    }

    public void OnLoadStateChanged() {
        if (IsLoaded && GameManager.Instance) {
            GameManager.Instance.CheckIfAllPlayersLoaded();
        }
    }

    public void OnSettingChanged() {
        if (!MainMenuManager.Instance || lastUpdatedTick >= Runner.Tick) {
            return;
        }

        lastUpdatedTick = Runner.Tick;
        MainMenuManager.Instance.playerList.UpdateAllPlayerEntries();
    }

    public void OnStartSettingChanged() {
        if (!MainMenuManager.Instance) {
            return;
        }

        MainMenuManager.Instance.UpdateStartGameButton();
        OnSettingChanged();
    }

    public void OnCharacterChanged() {
        if (!MainMenuManager.Instance || !HasInputAuthority) {
            return;
        }

        MainMenuManager.Instance.SwapCharacter(CharacterIndex, false);
        OnSettingChanged();
    }

    public void OnSkinChanged() {
        if (!MainMenuManager.Instance || !HasInputAuthority) {
            return;
        }

        MainMenuManager.Instance.SwapPlayerSkin(SkinIndex, false);
    }

    public void OnInOptionsChanged() {
        OnInOptionsChangedEvent?.Invoke(IsInOptions);
    }

    public void OnIsReadyChanged() {
        OnIsReadyChangedEvent?.Invoke(IsReady);

        if (MainMenuManager.Instance && HasInputAuthority) {
            MainMenuManager.Instance.UpdateStartGameButton();
        }
    }
}
