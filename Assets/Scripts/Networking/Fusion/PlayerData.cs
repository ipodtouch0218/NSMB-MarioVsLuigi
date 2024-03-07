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
using System.Collections;

public class PlayerData : NetworkBehaviour, IStateAuthorityChanged {

    //---Static stuffs
    public bool Locked => SessionData.Instance && SessionData.Instance.GameStarted && !IsCurrentlySpectating;

    //---Events
    public static event Action<PlayerData> OnPlayerDataReady;
    public static event Action<PlayerData> OnPlayerDataDespawned;
    public event Action<bool> OnInOptionsChangedEvent;
    public event Action<bool> OnIsReadyChangedEvent;

    //---Networked Variables
    [Networked] public PlayerRef Owner { get; set; }
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
    private bool IsLocal => Owner == Runner.LocalPlayer;

    //---Private Variables
    private NicknameColor nicknameColor = NicknameColor.White;
    private Tick lastUpdatedTick;
    private string filteredNickname;
    private ChangeDetector changeDetector;
    private bool sentJoinMessage;
    private Coroutine pingRoutine;

    public void Awake() {
        DontDestroyOnLoad(gameObject);
    }

    public void OnBeforeSpawned(PlayerRef owner) {
        Owner = owner;

        /*
        // Expose their connection token :flushed:
        byte[] token = Runner.GetPlayerConnectionToken(owner);
        try {
            ConnectionToken = ConnectionToken.Deserialize(token);
            if (!ConnectionToken.HasValidSignature()) {
                // Invalid signature, nice try guy
                throw new Exception();
            }
            if (ConnectionToken.signedData.UserId != Guid.Parse(Runner.GetPlayerUserId(Owner))) {
                // Attempted to steal from another user???
                throw new Exception();
            }
            // Successful :D
        } catch {
            if (!Runner.IsSinglePlayer) {
                Debug.LogWarning($"No/malformed/invalid connection token from player with id '{Runner.GetPlayerUserId(Owner)}'.");
            }

            SetNickname(ConnectionToken.nickname.Value);
            ConnectionToken = new();
        }
        */

        // Find the least populated team and automatically join that one.
        if (SessionData.Instance) {
            int[] teamCounts = new int[5];
            foreach ((_, PlayerData data) in SessionData.Instance.PlayerDatas) {
                teamCounts[data.Team]++;
            }

            int minIndex = 0;
            for (int i = 1; i < teamCounts.Length; i++) {
                if (teamCounts[i] < teamCounts[minIndex]) {
                    minIndex = i;
                }
            }

            Team = (sbyte) minIndex;
        }

        IsRoomOwner = (Owner == Runner.LocalPlayer);
        JoinTick = IsRoomOwner ? -1 : Runner.Tick;
    }

    public override void Spawned() {
        if (SessionData.Instance) {
            SessionData.Instance.PlayerDatas.Add(Owner, this);
        }

        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority) {
            IsCurrentlySpectating = SessionData.Instance ? SessionData.Instance.GameStarted : false;
            Ping = (int) (Runner.GetPlayerRtt(Owner) * 1000);

            if (SessionData.Instance) {
                SessionData.Instance.LoadWins(this);
            }
        } else {
            OnConnectionTokenChanged();
        }

        if (IsLocal) {
            // We're the client. update with our data.
            Rpc_SetConnectionToken(GlobalController.Instance.connectionToken);
            Rpc_SetCharacterIndex((byte) Settings.Instance.generalCharacter);
            Rpc_SetSkinIndex((byte) Settings.Instance.generalSkin);
            StartCoroutine(UpdatePingRoutine());

            PauseOptionMenuManager.OnOptionsOpenedToggled += OnOptionsOpenToggled;
        }

        UpdateObjectName();
    }

    public void StateAuthorityChanged() {
        if (!HasStateAuthority) {
            return;
        }

        if (Owner == Runner.LocalPlayer) {
            // We are the new master client...
            IsMuted = false;
            IsRoomOwner = true;
            IsReady = false;
        } else {
            // Other, non-master client player
            IsRoomOwner = false;
        }
    }

    public override void Render() {
        base.Render();

        bool ready = false;
        foreach (var change in changeDetector.DetectChanges(this)) {
            switch (change) {
            case nameof(Team):
            case nameof(IsManualSpectator):
                OnStartSettingChanged();
                break;
            case nameof(IsRoomOwner): OnRoomOwnerChanged(); break;
            case nameof(Wins): OnSettingChanged(); break;
            case nameof(IsMuted): OnIsMutedChanged(); break;
            case nameof(Ping): OnPingChanged(); break;
            case nameof(IsLoaded): OnLoadStateChanged(); break;
            case nameof(IsInOptions): OnInOptionsChanged(); break;
            case nameof(IsReady): OnIsReadyChanged(IsReady); break;
            case nameof(CharacterIndex): OnCharacterChanged(); break;
            case nameof(SkinIndex): OnSkinChanged(); break;
            case nameof(DisplayNickname): OnDisplayNicknameChanged(); ready = true; break;
            case nameof(ConnectionToken): OnConnectionTokenChanged(); ready = true; break;
            }
        }

        if (ready) {
            OnPlayerDataReady?.Invoke(this);
        }
    }

    public override void FixedUpdateNetwork() {
        if (!HasStateAuthority) {
            return;
        }

        if (!ConnectionToken.HasValidSignature() && (Runner.Tick - JoinTick) > (int) (Runner.TickRate * 10f)) {
            // Kick player for not sending the token in time...
            //Runner.Disconnect(Owner);
            SessionData.Instance.Rpc_Disconnect(Owner);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (pingRoutine != null) {
            StopCoroutine(pingRoutine);
        }

        OnPlayerDataDespawned?.Invoke(this);

        if (hasState) {
            SessionData.Instance.SaveWins(this);
        }

        if (IsLocal) {
            PauseOptionMenuManager.OnOptionsOpenedToggled -= OnOptionsOpenToggled;
        }
    }

    public void SendJoinMessageIfNeeded() {
        if (sentJoinMessage) {
            return;
        }

        if (Owner == Runner.LocalPlayer && IsRoomOwner) {
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.hostreminder");
        }

        ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.joined", "playername", GetNickname());

        if (MainMenuManager.Instance) {
            MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_PlayerConnect);
        }

        sentJoinMessage = true;
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

        if (Runner.LocalPlayer != Owner) {
            // Check for players with duplicate names, and add (1), (2), etc
            int count = SessionData.Instance.PlayerDatas
                .Select(kvp => kvp.Value)
                .Where(pd => pd.JoinTick < JoinTick)
                .Where(pd => pd.RawNickname.ToString().Filter() == name)
                .Count();

            if (count > 0) {
                name += " (" + count + ")";
            }
        }

        DisplayNickname = name;
    }

    private IEnumerator UpdatePingRoutine() {
        int avgRate = 10;
        WaitForSeconds waitTime = new(1f / avgRate);

        while (true) {
            double total = 0;
            for (int i = 0; i < avgRate; i++) {
                total += Runner.GetPlayerRtt(Owner);
                yield return waitTime;
            }

            Rpc_SetPing((int) (total / avgRate * 1000));
            yield return waitTime;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_SetPing(int ping, RpcInfo info = default) {
        if (info.Source != Owner) {
            return;
        }

        Ping = ping;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_SetConnectionToken(ConnectionToken token, RpcInfo info = default) {
        if (info.Source != Owner) {
            return;
        }

        ConnectionToken = token;
        SetNickname(ConnectionToken.nickname.Value);
        //OnConnectionTokenChanged(); // Needed, for some reason...
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_FinishedLoading(RpcInfo info = default) {
        if (info.Source != Owner) {
            return;
        }

        IsLoaded = true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_SetPermanentSpectator(bool value, RpcInfo info = default) {
        if (info.Source != Owner) {
            return;
        }

        // Not accepting changes at this time
        if (Locked) {
            return;
        }

        IsManualSpectator = value;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_SetCharacterIndex(byte index, RpcInfo info = default) {
        if (info.Source != Owner) {
            return;
        }

        // Not accepting changes at this time
        if (Locked) {
            return;
        }

        // Invalid character...
        if (index >= ScriptableManager.Instance.characters.Length) {
            return;
        }

        CharacterIndex = index;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_SetSkinIndex(byte index, RpcInfo info = default) {
        if (info.Source != Owner) {
            return;
        }

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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_SetTeamNumber(sbyte team, RpcInfo info = default) {
        if (info.Source != Owner) {
            return;
        }

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

    [Rpc(RpcSources.All, RpcTargets.InputAuthority | RpcTargets.StateAuthority)]
    public void Rpc_SetOptionsOpen(bool open, RpcInfo info = default) {
        if (info.Source != Owner) {
            return;
        }

        if (HasStateAuthority) {
            IsInOptions = open;
        } else if (IsLocal) {
            // Bodge for the lack of "InvokeResim" in Fusion 2
            // Makes it so the icon appears.. "predictively"? Is that a word?
            OnInOptionsChanged();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.InputAuthority | RpcTargets.StateAuthority)]
    public void Rpc_SetIsReady(bool ready, RpcInfo info = default) {
        if (info.Source != Owner) {
            return;
        }

        if (HasStateAuthority) {
            IsReady = ready;
        } else if (IsLocal) {
            // Bodge for the lack of "InvokeResim" in Fusion 2
            // Makes it so the icon appears.. "predictively"? Is that a word?
            OnIsReadyChanged(ready);
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

    public void OnIsMutedChanged() {
        if (MainMenuManager.Instance) {
            MainMenuManager.Instance.chat.OnDisableChatChanged();
        }
    }

    public void OnPingChanged() {
        if (Owner == Runner.LocalPlayer && MainMenuManager.Instance) {
            MainMenuManager.Instance.playerList.UpdateAllPlayerEntries();
        }
    }

    public void OnRoomOwnerChanged() {
        if (IsRoomOwner) {
            ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.promote", "playername", GetNickname());

            if (Runner.LocalPlayer == Owner) {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.hostreminder");
            }
        }

        OnSettingChanged();
    }

    public void OnSettingChanged() {
        if (!MainMenuManager.Instance || lastUpdatedTick >= Runner.Tick) {
            return;
        }

        lastUpdatedTick = Runner.Tick;
        MainMenuManager.Instance.playerList.UpdatePlayerEntry(this);
    }

    public void OnStartSettingChanged() {
        if (!MainMenuManager.Instance) {
            return;
        }

        MainMenuManager.Instance.UpdateStartGameButton();
        MainMenuManager.Instance.playerList.UpdateAllPlayerEntries();
    }

    public void OnCharacterChanged() {
        if (!MainMenuManager.Instance || !IsLocal) {
            return;
        }

        MainMenuManager.Instance.SwapCharacter(CharacterIndex, false);
        OnSettingChanged();
    }

    public void OnSkinChanged() {
        if (!MainMenuManager.Instance || !IsLocal) {
            return;
        }

        MainMenuManager.Instance.SwapPlayerSkin(SkinIndex, false);
    }

    public void OnDisplayNicknameChanged() {
        UpdateObjectName();
        SendJoinMessageIfNeeded();
    }

    public void OnConnectionTokenChanged() {
        if (ConnectionToken.signedData.UserId.ToString() == Runner.GetPlayerUserId(Owner)) {
            nicknameColor = NicknameColor.FromConnectionToken(ConnectionToken);
        } else {
            nicknameColor = NicknameColor.White;
        }
    }

    public void OnInOptionsChanged() {
        OnInOptionsChangedEvent?.Invoke(IsInOptions);
    }

    public void OnIsReadyChanged(bool state) {
        OnIsReadyChangedEvent?.Invoke(state);

        if (MainMenuManager.Instance && IsLocal) {
            MainMenuManager.Instance.UpdateReadyButton(state);
        }
    }
}
