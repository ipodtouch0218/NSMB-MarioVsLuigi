using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;
using NSMB.Utils;

public class CustomAnimatorSerializer : MonoBehaviour, ICustomSerializeView {

    public bool Active { get; set; }

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private int layerIndex = 0;

    private float lastSendTimestamp;
    private AnimatorStateInfo? lastSentState;

    public void Deserialize(List<byte> buffer, ref int index, PhotonMessageInfo info) {
        SerializationUtils.ReadInt(buffer, ref index, out int stateHash);
        SerializationUtils.UnpackFromByte(buffer, ref index, 0, 1, out float normalizedTime);

        /*
        float lag = (float) (PhotonNetwork.Time - info.SentServerTime);
        normalizedTime += lag;
        */

        animator.Play(stateHash, layerIndex, normalizedTime);
    }

    public void Serialize(List<byte> buffer) {

        if (lastSentState != null || (PhotonNetwork.Time - lastSendTimestamp < 1000)) {
            //don't send anything
            return;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);
        SerializationUtils.WriteInt(buffer, currentState.fullPathHash);
        SerializationUtils.PackToByte(buffer, currentState.normalizedTime, 0, 1);

        lastSendTimestamp = (float) PhotonNetwork.Time;
    }
}