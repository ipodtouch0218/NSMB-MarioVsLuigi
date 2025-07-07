
using Photon.Deterministic;

namespace Quantum {
    public unsafe class StarChasersGamemode : GamemodeAsset {

        public AssetRef<EntityPrototype> BigStarPrototype;

        public override void EnableGamemode(Frame f) {
            f.SystemEnable<BigStarSystem>();
            f.Global->AutomaticStageRefreshTimer = f.Global->AutomaticStageRefreshInterval = 0;
        }

        public override void DisableGamemode(Frame f) {
            f.SystemDisable<BigStarSystem>();
        }

        public override void CheckForGameEnd(Frame f) {

            // End Condition: only one team alive
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;

            bool livesGame = f.Global->Rules.IsLivesEnabled;
            bool oneOrNoTeamAlive = true;
            int aliveTeam = -1;
            while (marioFilter.NextUnsafe(out _, out MarioPlayer* mario)) {
                if ((livesGame && mario->Lives <= 0) || mario->Disconnected) {
                    continue;
                }

                if (aliveTeam == -1 && mario->GetTeam(f) is byte team) {
                    aliveTeam = team;
                } else {
                    oneOrNoTeamAlive = false;
                    break;
                }
            }

            if (oneOrNoTeamAlive) {
                if (aliveTeam == -1) {
                    // It's a draw
                    GameLogicSystem.EndGame(f, false, null);
                    return;
                } else if (f.Global->RealPlayers > 1) {
                    // <team> wins, assuming more than 1 player
                    // so the player doesn't insta-win in a solo game.
                    GameLogicSystem.EndGame(f, false, aliveTeam);
                    return;
                }
            }

            int? winningTeam = GetWinningTeam(f, out int stars);

            // End Condition: team gets to enough stars
            if (winningTeam != null && stars >= f.Global->Rules.StarsToWin) {
                // <team> wins
                GameLogicSystem.EndGame(f, false, winningTeam.Value);
                return;
            }

            // End Condition: timer expires
            if (f.Global->Rules.IsTimerEnabled && f.Global->Timer <= 0) {
                if (f.Global->Rules.DrawOnTimeUp) {
                    // It's a draw
                    GameLogicSystem.EndGame(f, false, null);
                    return;
                }

                // Check if one team is winning
                if (winningTeam != null) {
                    // <team> wins
                    GameLogicSystem.EndGame(f, false, winningTeam.Value);
                    return;
                }
            }
        }

        public override int GetObjectiveCount(Frame f, PlayerRef player) {
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;

            while (marioFilter.NextUnsafe(out _, out MarioPlayer* mario)) {
                if (player != mario->PlayerRef) {
                    continue;
                }

                return GetObjectiveCount(f, mario);
            }

            return -1;
        }

        public override int GetObjectiveCount(Frame f, MarioPlayer* mario) {
            if (mario == null || mario->Disconnected || (mario->Lives == 0 && f.Global->Rules.IsLivesEnabled)) {
                return -1;
            }

            // Make a copy to not modify the `type` variable
            // Which can cause desyncs.
            GamemodeSpecificData gamemodeDataCopy = mario->GamemodeData;
            return gamemodeDataCopy.StarChasers->Stars;
        }

        public override FP GetItemSpawnWeight(Frame f, CoinItemAsset item, int leaderStars, int ourStars) {
            int starsToWin = f.Global->Rules.StarsToWin;
            int starDifference = leaderStars - ourStars;
            FP bonus = item.LosingSpawnBonus * FPMath.Log(starDifference + 1, FP.E) * (FP._1 - ((FP) (starsToWin - leaderStars) / starsToWin));
            return FPMath.Max(0, item.SpawnChance + bonus);
        }
    }
}