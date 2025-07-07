namespace Quantum {
    public static class KillReasonExtensions {
    
        public static bool ShouldSpawnCoin(this KillReason reason) {
            return reason == KillReason.Special || reason == KillReason.Groundpounded;
        }
    }
}