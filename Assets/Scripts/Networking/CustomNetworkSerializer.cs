using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;

public class CustomNetworkSerializer : MonoBehaviour, IPunObservable {

    [SerializeField]
    private List<Component> serializableViews;
    private List<ICustomSerializeView> castedViews;

    private readonly List<byte> buffer = new();
    private int lastReceivedTimestamp;

    public void Awake() {
        castedViews = serializableViews.Where((view) => view as ICustomSerializeView is not null).Cast<ICustomSerializeView>().ToList();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
        //dont serialize when game is over
        if (!GameManager.Instance || (GameManager.Instance && GameManager.Instance.gameover))
            return;

        //clear byte buffer
        buffer.Clear();

        if (stream.IsWriting) {
            //write to buffer
            castedViews.Where((view) => view.Active).ToList().ForEach((view) => view.Serialize(buffer));

            //write to network
            stream.SendNext(buffer.ToArray());

        } else if (stream.IsReading) {
            //check that packet is coming in order
            int oldTimestamp = lastReceivedTimestamp;
            lastReceivedTimestamp = info.SentServerTimestamp;

            if (info.SentServerTimestamp - oldTimestamp < 0)
                return;

            //incoming bytes
            buffer.AddRange((byte[]) stream.ReceiveNext());
            int index = 0;

            //deserialize
            castedViews.Where((view) => view.Active).ToList().ForEach((view) => view.Deserialize(buffer, ref index, info));
        }
    }
}