namespace Quantum {
    public partial struct PlayerData {
        public readonly bool CanSendChatMessage(Frame f) {
            return f.Number - LastChatMessage > 1 * f.UpdateRate;
        }
    }
}