namespace Quantum {
    public unsafe partial struct PlayerInformation {

        public int GetStarCount(Frame f) {
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;

            while (marioFilter.NextUnsafe(out _, out MarioPlayer* mario)) {
                if (PlayerRef != mario->PlayerRef) {
                    continue;
                }
                
                if (mario->Disconnected || (mario->Lives == 0 && f.Global->Rules.IsLivesEnabled)) {
                    return -1;
                }

                return mario->Stars;
            }

            return -1;
        }
    }
}