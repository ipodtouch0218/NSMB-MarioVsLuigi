using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public static class PhotonViewExtensions {
    public static bool IsMineOrLocal(this PhotonView view) {
        return !view || view.IsMine;
    }
}