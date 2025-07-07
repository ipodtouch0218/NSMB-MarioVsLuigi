using Quantum;
using UnityEngine;

namespace NSMB.Chat {
    public class ChatMessageData {
        public PlayerRef player;
        public string userId;
        public Color color;
        public bool isSystemMessage;
        public string message;
        public string[] replacements;
    }
}
