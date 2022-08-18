using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Photon.Pun;
using NSMB.Utils;

public class CustomNetworkSerializer : MonoBehaviour, IPunObservable {

    [SerializeField]
    private List<Component> serializableViews;
    private List<ICustomSerializeView> views;

    private readonly List<byte> buffer = new();
    private int lastReceivedTimestamp;

    public void Awake() {
        views = serializableViews.Where((view) => view as ICustomSerializeView is not null).Cast<ICustomSerializeView>().ToList();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
        //dont serialize when game is over
        if (!GameManager.Instance || (GameManager.Instance && GameManager.Instance.gameover))
            return;

        //clear byte buffer
        buffer.Clear();

        if (stream.IsWriting) {
            //write to buffer

            for (byte i = 0; i < views.Count; i++) {
                var view = views[i];
                if (!view.Active)
                    continue;

                int bufferSize = buffer.Count;

                view.Serialize(buffer);
                //Debug.Log(view.GetType().Name + " = " + string.Join(",", buffer.Skip(bufferSize)));

                //data was written, write component id header
                if (buffer.Count != bufferSize)
                    buffer.Insert(bufferSize, i);
            }

            byte[] uncompressed = buffer.ToArray();
            /*
            //compression
            buffer.Insert(0, 0);
            uncompressed = buffer.ToArray();
            buffer[0] = 1;
            byte[] compressed = SerializationUtils.Compress(buffer.ToArray());
            if (compressed.Length >= buffer.Count) {
                stream.SendNext(uncompressed);
            } else {
                stream.SendNext(compressed);
            }
            */
            stream.SendNext(uncompressed);

        } else if (stream.IsReading) {
            //check that packet is coming in order
            int oldTimestamp = lastReceivedTimestamp;
            lastReceivedTimestamp = info.SentServerTimestamp;

            if (info.SentServerTimestamp - oldTimestamp < 0)
                return;

            //incoming bytes
            byte[] bytes = (byte[]) stream.ReceiveNext();
            /*
            byte compressed = bytes[0];
            if (bytes[0] == 1)
                bytes = SerializationUtils.Decompress(bytes);
            */

            buffer.AddRange(bytes);

            int index = 0;

            //deserialize
            while (index < buffer.Count) {
                SerializationUtils.ReadByte(buffer, ref index, out byte view);
                views[view].Deserialize(buffer, ref index, info);
            }
        }
    }
}