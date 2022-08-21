using Photon.Pun;

public static class PhotonViewExtensions {
    public static bool IsMineOrLocal(this PhotonView view) {
        return !view || view.IsMine;
    }

    //public static void RPCFunc(this PhotonView view, Delegate action, RpcTarget target, params object[] parameters) {
    //    view.RPC(nameof(action), target, parameters);
    //}
}