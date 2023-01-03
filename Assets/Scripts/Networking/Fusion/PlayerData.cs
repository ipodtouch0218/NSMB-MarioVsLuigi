using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

using Fusion;
using Fusion.Sockets;
using NSMB.Extensions;
using NSMB.Utils;

public class PlayerData : NetworkBehaviour {

    //---Static stuffs
    public static bool Locked => SessionData.Instance && SessionData.Instance.GameStarted;

    //---Networked Variables
    [Networked(OnChanged = nameof(OnNameChanged)), Capacity(20)] private string Nickname { get; set; } = "noname";
    [Networked, Capacity(28)]                                    private string DisplayNickname { get; set; } = "noname";
    [Networked, Capacity(32)]                                    private string UserId { get; set; }
    [Networked]                                                  public sbyte PlayerId { get; set; }
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

    //---Private Variables
    private Tick lastUpdatedTick;
    private NetAddress address;
    private string cachedUserId;

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
            string nickname = Encoding.UTF8.GetString(Runner.GetPlayerConnectionToken(Object.InputAuthority) ?? Encoding.UTF8.GetBytes("noname"));
            SetNickname(nickname);

            //expose their userid
            //TODO: use an auth-server signed userid, to disallow userid spoofing.
            UserId = Runner.GetPlayerUserId(Object.InputAuthority)?.Replace("-", "");

            IsCurrentlySpectating = SessionData.Instance ? SessionData.Instance.GameStarted : false;
        }

        if (MainMenuManager.Instance)
            StartCoroutine(MainMenuManager.Instance.OnPlayerDataValidated(Object.InputAuthority));
    }

    public string GetRawNickname() {
        return Nickname.ToString();
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
        name = name[..Mathf.Min(name.Length, MainMenuManager.NicknameMax)];

        //if this new nickname is invalid, default back to "noname"
        if (name.Length < MainMenuManager.NicknameMin)
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

    public static void OnLoadStateChanged(Changed<PlayerData> changed) {
        if (changed.Behaviour.IsLoaded && GameManager.Instance)
            GameManager.Instance.OnPlayerLoaded();
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
        changed.Behaviour.gameObject.name = "PlayerData (" + changed.Behaviour.Nickname + ")";
    }

    public static void OnCharacterChanged(Changed<PlayerData> changed) {
        if (!MainMenuManager.Instance)
            return;

        MainMenuManager.Instance.SwapCharacter(changed.Behaviour.CharacterIndex, false);
        OnSettingChanged(changed);
    }

    public static void OnSkinChanged(Changed<PlayerData> changed) {
        if (!MainMenuManager.Instance)
            return;

        MainMenuManager.Instance.SwapPlayerSkin(changed.Behaviour.SkinIndex, false);
    }
}
