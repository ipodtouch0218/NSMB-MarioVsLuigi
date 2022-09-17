using Fusion;

public class NetworkPlayerData : NetworkBehaviour {

    [Networked] public NetworkString<_32> Nickname { get; set; } = "noname";
    [Networked] public byte CharacterIndex { get; set; }
    [Networked] public byte SkinIndex { get; set; }
    [Networked] public bool IsPermanentSpectator { get; set; }


    public override void Spawned() {
        //keep track of our data, pls kthx
        Runner.SetPlayerObject(Object.InputAuthority, Object);
    }

    #region RPCs
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetPermanentSpectator(bool value) {
        //not accepting changes at this time
        //TODO: change to "game started" somehow
        if (GameManager.Instance)
            return;

        IsPermanentSpectator = value;
    }



    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetCharacterIndex(byte index) {
        //invalid character...
        if (index >= GlobalController.Instance.characters.Length)
            return;

        //not accepting changes at this time
        //TODO: change to "game started" somehow
        if (GameManager.Instance)
            return;

        CharacterIndex = index;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetSkinIndex(byte index) {
        //invalid skin...
        if (index >= GlobalController.Instance.skins.Length)
            return;

        //not accepting changes at this time
        //TODO: change to "game started" somehow
        if (GameManager.Instance)
            return;

        SkinIndex = index;
    }
    #endregion
}