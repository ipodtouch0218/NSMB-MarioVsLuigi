using Photon.Deterministic;
using System;

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
            Span<int> objectiveCounts = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, objectiveCounts);

            int aliveTeamCount = 0;
            int aliveTeam = -1;
            for (int i = 0; i < objectiveCounts.Length; i++) {
                if (objectiveCounts[i] > -1) {
                    aliveTeamCount++;
                    aliveTeam = i;
                }
            }

            if (aliveTeamCount <= 1) {
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

            // End Condition: team gets to enough stars
            int? winningTeam = GetWinningTeam(f, out int stars);
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
            if (mario == null || mario->Disconnected || (mario->Lives == 0 && (f.Global->Rules.IsLivesEnabled || mario->HadLives))) {
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