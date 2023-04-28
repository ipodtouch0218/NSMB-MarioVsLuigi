using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Utils;

public class ChatMessage : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private TMP_Text chatText;
    [SerializeField] private Image image;

    //---Public Variables
    public PlayerRef player;

    public void OnValidate() {
        if (!chatText) chatText = GetComponent<TMP_Text>();
        if (!image) image = GetComponent<Image>();
    }

    public void Initialize(string message, PlayerRef player) {
        chatText.text = message;
        this.player = player;
        UpdatePlayerColor();
    }

    public void UpdatePlayerColor() {
        if (player == PlayerRef.None) {
            image.color = Color.white;
        } else {
            image.color = Utils.GetPlayerColor(NetworkHandler.Runner, player, 0.15f);
        }
    }
}
