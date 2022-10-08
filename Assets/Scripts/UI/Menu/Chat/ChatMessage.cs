using UnityEngine;
using TMPro;

public class ChatMessage : MonoBehaviour {

    [SerializeField] private TMP_Text chatText;

    public void SetText(string message) {
        chatText.text = message;
    }
}
