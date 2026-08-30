namespace Quantum {
    public unsafe partial struct PlayerData {

        public readonly bool IsRoomHost(Frame f) {
            return f.Global->Host == PlayerRef;
        }

        public readonly bool CanSendChatMessage(Frame f) {
            return f.Number - LastChatMessage > 1 * f.UpdateRate;
        }

        public void SetAsHost(Frame f, bool sendEvent) {
            if (IsRoomHost(f)) {
                return;
            }

            if (sendEvent && f.Global->Host != PlayerRef.None) {
                f.Events.PlayerDataChanged(f.Global->Host);
            }

            f.Global->Host = PlayerRef;
            
            // These should not be true while hosting
            IsReady = false;
            IsTeamLocked = false;
            
            if (sendEvent) {
                f.Events.HostChanged(PlayerRef);
                f.Events.PlayerDataChanged(PlayerRef);
            }
        }
    }
}