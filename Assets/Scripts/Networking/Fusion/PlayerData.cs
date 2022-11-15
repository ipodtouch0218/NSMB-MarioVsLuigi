using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class PlayerData : NetworkBehaviour {

    //---Static stuffs
    public static bool Locked => LobbyData.Instance && LobbyData.Instance.GameStarted;

    //---Networked Variables
    [Networked(OnChanged = nameof(OnNameChanged)), Capacity(20)] private string Nickname { get; set; } = "noname";
    [Networked, Capacity(28)] private string DisplayNickname { get; set; } = "noname";
    [Networked, Capacity(32)] private string UserId { get; set; }
    [Networked] public sbyte PlayerId { get; set; }
    [Networked(OnChanged = nameof(OnStartSettingChanged))] public sbyte Team { get; set; }
    [Networked(OnChanged = nameof(OnStartSettingChanged))] public NetworkBool IsManualSpectator { get; set; }
    [Networked] public NetworkBool IsCurrentlySpectating { get; set; }
    [Networked] public NetworkBool IsRoomOwner { get; set; }
    [Networked(OnChanged = nameof(OnLoadStateChanged))] public NetworkBool IsLoaded { get; set; }
    [Networked] public NetworkBool IsMuted { get; set; }
    [Networked] public TickTimer MessageCooldownTimer { get; set; }
    [Networked(OnChanged = nameof(OnSettingChanged))] public byte CharacterIndex { get; set; }
    [Networked] public byte SkinIndex { get; set; }
    [Networked(OnChanged = nameof(OnSettingChanged))] public int Ping { get; set; }


    private Tick lastUpdatedTick;

    //---Misc Variables
    private string cachedUserId = null;

    public void Awake() {
        DontDestroyOnLoad(gameObject);
    }

    public override void Spawned() {
        //keep track of our data, pls kthx
        Runner.SetPlayerObject(Object.InputAuthority, Object);

        PlayerId = -1;
        Team = (sbyte) ((Object.InputAuthority + 1) % 5);

        if (Object.HasInputAuthority) {
            //we're the client. update with our data.
            Rpc_SetCharacterIndex(Settings.Instance.character);
            Rpc_SetSkinIndex(Settings.Instance.skin);

            if (Runner.IsServer)
                IsRoomOwner = true;
        }

        if (Runner.IsServer) {
            string nickname = Encoding.Unicode.GetString(Runner.GetPlayerConnectionToken(Object.InputAuthority) ?? Encoding.Unicode.GetBytes("noname"));
            SetNickname(nickname);

            //expose their userid
            //TOOD: use an auth-server signed userid, to disallow userid spoofing.
            UserId = Runner.GetPlayerUserId(Object.InputAuthority)?.Replace("-", "");

            IsCurrentlySpectating = LobbyData.Instance ? LobbyData.Instance.GameStarted : false;
        }

        if (MainMenuManager.Instance)
            StartCoroutine(MainMenuManager.Instance.OnPlayerDataValidated(Object.InputAuthority));
    }

    public string GetNickname(bool filter = true) {
        return filter ? DisplayNickname.ToString().Filter() : DisplayNickname.ToString();
    }

    public string GetUserId() {
        cachedUserId ??= Regex.Replace(UserId.ToString(), "(.{8})(.{4})(.{4})(.{4})(.{12})", "$1-$2-$3-$4-$5");

        return cachedUserId;
    }

    public void SetNickname(string name) {
        //limit nickname to valid characters only.
        name = Regex.Replace(name, @"[^\p{L}\d]", "");

        //enforce character limits
        name = name[..Mathf.Min(name.Length, MainMenuManager.NICKNAME_MAX)];

        //if this new nickname is invalid, default back to "noname"
        if (name.Length < MainMenuManager.NICKNAME_MIN)
            name = "noname";

        Nickname = name;
        gameObject.name = "PlayerData (" + name + ")";

        //check for players with duplicate names, and add (1), (2), etc
        int count = Runner.ActivePlayers.Where(pr => pr.GetPlayerData(Runner).Nickname.ToString().Filter() == name).Count() - 1;
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SceneLoaded() {
        //no gamemanager, how can we be loaded???
        if (!GameManager.Instance)
            return;

        IsLoaded = true;
    }

    public static void OnLoadStateChanged(Changed<PlayerData> changed) {
        if (GameManager.Instance)
            GameManager.Instance.OnPlayerLoaded();
    }

    public static void OnSettingChanged(Changed<PlayerData> changed) {
        if (!MainMenuManager.Instance)
            return;

        if (changed.Behaviour.lastUpdatedTick >= changed.Behaviour.Runner.Tick) {
            return;
        } else {
            changed.Behaviour.lastUpdatedTick = changed.Behaviour.Runner.Tick;
        }

        MainMenuManager.Instance.playerList.UpdateAllPlayerEntries();
    }

    public static void OnStartSettingChanged(Changed<PlayerData> changed) {
        if (!MainMenuManager.Instance)
            return;

        MainMenuManager.Instance.UpdateStartGameButton();
        OnSettingChanged(changed);
    }

    public static void OnNameChanged(Changed<PlayerData> changed) {
        changed.Behaviour.gameObject.name = "PlayerData (" + changed.Behaviour.Nickname + ")";
    }
}
