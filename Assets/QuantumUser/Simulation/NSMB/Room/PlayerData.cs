namespace Quantum {
    public partial struct PlayerData {
        public bool CanSendChatMessage(Frame f) {
            return f.Number - LastChatMessage > 1 * f.UpdateRate;
        }
    }
}