using Quantum;
using TMPro;
using UnityEngine;

public class CameraReplayUITab : ReplayUITab {

    //---Serialized Variables
    [SerializeField] private TMP_Text template;

    public unsafe void Start() {
        template.gameObject.SetActive(false);
        Frame f = NetworkHandler.Game.Frames.Predicted;
        for (int i = 0; i < f.Global->RealPlayers; i++) {
            ref PlayerInformation playerInfo = ref f.Global->PlayerInfo[i];

            TMP_Text newLabel = Instantiate(template, transform);
            newLabel.text = playerInfo.Nickname.ToString();
            newLabel.gameObject.SetActive(true);
            selectables.Add(newLabel.gameObject);
        }
    }
}
