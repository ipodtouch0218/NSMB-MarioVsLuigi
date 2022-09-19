using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

using NSMB.Extensions;
using NSMB.Utils;
using Fusion;

public class PlayerData : NetworkBehaviour {

    //---Static stuffs
    //TODO: change to "game started" somehow
    public static bool Locked { get => GameManager.Instance; }

    //---Networked Variables
    [Networked] private NetworkString<_32> Nickname { get; set; } = "noname";
    [Networked] private NetworkString<_32> UserId { get; set; }
    [Networked] public NetworkBool IsNicknameSet { get; set; }
    [Networked] public NetworkBool IsMuted { get; set; }
    [Networked] public NetworkBool IsManualSpectator { get; set; }
    [Networked] public NetworkBool IsCurrentlySpectating { get; set; }
    [Networked(OnChanged = nameof(OnLoadStateChanged))] public NetworkBool IsLoaded { get; set; }
    [Networked] public TickTimer MessageCooldownTimer { get; set; }
    [Networked] public byte CharacterIndex { get; set; }
    [Networked] public byte SkinIndex { get; set; }

    //---Misc Variables
    private string cachedUserId = null;


    public void Awake() {
        DontDestroyOnLoad(gameObject);
    }

    public override void Spawned() {
        if (Object.HasInputAuthority) {
            //we're the client. update with our data.
            Rpc_SetNickname(Settings.Instance.nickname);
            Rpc_SetCharacterIndex(Settings.Instance.character);
            Rpc_SetSkinIndex(Settings.Instance.skin);
        }

        if (Runner.IsServer) {
            //expose their userid
            UserId = Runner.GetPlayerUserId(Object.InputAuthority).Replace("-", "");
        }

        //keep track of our data, pls kthx
        Runner.SetPlayerObject(Object.InputAuthority, Object);
    }

    public string GetNickname() {
        return Nickname.ToString().Filter();
    }

    public string GetUserId() {
        if (cachedUserId == null)
            cachedUserId = Regex.Replace(UserId.ToString(), "(.{8})(.{4})(.{4})(.{4})(.{12})", "$1-$2-$3-$4-$5");

        return cachedUserId;
    }

    public static void OnLoadStateChanged(Changed<PlayerData> changed) {
        GameManager.Instance.OnPlayerLoaded(changed.Behaviour);
    }

    #region RPCs
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetNickname(string name) {
        //don't allow changing nicknames after we've already set it.
        if (IsNicknameSet)
            return;

        //limit nickname to valid characters only.
        name = Regex.Replace(name, @"[^\p{L}\d]", "");

        //enforce character limits
        name = name.Substring(0, Mathf.Max(name.Length, MainMenuManager.NICKNAME_MAX));

        //if this new nickname is invalid, default back to "noname"
        if (name.Length < MainMenuManager.NICKNAME_MIN)
            name = "noname";

        //check for players with duplicate names, and add (1), (2), etc
        int count = Runner.ActivePlayers.Where(pr => pr.GetPlayerData(Runner).Nickname.ToString().Split(" ")[0] == name).Count();
        if (count >= 0)
            name += " (" + count + ")";

        Nickname = name;
        IsNicknameSet = true;
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
        if (index >= GlobalController.Instance.characters.Length)
            return;

        CharacterIndex = index;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetSkinIndex(byte index) {
        //not accepting changes at this time
        //TODO: change to "game started" somehow
        if (Locked)
            return;

        //invalid skin...
        if (index >= GlobalController.Instance.skins.Length)
            return;

        SkinIndex = index;
    }
    #endregion
}